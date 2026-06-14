[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$GodotExe = "E:\Work\Godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe",
    [string]$GameProject = "E:\Work\SlaytheSpare2",
    [string]$DeployDirectory = "E:\Work\Godot\Godot_v4.5.1-stable_mono_win64\mods\ShinGetterMod"
)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot "ShinGetterMod.csproj"
$buildDirectory = Join-Path $projectRoot "build"
$buildDll = Join-Path $buildDirectory "ShinGetterMod.dll"
$stagedDll = Join-Path $buildDirectory "ShinGetterMod.compiled.dll"
$buildPck = Join-Path $buildDirectory "ShinGetterMod.pck"
$buildManifest = Join-Path $buildDirectory "ShinGetterMod.json"
$temporaryPck = Join-Path $buildDirectory "ShinGetterMod.export.pck"
$manifest = Join-Path $projectRoot "ShinGetterMod.json"
$exportLog = Join-Path $buildDirectory "godot-export.log"
$validationScript = Join-Path $projectRoot "tools\validate-mod-resources.gd"
$validationLog = Join-Path $buildDirectory "godot-validation.log"
$gameProjectFile = Join-Path $GameProject "project.godot"

foreach ($requiredPath in @($GodotExe, $projectFile, $manifest, $validationScript, $gameProjectFile)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required file not found: $requiredPath"
    }
}

New-Item -ItemType Directory -Force -Path $buildDirectory, $DeployDirectory | Out-Null

Write-Host "Building ShinGetterMod ($Configuration)..."
dotnet build $projectFile -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $buildDll -PathType Leaf)) {
    throw "Compiled DLL not found: $buildDll"
}

Copy-Item -LiteralPath $buildDll -Destination $stagedDll -Force
Remove-Item -LiteralPath $temporaryPck -Force -ErrorAction SilentlyContinue

try {
    Write-Host "Exporting PCK with Godot..."
    & $GodotExe --headless --quit --path $projectRoot --export-pack "Windows Desktop" $temporaryPck *> $exportLog
    $godotExitCode = $LASTEXITCODE
    if ($godotExitCode -ne 0) {
        Get-Content -LiteralPath $exportLog -Tail 40
        throw "Godot export failed with exit code $godotExitCode."
    }

    if (-not (Test-Path -LiteralPath $temporaryPck -PathType Leaf)) {
        throw "Godot exited successfully but did not create: $temporaryPck"
    }

    $pckInfo = Get-Item -LiteralPath $temporaryPck
    if ($pckInfo.Length -eq 0) {
        throw "Godot created an empty PCK: $temporaryPck"
    }

    Move-Item -LiteralPath $temporaryPck -Destination $buildPck -Force
}
finally {
    if (Test-Path -LiteralPath $stagedDll -PathType Leaf) {
        Move-Item -LiteralPath $stagedDll -Destination $buildDll -Force
    }
}

Write-Host "Validating required resources in exported PCK..."
& $GodotExe --headless --path $GameProject --script $validationScript -- $buildPck *> $validationLog
$validationExitCode = $LASTEXITCODE
if ($validationExitCode -ne 0) {
    Get-Content -LiteralPath $validationLog -Tail 40
    throw "PCK resource validation failed with exit code $validationExitCode."
}

Copy-Item -LiteralPath $manifest -Destination $buildManifest -Force

foreach ($artifact in @($buildDll, $buildPck, $buildManifest)) {
    Copy-Item -LiteralPath $artifact -Destination $DeployDirectory -Force
}

Write-Host "Deployment complete: $DeployDirectory"
Get-Item -LiteralPath @(
    (Join-Path $DeployDirectory "ShinGetterMod.dll"),
    (Join-Path $DeployDirectory "ShinGetterMod.pck"),
    (Join-Path $DeployDirectory "ShinGetterMod.json")
) | Select-Object Name, Length, LastWriteTime
