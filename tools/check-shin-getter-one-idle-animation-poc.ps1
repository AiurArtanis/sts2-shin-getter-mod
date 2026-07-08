$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$relativePath) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) {
        return $null
    }

    return Get-Content -Raw -Encoding UTF8 -LiteralPath $path
}

function Require-Pattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text -or $text -notmatch $pattern) {
        $failures.Add($name)
    }
}

function Require-AbsentPattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-RepoFile $relativePath
    if ($null -ne $text -and $text -match $pattern) {
        $failures.Add($name)
    }
}

$frameDir = Join-Path $root 'images\characters\shin_getter\forms\getter_one_idle'
$frameCount = 0
$importCount = 0
$staleFrameArtifacts = @()
if (Test-Path -LiteralPath $frameDir) {
    $frameCount = @(Get-ChildItem -LiteralPath $frameDir -File -Filter 'sprite_*.png').Count
    $importCount = @(Get-ChildItem -LiteralPath $frameDir -File -Filter 'sprite_*.png.import').Count
    $staleFrameArtifacts = @(Get-ChildItem -LiteralPath $frameDir -File | Where-Object {
        $_.Name -match '^sprite_(\d{6})\.png(\.import)?$' -and [int]$Matches[1] -gt 24
    })
}

if ($frameCount -ne 24) {
    $failures.Add("Getter One idle frame count is exactly 24 (found $frameCount)")
}

if ($importCount -ne 24) {
    $failures.Add("Getter One idle import count is exactly 24 (found $importCount)")
}

if ($staleFrameArtifacts.Count -gt 0) {
    $failures.Add("Getter One idle has no frame/import artifacts after sprite_000024 (found $($staleFrameArtifacts.Count))")
}

Require-Pattern 'GetterOne is AnimatedSprite2D in creature scene' 'scenes\creature_visuals\shin_getter.tscn' '\[node name="GetterOne" type="AnimatedSprite2D"'
Require-Pattern 'GetterOne animation path points at imported idle frames' 'scenes\creature_visuals\shin_getter.tscn' 'getter_one_idle'
Require-AbsentPattern 'Creature scene does not directly attach idle loader C# script' 'scenes\creature_visuals\shin_getter.tscn' 'NShinGetterSpriteSequence\.cs'
Require-Pattern 'Runtime loader creates SpriteFrames' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'new\s+SpriteFrames\(\)'
Require-Pattern 'Runtime loader sorts frames by file name' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'OrderBy\([^)]*Path\.GetFileName'
Require-Pattern 'Runtime loader starts idle animation' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'Play\(IdleAnimationName\)'
Require-Pattern 'Animation state machine can return to idle' 'src\Nodes\Combat\NShinGetterSpriteAnimationStateMachine.cs' 'PlayIdle'
Require-Pattern 'Runtime loader caps source frames at 24' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'MaxFrames\s*=\s*24'
Require-Pattern 'Runtime loader builds ping-pong frames' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'AddPingPongFrames'
Require-Pattern 'Runtime loader appends reverse frames' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'index\s*=\s*textures\.Length\s*-\s*1[\s\S]*index\s*>=\s*0[\s\S]*frames\.AddFrame'
Require-Pattern 'Static visual helper configures GetterOne animation' 'src\Nodes\Combat\NShinGetterStaticVisuals.cs' 'NShinGetterSpriteSequence\.EnsureLoaded'
Require-Pattern 'GetterOne keeps POC frame cap metadata' 'scenes\creature_visuals\shin_getter.tscn' 'metadata/max_frames\s*=\s*24'
Require-Pattern 'Static visuals uses CanvasItem form nodes' 'src\Nodes\Combat\NShinGetterStaticVisuals.cs' 'CanvasItem'
Require-Pattern 'Static visuals still animates scale through Node2D' 'src\Nodes\Combat\NShinGetterStaticVisuals.cs' 'Node2D'
Require-Pattern 'Resource validator checks first idle frame' 'tools\validate-mod-resources.gd' 'getter_one_idle/sprite_000001\.png'
Require-Pattern 'Resource validator checks last kept idle frame' 'tools\validate-mod-resources.gd' 'getter_one_idle/sprite_000024\.png'
Require-Pattern 'Resource validator rejects removed idle frames' 'tools\validate-mod-resources.gd' 'getter_one_idle/sprite_000025\.png'
Require-Pattern 'Resource validator forbids direct idle loader dependency' 'tools\validate-mod-resources.gd' 'NShinGetterSpriteSequence\.cs'

if ($failures.Count -gt 0) {
    Write-Host 'RED: Shin Getter One idle sprite animation POC checks failing:'
    $failures | Select-Object -First 80
    exit 1
}

Write-Host 'GREEN: Shin Getter One idle sprite animation POC checks passed.'
