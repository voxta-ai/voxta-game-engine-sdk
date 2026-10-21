[CmdletBinding()]
param(
    [switch]$VerifyPackageAssets,
    [string]$PackageRoot = (Join-Path $(if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }) '.nuget/packages')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$thirdPartyRoot = Join-Path $repositoryRoot 'unity/Runtime/Plugins/ThirdParty'
$voxtaRoot = Join-Path $repositoryRoot 'unity/Runtime/Plugins/Voxta'
$inventoryRoot = Join-Path $PSScriptRoot 'Inventory'
$thirdPartyInventoryPath = Join-Path $inventoryRoot 'RUNTIME-MODEL-DEPENDENCY-INVENTORY.json'
$voxtaInventoryPath = Join-Path $inventoryRoot 'VOXTA-MODEL-INVENTORY.json'
$lockPath = Join-Path $repositoryRoot 'Tools/ModelDependencies/VoxtaModel/Probe/packages.lock.json'
$noticesPath = Join-Path $thirdPartyRoot 'Third-Party Notices.txt'
# Unity 6.5+ supplies these at 8.0 through its BCLExtensions profile and overrides same-named plug-ins.
$unity65SuppliedAssemblies = @(
    'Microsoft.Bcl.AsyncInterfaces.dll'
    'Microsoft.Extensions.DependencyInjection.Abstractions.dll'
    'Microsoft.Extensions.Logging.Abstractions.dll'
    'System.Diagnostics.DiagnosticSource.dll'
    'System.Runtime.CompilerServices.Unsafe.dll'
    'System.Text.Encodings.Web.dll'
    'System.Text.Json.dll'
)

foreach ($inventoryPath in @($thirdPartyInventoryPath, $voxtaInventoryPath)) {
    if (-not (Test-Path -LiteralPath $inventoryPath)) {
        throw "Missing runtime dependency inventory: $inventoryPath"
    }
}

$thirdPartyInventory = @(Get-Content -Raw -LiteralPath $thirdPartyInventoryPath | ConvertFrom-Json | ForEach-Object {
    $_ | Add-Member -NotePropertyName pluginRoot -NotePropertyValue $thirdPartyRoot -Force
    $_
})
$voxtaInventory = @(Get-Content -Raw -LiteralPath $voxtaInventoryPath | ConvertFrom-Json | ForEach-Object {
    $_ | Add-Member -NotePropertyName pluginRoot -NotePropertyValue $voxtaRoot -Force
    $_
})
$inventory = @($thirdPartyInventory + $voxtaInventory)
if ($inventory.Count -ne 23) {
    throw "Expected 23 runtime assemblies, found $($inventory.Count) inventory entries."
}

$duplicates = $inventory | Group-Object assembly | Where-Object Count -gt 1
if ($duplicates) {
    throw "Duplicate assembly inventory entries: $($duplicates.Name -join ', ')"
}

$pluginRoots = @($thirdPartyRoot, $voxtaRoot)
$expectedAssemblies = @($inventory.assembly | Sort-Object)
$actualAssemblies = @(
    foreach ($pluginRoot in $pluginRoots) {
        Get-ChildItem -LiteralPath $pluginRoot -File -Filter '*.dll' | Select-Object -ExpandProperty Name
    }
) | Sort-Object
if (Compare-Object $expectedAssemblies $actualAssemblies) {
    throw 'Vendored DLL filenames do not exactly match their inventories.'
}

if (-not (Test-Path -LiteralPath $noticesPath)) {
    throw "Missing third-party notices: $noticesPath"
}
if ((Get-Content -Raw -LiteralPath $noticesPath) -notmatch 'Voxta\.Model\.dll') {
    throw 'Third-party notices must state the first-party licensing of Voxta.Model.dll.'
}
$notices = Get-Content -Raw -LiteralPath $noticesPath
$unlisted = @($thirdPartyInventory | Where-Object { $notices -notmatch "(?m)^  - $([regex]::Escape($_.packageId))?$" } | ForEach-Object packageId)
if ($unlisted) {
    throw "Third-party notices do not list: $($unlisted -join ', ')"
}
if ($voxtaInventory[0].license -ne 'first-party:Unity Asset Store EULA') {
    throw "Voxta.Model inventory license must be 'first-party:Unity Asset Store EULA', found '$($voxtaInventory[0].license)'."
}

foreach ($assembly in $unity65SuppliedAssemblies) {
    $metaPath = Join-Path $thirdPartyRoot "$assembly.meta"
    if ((Get-Content -Raw -LiteralPath $metaPath) -notmatch "(?m)^  defineConstraints:?
  - '!UNITY_6000_5_OR_NEWER'") {
        throw "$assembly must be constrained to '!UNITY_6000_5_OR_NEWER'; Unity 6.5+ supplies its own copy."
    }
}

$lock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json
$lockDependencies = $lock.dependencies.'.NETStandard,Version=v2.1'

foreach ($entry in $inventory) {
    $assemblyPath = Join-Path $entry.pluginRoot $entry.assembly
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version
    if (($entry.assembly -eq 'System.Text.Json.dll' -or $entry.assembly -like 'Microsoft.AspNetCore.SignalR.*.dll') -and $assemblyVersion.Major -ne 8) {
        throw "Expected an 8.x $($entry.assembly) assembly to match Unity 6.5+'s System.Text.Json 8, found $assemblyVersion."
    }
    $assemblyHash = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA512).Hash
    if ($assemblyHash -ne $entry.assemblySha512) {
        throw "Assembly hash mismatch for $($entry.assembly)."
    }

    $lockEntry = $lockDependencies.($entry.packageId)
    if ($null -eq $lockEntry -or $lockEntry.resolved -ne $entry.version -or $lockEntry.contentHash -ne $entry.packageSha512) {
        throw "Restore lock data does not match inventory for $($entry.packageId) $($entry.version)."
    }

    if ($VerifyPackageAssets) {
        $packageId = $entry.packageId.ToLowerInvariant()
        $nupkgPath = Join-Path $PackageRoot "$packageId/$($entry.version)/$packageId.$($entry.version).nupkg"
        if (-not (Test-Path -LiteralPath $nupkgPath)) {
            throw "Missing locked package artifact: $nupkgPath"
        }

        $metadataPath = Join-Path $PackageRoot "$packageId/$($entry.version)/.nupkg.metadata"
        if (-not (Test-Path -LiteralPath $metadataPath)) {
            throw "Missing NuGet package metadata: $metadataPath"
        }
        $packageMetadata = Get-Content -Raw -LiteralPath $metadataPath | ConvertFrom-Json
        if ($packageMetadata.contentHash -ne $entry.packageSha512) {
            throw "NuGet content hash mismatch for $($entry.packageId) $($entry.version)."
        }

        $archive = [IO.Compression.ZipFile]::OpenRead($nupkgPath)
        try {
            $asset = $archive.Entries | Where-Object FullName -eq $entry.asset
            if ($null -eq $asset) { throw "Package $($entry.packageId) does not contain $($entry.asset)." }
            $stream = $asset.Open()
            try {
                $memory = [IO.MemoryStream]::new()
                try {
                    $stream.CopyTo($memory)
                    $assetSha512 = [Security.Cryptography.SHA512]::Create()
                    try { $assetHash = ($assetSha512.ComputeHash($memory.ToArray()) | ForEach-Object ToString x2) -join '' }
                    finally { $assetSha512.Dispose() }
                }
                finally { $memory.Dispose() }
            }
            finally { $stream.Dispose() }
        }
        finally { $archive.Dispose() }

        if ($assetHash -ne $entry.assemblySha512) { throw "Package asset hash mismatch for $($entry.packageId): $($entry.asset)" }
    }
}

Write-Host "Validated $($inventory.Count) vendored runtime assemblies and their locked restore metadata."
if ($VerifyPackageAssets) { Write-Host 'Validated the corresponding NuGet package and selected asset SHA-512 hashes.' }