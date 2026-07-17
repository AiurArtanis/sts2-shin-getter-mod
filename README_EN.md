# Shin Getter Mod

[简体中文](README.md) | [日本語](README_JP.md)

![Shin Getter Mod character-select screen](animations/character_select/shin_getter/character_select_shin_getter_bg.png)

**Shin Getter has crossed space and time to climb the Spire.** This gameplay character mod for *Slay the Spire 2* brings four Getter forms, mid-combat transformations, and several interlocking deck archetypes together to recreate Shin Getter's escalating combat rhythm.

> Current version `v0.9.42` · Minimum game version `0.106.1` · Godot `4.5.1 Mono` · .NET `9` · 简体中文 / English / 日本語

## Why play it?

- **Four forms, one character.** Switch among Getter-1, Getter-2, Getter-3, and Shin Getter Dragon, making form choice part of each turn's decisions.
- **More than one route to a winning deck.** Transformation, Morale, Vitality, Regeneration, Plating, Evolution, and Radiation can anchor separate archetypes or combine across them.
- **Built around Shin Getter from cards to presentation.** The mod includes custom card frames, character animation, VFX, voices, finishing music, and a dedicated character-select scene.
- **Integrated into the Spire instead of being a simple reskin.** Alongside the full card pool are exclusive relics, potions, enchantments, an event, and Ancient dialogue.

## What's included

The content currently registered in `v0.9.42` includes:

- **72 cards** spanning four forms and several core mechanics
- **10 relics**, **3 potions**, and **2 enchantments**
- **1 exclusive event** plus dedicated Ancient dialogue
- Simplified Chinese, English, and Japanese localization
- A complete character mod loaded through DLL, PCK, and JSON artifacts

## Featured cards

<p align="center">
  <img src="images/packed/card_single/shin_getter/s_g_c_shin_form_card.png" width="30%" alt="Finished artwork for the Shin Form card" />
  <img src="images/packed/card_single/shin_getter/s_g_c_stoner_sunshine_card.png" width="30%" alt="Finished artwork for the Stoner Sunshine card" />
  <img src="images/packed/card_single/shin_getter/s_g_c_saint_dragon_roar_card.png" width="30%" alt="Finished artwork for the Saint Dragon Roar card" />
</p>

## Project status

This project is still in **active pre-release development**. Version numbers, balance, content counts, and compatibility with future game builds may change. There is no stable installation package or save-compatibility guarantee yet. Developers who want to test the mod or study its implementation can build it from source.

## Build from source

### Requirements

- *Slay the Spire 2* `0.106.1` or later
- Godot `4.5.1 Mono`
- .NET SDK `9`
- A local game project directory that Godot can load for validation

The following dependencies come from a local game or development environment and are not tracked by this repository:

- `lib/sts2.dll`
- `lib/0Harmony.dll`
- Game- or editor-provided plugins under `addons/`, including FMOD, Spine, and Sentry

After restoring those dependencies, build the C# project:

```powershell
dotnet build .\ShinGetterMod.csproj -c Debug
```

The compiled DLL and PDB are copied into the ignored `build/` directory.

### Export a test package

Export the PCK with Godot's `Windows Desktop` preset:

```powershell
godot --headless --quit --path . --export-pack "Windows Desktop" .\build\ShinGetterMod.pck
```

Place these files together in your local game's Mod directory:

- `ShinGetterMod.dll`
- `ShinGetterMod.pck`
- `ShinGetterMod.json`

Godot resource changes should also run `tools/validate-mod-resources.gd` from the local game project and pass one headless load check. Personal deployment scripts and machine-specific paths are not tracked by this repository.

## Repository layout

- `src/`: cards, powers, relics, potions, enchantments, events, patches, and runtime code
- `scenes/`, `animations/`, `images/`, `materials/`, `shaders/`, `audio/`: Godot scenes and audiovisual assets
- `ShinGetterMod/`: mod data and localization resources
- `tools/validate-mod-resources.gd`: exported-resource integrity check
- `ShinGetterMod.json`: mod manifest, version, and minimum game version

## Contributing and reporting issues

Before the public release, the repository remains focused on coordinated development and testing. When reporting a problem, include the game version, mod version, reproduction steps, and relevant logs whenever possible. Keep code contributions focused and run at least the relevant C# build. Changes to Godot resources should also pass the full build, resource-validation, and load-validation pipeline.

Do not commit local game dependencies, `addons/`, `build/` artifacts, or personal test scripts.

## License and asset notice

This is an unofficial fan-made mod. Names, characters, and source material related to *Slay the Spire 2* and Shin Getter remain the property of their respective rights holders.

This repository does not currently include an open-source license. The code license, third-party asset attribution, and usage boundaries will be documented before redistribution and public contribution are opened. Until then, do not assume that the repository's contents are licensed for copying, modification, or redistribution.
