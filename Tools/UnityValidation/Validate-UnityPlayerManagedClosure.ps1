[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PlayerPath,
    [string]$RepositoryRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PlayerPath -PathType Leaf)) { throw "Player executable was not found: $PlayerPath" }
$playerDirectory = Split-Path -Parent $PlayerPath
$playerName = [IO.Path]::GetFileNameWithoutExtension($PlayerPath)
$dataDirectory = Join-Path $playerDirectory "${playerName}_Data"
if (-not (Test-Path -LiteralPath $dataDirectory -PathType Container)) { throw "Player data directory was not found: $dataDirectory" }

$inventoryPaths = @(
    (Join-Path $RepositoryRoot 'unity/Runtime/Plugins/ThirdParty/RUNTIME-MODEL-DEPENDENCY-INVENTORY.json'),
    (Join-Path $RepositoryRoot 'unity/Runtime/Plugins/Voxta/VOXTA-MODEL-INVENTORY.json')
)
foreach ($inventoryPath in $inventoryPaths) {
    if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) { throw "Runtime dependency inventory was not found: $inventoryPath" }
}
$inventory = @($inventoryPaths | ForEach-Object { Get-Content -Raw -LiteralPath $_ | ConvertFrom-Json | ForEach-Object { $_ } })
$requiredAssemblies = @('System.Text.Json.dll', 'Voxta.Model.dll', $inventory.assembly | Where-Object { $_ -like 'Microsoft.AspNetCore.SignalR.*.dll' })
$playerAssemblies = @(Get-ChildItem -LiteralPath $dataDirectory -Recurse -File -Filter '*.dll')
foreach ($assemblyName in $requiredAssemblies) {
    $matches = @($playerAssemblies | Where-Object Name -eq $assemblyName)
    if ($matches.Count -ne 1) { throw "Expected exactly one $assemblyName in the player output, found $($matches.Count)." }
    $expected = @($inventory | Where-Object { $_.assembly -eq $assemblyName })
    if ($expected.Count -ne 1) { throw "Inventory does not uniquely identify $assemblyName." }
    $actualHash = (Get-FileHash -LiteralPath $matches[0].FullName -Algorithm SHA512).Hash
    if ($actualHash -ne $expected[0].assemblySha512) { throw "Player output hash mismatch for $assemblyName." }
}
Write-Host "Validated the pinned SignalR, System.Text.Json, and Voxta.Model managed closure in $PlayerPath."