[CmdletBinding()]
param(
    # Extra package sources to restore from, e.g. a local feed holding an unpublished Voxta.Model build.
    [string[]]$AdditionalSource = @(),
    [string]$PackageRoot = (Join-Path $(if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }) '.nuget/packages')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$probeRoot = Join-Path $PSScriptRoot 'Probe'
$probeProject = Join-Path $probeRoot 'VoxtaModelClosureProbe.csproj'
$lockPath = Join-Path $probeRoot 'packages.lock.json'
$thirdPartyRoot = Join-Path $repositoryRoot 'unity/Runtime/Plugins/ThirdParty'
$voxtaRoot = Join-Path $repositoryRoot 'unity/Runtime/Plugins/Voxta'
$pinnedModelPath = Join-Path $PSScriptRoot 'pinned-model.json'
# Kept outside the Unity package so the records are not imported into consuming projects.
$inventoryRoot = Join-Path $PSScriptRoot 'Inventory'
# Voxta.Model is first-party; the copy vendored into the Unity package is covered by the Asset Store EULA.
$voxtaModelLicense = 'first-party:Unity Asset Store EULA'

# Unity's .NET Standard 2.1 profile already supplies these; vendoring them causes duplicate references.
$unitySuppliedPackages = @(
    'System.Buffers'
    'System.ComponentModel.Annotations'
    'System.Memory'
    'System.Numerics.Vectors'
    'System.Threading.Tasks.Extensions'
)

# Unity 6.5+ adds these to the scripting profile (Editor/Data/BCLExtensions) at the same assembly
# versions SignalR 8 needs, and overrides any plug-in of the same name, so the vendored copies only
# load on older editors.
$unity65SuppliedAssemblies = @(
    'Microsoft.Bcl.AsyncInterfaces.dll'
    'Microsoft.Extensions.DependencyInjection.Abstractions.dll'
    'Microsoft.Extensions.Logging.Abstractions.dll'
    'System.Diagnostics.DiagnosticSource.dll'
    'System.Runtime.CompilerServices.Unsafe.dll'
    'System.Text.Encodings.Web.dll'
    'System.Text.Json.dll'
)
$unity65DefineConstraint = '!UNITY_6000_5_OR_NEWER'

$sources = @('https://api.nuget.org/v3/index.json') + $AdditionalSource
$restoreArguments = @('restore', $probeProject, '--use-lock-file', '--force-evaluate', '--packages', $PackageRoot)
foreach ($source in $sources) { $restoreArguments += @('--source', $source) }
& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed for the closure probe.' }

$lockDependencies = (Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json).dependencies.'.NETStandard,Version=v2.1'

function Write-TextFile($path, $text) {
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
}

function Get-ZipEntryBytes($archive, $entryName) {
    $entry = $archive.Entries | Where-Object FullName -eq $entryName
    $stream = $entry.Open()
    try {
        $memory = [IO.MemoryStream]::new()
        $stream.CopyTo($memory)
        return $memory.ToArray()
    }
    finally { $stream.Dispose() }
}

$inventory = @()
foreach ($property in $lockDependencies.PSObject.Properties) {
    $packageId = $property.Name
    if ($unitySuppliedPackages -contains $packageId) { continue }

    $version = $property.Value.resolved
    $lowerId = $packageId.ToLowerInvariant()
    $packageDirectory = Join-Path $PackageRoot "$lowerId/$version"
    $nupkgPath = Join-Path $packageDirectory "$lowerId.$version.nupkg"

    $archive = [IO.Compression.ZipFile]::OpenRead($nupkgPath)
    try {
        $asset = $null
        foreach ($framework in @('netstandard2.1', 'netstandard2.0')) {
            $asset = $archive.Entries | Where-Object { $_.FullName -like "lib/$framework/*.dll" } | Select-Object -First 1
            if ($asset) { break }
        }
        if (-not $asset) { throw "$packageId $version has no netstandard2.1 or netstandard2.0 assembly." }

        $nuspecEntry = $archive.Entries | Where-Object { $_.FullName -like '*.nuspec' -and $_.FullName -notlike '*/*' } | Select-Object -First 1
        [xml]$nuspec = [Text.Encoding]::UTF8.GetString((Get-ZipEntryBytes $archive $nuspecEntry.FullName)).TrimStart([char]0xFEFF)
        $licenseNode = $nuspec.package.metadata.license
        $license = if ($packageId -eq 'Voxta.Model') { $voxtaModelLicense }
            elseif ($licenseNode) { "$($licenseNode.type):$($licenseNode.'#text')" }
            else { "url:$($nuspec.package.metadata.licenseUrl)" }

        $assemblyBytes = Get-ZipEntryBytes $archive $asset.FullName
    }
    finally { $archive.Dispose() }

    $pluginRoot = if ($packageId -eq 'Voxta.Model') { $voxtaRoot } else { $thirdPartyRoot }
    $assemblyPath = Join-Path $pluginRoot $asset.Name
    [IO.File]::WriteAllBytes($assemblyPath, $assemblyBytes)

    $inventory += [pscustomobject][ordered]@{
        packageId      = $packageId
        version        = $version
        sourceUrl      = "https://api.nuget.org/v3-flatcontainer/$lowerId/$version/$lowerId.$version.nupkg"
        packageSha512  = $property.Value.contentHash
        license        = $license
        asset          = $asset.FullName
        assembly       = $asset.Name
        assemblySha512 = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA512).Hash
    }
}

$thirdPartyInventory = @($inventory | Where-Object packageId -ne 'Voxta.Model' | Sort-Object packageId)
$voxtaInventory = @($inventory | Where-Object packageId -eq 'Voxta.Model')

foreach ($pluginRoot in @($thirdPartyRoot, $voxtaRoot)) {
    $expected = @($inventory.assembly)
    foreach ($stale in Get-ChildItem -LiteralPath $pluginRoot -File -Filter '*.dll' | Where-Object { $expected -notcontains $_.Name }) {
        Remove-Item -LiteralPath $stale.FullName, "$($stale.FullName).meta" -ErrorAction SilentlyContinue
        Write-Host "Removed $($stale.Name), which the closure no longer needs."
    }
}

$metaTemplate = Join-Path $thirdPartyRoot 'System.Text.Json.dll.meta'
foreach ($entry in $inventory) {
    $pluginRoot = if ($entry.packageId -eq 'Voxta.Model') { $voxtaRoot } else { $thirdPartyRoot }
    $metaPath = Join-Path $pluginRoot "$($entry.assembly).meta"
    if (-not (Test-Path -LiteralPath $metaPath)) {
        $guid = [guid]::NewGuid().ToString('N')
        Write-TextFile $metaPath ((Get-Content -Raw -LiteralPath $metaTemplate) -replace '(?m)^guid: [0-9a-f]{32}', "guid: $guid")
        Write-Host "Created $($entry.assembly).meta; review its platform settings."
    }

    $meta = Get-Content -Raw -LiteralPath $metaPath
    $newline = if ($meta.Contains("`r`n")) { "`r`n" } else { "`n" }
    $constraints = if ($unity65SuppliedAssemblies -contains $entry.assembly) { "$newline  - '$unity65DefineConstraint'" } else { ' []' }
    Write-TextFile $metaPath ($meta -replace "(?ms)^  defineConstraints:.*?(?=^  \w)", "  defineConstraints:$constraints$newline")
}

Write-TextFile (Join-Path $inventoryRoot 'RUNTIME-MODEL-DEPENDENCY-INVENTORY.json') ((ConvertTo-Json @($thirdPartyInventory)) -replace "`r`n", "`n")
Write-TextFile (Join-Path $inventoryRoot 'VOXTA-MODEL-INVENTORY.json') ((ConvertTo-Json @($voxtaInventory)) -replace "`r`n", "`n")

$model = $voxtaInventory[0]
$signalR = $thirdPartyInventory | Where-Object packageId -eq 'Microsoft.AspNetCore.SignalR.Client'
$json = $thirdPartyInventory | Where-Object packageId -eq 'System.Text.Json'
$summary = @("Locked runtime dependency closure: SignalR $($signalR.version) / System.Text.Json $($json.version) / Voxta.Model $($model.version)", '')
function Format-InventoryLine($entry) { "$($entry.packageId) $($entry.version) [$($entry.asset)] package-content-hash=$($entry.packageSha512) assembly-sha512=$($entry.assemblySha512)" }
$summary += $thirdPartyInventory | ForEach-Object { Format-InventoryLine $_ }
Write-TextFile (Join-Path $inventoryRoot 'THIRD-PARTY-INVENTORY.txt') (($summary -join "`r`n") + "`r`n")
Write-TextFile (Join-Path $inventoryRoot 'VOXTA-MODEL-INVENTORY.txt') ((Format-InventoryLine $model) + "`r`n")

$pinned = (Get-Content -Raw -LiteralPath $pinnedModelPath) -replace '("packageVersion": ")[^"]*', "`${1}$($model.version)" -replace '("assemblySha512": ")[^"]*', "`${1}$($model.assemblySha512)"
Write-TextFile $pinnedModelPath $pinned

Write-Host "Vendored $($inventory.Count) runtime assemblies. Update pinned-model.json repositoryCommit and the asmdef precompiledReferences if the set changed."
