# Project Working Agreements

## Release Version Naming

- Starting with the version after `v1.0.0`, name the release tag branch `mod-<version>`.
- `<version>` must exactly match the `version` value in `shin-getter-mod-godot/ShinGetterMod.json`.
- Keep the version prefix `v` lowercase.
- Example: JSON version `v1.0.1` uses `mod-v1.0.1`.

## Release Packaging

- Keep the canonical mod image at the repository root as `mod_image.png`.
- Publish a ZIP named `shin-getter-mod-<version>.zip`, where `<version>` exactly matches the manifest version, including the lowercase `v` prefix.
- Place `mod_image.png`, `ShinGetterMod.dll`, `ShinGetterMod.pck`, and `ShinGetterMod.json` at the ZIP root.
- Example: manifest version `v1.0.3` uses `shin-getter-mod-v1.0.3.zip`.
