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

function Require-FileCount([string]$name, [string]$relativePath, [string]$filter, [int]$expected) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) {
        $failures.Add($name)
        return
    }

    $count = (Get-ChildItem -LiteralPath $path -File -Filter $filter | Measure-Object).Count
    if ($count -ne $expected) {
        $failures.Add("$name (expected $expected, got $count)")
    }
}

$patchPath = 'src\Patches\ShinGetterCardFramePatch.cs'
$shaderPath = 'shaders\shin_getter_hsv.gdshader'
$materialPath = 'materials\cards\frames\card_frame_shin_getter_mat.tres'
$scenePath = 'scenes\creature_visuals\shin_getter.tscn'
$getterTwoFramesPath = 'scenes\creature_visuals\shin_getter_two_idle_frames.tres'
$stateMachinePath = 'src\Nodes\Combat\NShinGetterSpriteAnimationStateMachine.cs'

Require-Pattern 'Root manifest version is v0.9.21+' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(2[1-9]|[3-9][0-9])"'

Require-Pattern 'Frame shader keeps HSV route' $shaderPath 'uniform float h: hint_range[\s\S]*uniform float s: hint_range[\s\S]*uniform float v'
Require-Pattern 'Frame shader exposes narrow border color overlay' $shaderPath 'uniform vec4 border_color'
Require-Pattern 'Frame shader exposes border tint strength' $shaderPath 'uniform float border_tint_strength'
Require-Pattern 'Frame shader applies border overlay after HSV transform' $shaderPath 'edge_mask[\s\S]*mix\(col\.rgb,\s*border_color\.rgb,\s*edge_mask\s*\*\s*border_tint_strength\)'
Require-AbsentPattern 'Frame shader still avoids text-panel/base split' $shaderPath 'text_panel_mask|tint_base_color|tint_base_strength'

Require-Pattern 'Default material keeps getter-ray HSV parameters' $materialPath 'shader_parameter/h = 0\.455[\s\S]*shader_parameter/s = 1\.05[\s\S]*shader_parameter/v = 1\.16'
Require-Pattern 'Default material stores default getter-ray border color' $materialPath 'shader_parameter/border_color = Color\(0\.109804,\s*0\.752941,\s*0\.6,\s*1\)'
Require-Pattern 'Default material stores a narrow border tint strength' $materialPath 'shader_parameter/border_tint_strength = 0\.78'

Require-Pattern 'Frame patch HSV target also carries border color' $patchPath 'FrameHsvTarget\(string Key,\s*Color BorderColor,\s*float H,\s*float S,\s*float V'
Require-Pattern 'Getter One target border is requested red' $patchPath 'GetterOneRedKey[\s\S]*new Color\(GetterOneRedKey\)[\s\S]*0\.025f'
Require-Pattern 'Getter Two target border is requested silver' $patchPath 'GetterTwoSilverKey[\s\S]*new Color\(GetterTwoSilverKey\)[\s\S]*0\.0f'
Require-Pattern 'Getter Three target border is requested yellow' $patchPath 'GetterThreeYellowKey[\s\S]*new Color\(GetterThreeYellowKey\)[\s\S]*0\.14f'
Require-Pattern 'Default target border is getter-ray color' $patchPath 'DefaultGetterRayKey[\s\S]*new Color\(DefaultGetterRayKey\)[\s\S]*0\.455f'
Require-Pattern 'Frame patch writes h/s/v and border_color' $patchPath 'SetShaderParameter\("h"[\s\S]*SetShaderParameter\("s"[\s\S]*SetShaderParameter\("v"[\s\S]*SetShaderParameter\("border_color"'
Require-Pattern 'Frame patch writes border_tint_strength' $patchPath 'SetShaderParameter\("border_tint_strength"'

Require-FileCount 'Getter Two idle animation exports 24 png source frames' 'images\characters\shin_getter\forms\getter_two_idle' '*.png' 24
Require-Pattern 'Getter Two baked idle SpriteFrames resource exists' $getterTwoFramesPath '\[gd_resource type="SpriteFrames"'
Require-Pattern 'Getter Two baked idle SpriteFrames references first frame' $getterTwoFramesPath 'getter_two_idle/sprite_000001\.png'
Require-Pattern 'Getter Two baked idle SpriteFrames references last source frame' $getterTwoFramesPath 'getter_two_idle/sprite_000024\.png'
Require-Pattern 'Getter Two baked idle SpriteFrames loops idle' $getterTwoFramesPath '"loop": true[\s\S]*"name": &"idle"[\s\S]*"speed": 24\.0'
Require-Pattern 'Getter Two scene loads baked idle SpriteFrames resource' $scenePath 'shin_getter_two_idle_frames\.tres'
Require-Pattern 'Getter Two scene assigns sprite_frames' $scenePath 'GetterTwo[\s\S]*sprite_frames = ExtResource\("7_two_idle_frames"\)'
Require-Pattern 'Getter Two scene starts on idle animation' $scenePath 'GetterTwo[\s\S]*animation = &"idle"[\s\S]*autoplay = "idle"'
Require-Pattern 'Sprite loader rebuilds stale baked animations with too few frames' 'src\Nodes\Combat\NShinGetterSpriteSequence.cs' 'GetFrameCount\([\s\S]*expectedFrameCount[\s\S]*RemoveAnimation'
Require-Pattern 'Idle state machine resets to first frame before replaying idle' $stateMachinePath 'sprite\.Frame\s*=\s*0[\s\S]*sprite\.Play\(NShinGetterSpriteSequence\.IdleAnimationName\)'
Require-Pattern 'Idle state machine restores normal speed scale' $stateMachinePath 'sprite\.SpeedScale\s*=\s*1f'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.21 / border color and Getter Two idle checks failing:'
    $failures | Select-Object -First 140
    exit 1
}

Write-Host 'GREEN: v0.9.21 / border color and Getter Two idle checks passed.'
