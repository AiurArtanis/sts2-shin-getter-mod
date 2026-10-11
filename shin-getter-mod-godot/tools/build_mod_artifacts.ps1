param(
    [Parameter(Mandatory = $true)][string]$GodotPath,
    [string]$OutputDirectory,
    [string]$SourceRevisionId
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$godot = (Resolve-Path -LiteralPath $GodotPath).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $project 'build' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$relativeOutput = [IO.Path]::GetRelativePath($project, $output)
if (-not $relativeOutput.StartsWith('..') -and -not [IO.Path]::IsPathRooted($relativeOutput) -and
    ($relativeOutput -split '[\\/]')[0] -ne 'build') {
    throw 'Output must be outside the project or under its excluded build/ directory'
}
New-Item -ItemType Directory -Force -Path $output | Out-Null
foreach ($dependency in @('sts2.dll', '0Harmony.dll')) {
    if (-not (Test-Path -LiteralPath "$project/lib/$dependency")) {
        throw "Missing lib/$dependency; provide the intended game-line reference first"
    }
}

function Invoke-GodotChecked([string[]]$Arguments, [string]$Log) {
    & $godot @Arguments *> $Log
    if ($LASTEXITCODE -ne 0 -or (Select-String -LiteralPath $Log -Pattern '^SCRIPT ERROR:|^ERROR:' -Quiet)) {
        throw "Godot failed; inspect $Log"
    }
}

Invoke-GodotChecked @('--headless', '--editor', '--path', $project, '--import', '--quit') "$output/import.log"
$buildArgs = @('build', "$project/ShinGetterMod.csproj", '--nologo', '-v:minimal')
if ($SourceRevisionId) { $buildArgs += "-p:SourceRevisionId=$SourceRevisionId" }
& dotnet @buildArgs *> "$output/build.log"
if ($LASTEXITCODE -ne 0) { throw "Build failed; inspect $output/build.log" }
if ($output.TrimEnd('\', '/') -ne ([IO.Path]::GetFullPath("$project/build")).TrimEnd('\', '/')) {
    Copy-Item -LiteralPath "$project/build/ShinGetterMod.dll" -Destination "$output/ShinGetterMod.dll"
}
Copy-Item -LiteralPath "$project/ShinGetterMod.json" -Destination "$output/ShinGetterMod.json"
Invoke-GodotChecked @('--headless', '--path', $project, '--script', "$PSScriptRoot/build_mod_pck.gd", '--',
    "$output/ShinGetterMod.pck", "$output/pck-report.json") "$output/pack.log"

# Do not let the verifier fall back to source media in the imported project.
$validationRoot = Join-Path ([IO.Path]::GetTempPath()) ('shin-getter-pck-check-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $validationRoot | Out-Null
    'config_version=5' | Set-Content -LiteralPath "$validationRoot/project.godot" -Encoding ascii
    Invoke-GodotChecked @('--headless', '--path', $validationRoot, '--script', "$PSScriptRoot/validate_mod_pck.gd", '--',
        "$output/ShinGetterMod.pck", "$output/pck-report.json") "$output/pck-validation.log"
} finally {
    $resolved = [IO.Path]::GetFullPath($validationRoot)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ($resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolved).StartsWith('shin-getter-pck-check-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Get-Content -LiteralPath "$output/build.log" -Tail 8
Get-Content -LiteralPath "$output/pck-validation.log" -Tail 2
Get-Item -LiteralPath "$output/ShinGetterMod.dll", "$output/ShinGetterMod.pck", "$output/ShinGetterMod.json" |
    ForEach-Object { [pscustomobject]@{ Name = $_.Name; Bytes = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName).Hash } }
