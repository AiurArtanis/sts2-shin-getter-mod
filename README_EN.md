# Shin Getter Mod

[简体中文](README.md) | [日本語](README_JP.md)

![Shin Getter Mod character-select screen](animations/character_select/shin_getter/character_select_shin_getter_bg.png)

**Three hearts. One Getter. Endless evolution.**

Shin Getter crosses space and time to climb the Spire. Shift between Getter-1's explosive offense, Getter-2's high-speed tactics, and Getter-3's iron defense; then awaken Shin Getter Dragon and let the Getter Rays consume the tower. This is a gameplay character mod built around transformations, deckbuilding, and presentation, not a simple reskin.

> Current version `v0.9.41` · Minimum game version `0.106.1` · Godot `4.5.1 Mono` · .NET `9` · 简体中文 / English / 日本語

## ⚡ Combat at a glance

- **Four forms, one character.** Getter-1 focuses on Vitality bursts; Getter-2 gains extra Energy and draw while its Block is halved; Getter-3 holds the line with Plating and defensive retaliation; Shin Getter Dragon accepts rewards from all three forms.
- **Transformation is a decision.** Form-specific cards highlight the strong actions available now. Movement, transformation cards, and related relics let you make shifting part of an attacking turn.
- **Built around Shin Getter from cards to presentation.** Custom card frames, character animation, VFX, voices, finishing music, and a dedicated character-select scene form one experience.
- **Integrated into the Spire instead of being a reskin.** Alongside the full card pool are exclusive relics, potions, enchantments, an event, and Ancient dialogue.

## 🧭 Starter routes

- **Vitality burst:** Build Vitality, then finish with cards such as *Hot Blood* and *Dive Strike*.
- **Exhaust loop:** Use exhausting cards to trigger *Getter Claw*'s free damage. *Parts Swap* recovers key pieces, while Getter-2 can further amplify the payoff.
- **Iron retaliation:** Stack Plating and Block in Getter-3, then turn the enemy turn into a chance to counterattack.
- **Transformation chain:** Build around frequent shifting, movement-triggered transformations, and *Chosen One* so each change becomes resources or defense.

Morale supports several high-impact effects. Evolution and Radiation create additional late-game paths. Choose a primary engine first, then let the remaining mechanics support it.

## 📦 What's included

The content currently registered in `v0.9.41` includes:

- **72 cards** spanning four forms and several core mechanics
- **10 relics**, **3 potions**, and **2 enchantments**
- **1 exclusive event** plus dedicated Ancient dialogue
- Simplified Chinese, English, and Japanese localization
- A complete character mod loaded through DLL, PCK, and JSON artifacts

## 🃏 Featured cards

<p align="center">
  <img src="images/packed/card_single/shin_getter/s_g_c_shin_form_card.png" width="30%" alt="Finished artwork for the Shin Form card" />
  <img src="images/packed/card_single/shin_getter/s_g_c_stoner_sunshine_card.png" width="30%" alt="Finished artwork for the Stoner Sunshine card" />
  <img src="images/packed/card_single/shin_getter/s_g_c_saint_dragon_roar_card.png" width="30%" alt="Finished artwork for the Saint Dragon Roar card" />
</p>

## 🚀 Quick start

Once the Workshop edition is released: subscribe to the mod, enable it from the game's Mod menu, start a new run, and select **Shin Getter**. For a first run, follow the highlighted form-specific cards to establish a rhythm, then branch into one of the routes above.

The project is still in pre-release development. To help test it or study its implementation, build it from source below. Version numbers, balance, content counts, and compatibility with future game builds may change; no stable installation package or save-compatibility guarantee is offered yet.

## 🔭 What's next

- More events and enemies from the same universe
- A new \"Destiny\" deck archetype
- Continued animation and combat-presentation improvements

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

The built DLL and PDB are copied to the ignored `build/` directory.

### Build, validate, and deploy a test package

The bundled Godot/PowerShell pipeline compiles the DLL, exports the PCK, validates resources, copies the DLL/PCK/JSON artifacts, and runs a headless game-load validation:

```powershell
.\build-and-deploy.ps1 `
  -Configuration Debug `
  -GodotExe "D:\Godot\Godot_v4.5.1-stable_mono_win64_console.exe" `
  -GameProject "D:\Games\SlayTheSpire2" `
  -DeployDirectory "D:\Games\SlayTheSpire2\mods\ShinGetterMod"
```

Replace the paths with your local environment. On success, the deployment directory contains:

- `ShinGetterMod.dll`
- `ShinGetterMod.pck`
- `ShinGetterMod.json`

## Repository layout

- `src/`: cards, powers, relics, potions, enchantments, events, patches, and runtime code
- `scenes/`, `animations/`, `images/`, `materials/`, `shaders/`, `audio/`: Godot scenes and audiovisual assets
- `ShinGetterMod/`: mod data and localization resources
- `tools/validate-mod-resources.gd`: exported-package resource check
- `ShinGetterMod.json`: mod manifest, version, and minimum game version

## Contributing and reporting issues

Before the public release, the repository remains focused on coordinated development and testing. When reporting a problem, include the game version, mod version, reproduction steps, and relevant logs whenever possible. Keep code contributions focused and run at least the relevant C# build. Changes to Godot resources should also pass the full build, resource-validation, and load-validation pipeline.

Do not commit local game dependencies, `addons/`, `build/` artifacts, or personal test scripts.

## License and asset notice

This is an unofficial fan-made mod. Names, characters, and source material related to *Slay the Spire 2* and Shin Getter remain the property of their respective rights holders.

This repository does not currently include an open-source license. The code license, third-party asset attribution, and usage boundaries will be documented before redistribution and public contribution are opened. Until then, do not assume that the repository's contents are licensed for copying, modification, or redistribution.
