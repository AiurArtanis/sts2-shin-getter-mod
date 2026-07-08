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
$gameLoadLog = Join-Path $buildDirectory "godot-game-load.log"
$gameProjectFile = Join-Path $GameProject "project.godot"
$godotMonoBuildLogs = Join-Path $projectRoot "Godot\mono\build_logs"

function Invoke-NativeToLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$LogPath,
        [int]$TimeoutSeconds = 0
    )

    $argumentLine = ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join ' '

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = $argumentLine
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if ($TimeoutSeconds -gt 0) {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            $stdout = $stdoutTask.GetAwaiter().GetResult()
            $stderr = $stderrTask.GetAwaiter().GetResult()
            [System.IO.File]::WriteAllText($LogPath, $stdout + $stderr, [System.Text.Encoding]::UTF8)
            return 124
        }
    }
    else {
        $process.WaitForExit()
    }

    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    [System.IO.File]::WriteAllText($LogPath, $stdout + $stderr, [System.Text.Encoding]::UTF8)

    return $process.ExitCode
    }

function Copy-DeployArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArtifactPath,
        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory
    )

    if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) {
        throw "Deploy artifact not found: $ArtifactPath"
    }

    $artifactName = Split-Path -Leaf $ArtifactPath
    $destination = Join-Path $TargetDirectory $artifactName
    try {
        Copy-Item -LiteralPath $ArtifactPath -Destination $destination -Force -ErrorAction Stop
    }
    catch {
        throw "Deploy failed while copying '$artifactName' to '$TargetDirectory'. The target file may be locked by the running game/Godot (可能被游戏占用), or the directory may be unavailable. Close the game and retry. Original error: $($_.Exception.Message)"
    }
}

function Reset-GodotMonoBuildLogs {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($projectRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Godot\mono\build_logs"))
    if ($resolvedPath -ne $expectedPath -or -not $resolvedPath.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected Godot mono build log path: $resolvedPath"
    }

    if (Test-Path -LiteralPath $resolvedPath) {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $resolvedPath | Out-Null
    Set-Content -LiteralPath (Join-Path $resolvedPath ".gdignore") -Value "" -Encoding ASCII
}

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
Reset-GodotMonoBuildLogs -Path $godotMonoBuildLogs

try {
    Write-Host "Exporting PCK with Godot..."
    $godotExitCode = Invoke-NativeToLog `
        -FilePath $GodotExe `
        -Arguments @("--headless", "--quit", "--path", $projectRoot, "--export-pack", "Windows Desktop", $temporaryPck) `
        -LogPath $exportLog `
        -TimeoutSeconds 180
    $godotExportTimedOut = $godotExitCode -eq 124
    if ($godotExitCode -ne 0 -and -not $godotExportTimedOut) {
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

    if ($godotExportTimedOut) {
        Write-Warning "Godot export process did not exit within 180 seconds; continuing with generated PCK after terminating the headless process. Resource validation still runs next."
    }

    Move-Item -LiteralPath $temporaryPck -Destination $buildPck -Force
}
finally {
    if (Test-Path -LiteralPath $stagedDll -PathType Leaf) {
        Move-Item -LiteralPath $stagedDll -Destination $buildDll -Force
    }
}

Write-Host "Validating required resources in exported PCK..."
$validationExitCode = Invoke-NativeToLog `
    -FilePath $GodotExe `
    -Arguments @("--headless", "--path", $GameProject, "--script", $validationScript, "--", $buildPck) `
    -LogPath $validationLog
if ($validationExitCode -ne 0) {
    Get-Content -LiteralPath $validationLog -Tail 40
    throw "PCK resource validation failed with exit code $validationExitCode."
}

if (Select-String -LiteralPath $validationLog -SimpleMatch "Cannot instantiate C# script" -Quiet) {
    Get-Content -LiteralPath $validationLog -Tail 80
    throw "PCK resource validation found a C# script instantiation error."
}

Copy-Item -LiteralPath $manifest -Destination $buildManifest -Force

foreach ($artifact in @($buildDll, $buildPck, $buildManifest)) {
    Copy-DeployArtifact -ArtifactPath $artifact -TargetDirectory $DeployDirectory
}

Write-Host "Validating mod load in game project..."
$gameLoadExitCode = Invoke-NativeToLog `
    -FilePath $GodotExe `
    -Arguments @("--headless", "--quit", "--path", $GameProject) `
    -LogPath $gameLoadLog
if ($gameLoadExitCode -ne 0) {
    Get-Content -LiteralPath $gameLoadLog -Tail 80
    throw "Game project mod-load validation failed with exit code $gameLoadExitCode."
}

$modInitializationLine = Select-String -LiteralPath $gameLoadLog -SimpleMatch "Finished mod initialization" |
    Where-Object { $_.Line -like "*ShinGetterMod*" } |
    Select-Object -First 1
if (-not $modInitializationLine) {
    Get-Content -LiteralPath $gameLoadLog -Tail 80
    throw "Game project mod-load validation did not confirm ShinGetterMod initialization."
}

if (Select-String -LiteralPath $gameLoadLog -SimpleMatch "Cannot instantiate C# script" -Quiet) {
    Get-Content -LiteralPath $gameLoadLog -Tail 80
    throw "Game project mod-load validation found a C# script instantiation error."
}

Write-Host "Deployment complete: $DeployDirectory"
Get-Item -LiteralPath @(
    (Join-Path $DeployDirectory "ShinGetterMod.dll"),
    (Join-Path $DeployDirectory "ShinGetterMod.pck"),
    (Join-Path $DeployDirectory "ShinGetterMod.json")
) | Select-Object Name, Length, LastWriteTime
