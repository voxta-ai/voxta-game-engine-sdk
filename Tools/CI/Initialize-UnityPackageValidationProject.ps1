[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$Revision,

    [string]$PackageGitUrl = 'https://github.com/voxta-ai/voxta-game-engine-sdk.git',

    [string]$BuildScriptPath = (Join-Path $PSScriptRoot 'UnityPackageValidationBuild.cs')
)

$ErrorActionPreference = 'Stop'

if (Test-Path -LiteralPath $ProjectPath) {
    throw "Package-validation project path already exists: $ProjectPath"
}

if (-not (Test-Path -LiteralPath $BuildScriptPath -PathType Leaf)) {
    throw "Unity build script was not found: $BuildScriptPath"
}

$assetsPath = Join-Path $ProjectPath 'Assets'
$packagesPath = Join-Path $ProjectPath 'Packages'
$projectSettingsPath = Join-Path $ProjectPath 'ProjectSettings'
$editorPath = Join-Path $assetsPath 'CI/Editor'

New-Item -ItemType Directory -Force $editorPath, $packagesPath, $projectSettingsPath | Out-Null

$manifest = @{
    dependencies = @{
        'com.unity.test-framework' = '1.1.33'
        # game-ci/unity-test-runner always enables coverage and combines its
        # results, so the clean consumer project must provide this package.
        'com.unity.testtools.codecoverage' = '1.2.6'
        'com.voxta.game-engine-sdk' = "${PackageGitUrl}?path=unity#${Revision}"
    }
    testables = @('com.voxta.game-engine-sdk')
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packagesPath 'manifest.json') -Encoding utf8
@"
m_EditorVersion: 2022.3.62f3
m_EditorVersionWithRevision: 2022.3.62f3 (96770f904ca7)
"@ | Set-Content -LiteralPath (Join-Path $projectSettingsPath 'ProjectVersion.txt') -Encoding utf8

Copy-Item -LiteralPath $BuildScriptPath -Destination (Join-Path $editorPath 'UnityPackageValidationBuild.cs')
