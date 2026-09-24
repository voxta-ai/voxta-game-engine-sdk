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
$thirdPartyInventoryPath = Join-Path $thirdPartyRoot 'RUNTIME-MODEL-DEPENDENCY-INVENTORY.json'
$voxtaInventoryPath = Join-Path $voxtaRoot 'VOXTA-MODEL-INVENTORY.json'
$lockPath = Join-Path $repositoryRoot 'Tools/ModelDependencies/VoxtaModel/Probe/packages.lock.json'

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
if ($inventory.Count -ne 24) {
    throw "Expected 24 runtime assemblies, found $($inventory.Count) inventory entries."
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

foreach ($assemblyFile in @(foreach ($pluginRoot in $pluginRoots) { Get-ChildItem -LiteralPath $pluginRoot -File -Filter '*.dll' })) {
    $identity = [Reflection.AssemblyName]::GetAssemblyName($assemblyFile.FullName)
    if (($identity.Name -eq 'System.Text.Json' -or $identity.Name -like 'Microsoft.AspNetCore.SignalR.*') -and $identity.Version.Major -eq 8) {
        throw "SignalR 8 / System.Text.Json 8 assembly detected in the vendored closure: $($assemblyFile.Name) ($identity)."
    }
}

$lock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json
$lockDependencies = $lock.dependencies.'.NETStandard,Version=v2.1'

foreach ($entry in $inventory) {
    $assemblyPath = Join-Path $entry.pluginRoot $entry.assembly
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version
    if (($entry.assembly -eq 'System.Text.Json.dll' -or $entry.assembly -like 'Microsoft.AspNetCore.SignalR.*.dll') -and $assemblyVersion.Major -ne 10) {
        throw "Expected a 10.x $($entry.assembly) assembly, found $assemblyVersion."
    }
    $assemblyHash = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA512).Hash
    if ($assemblyHash -ne $entry.assemblySha512) {
        throw "Assembly hash mismatch for $($entry.assembly)."
    }

    $lockEntry = $lockDependencies.($entry.packageId)
    if ($null -eq $lockEntry -or $lockEntry.resolved -ne $entry.version -or $lockEntry.contentHash -ne $entry.packageSha512) {
        throw "Restore lock data does not match inventory for $($entry.packageId) $($entry.version)."
    }

    $licenseName = "org.nuget.$($entry.packageId.ToLowerInvariant())-License.md"
    if (-not (Test-Path -LiteralPath (Join-Path $entry.pluginRoot "licenses/$licenseName"))) {
        throw "Missing license notice for $($entry.packageId): $licenseName"
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