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
- After build, resource validation, and game-load validation succeed, copy the same four files to `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ShinGetterMod` and verify their sizes and SHA-256 hashes against the release inputs.
- Example: manifest version `v1.0.3` uses `shin-getter-mod-v1.0.3.zip`.

## Protected Branch Integration

- When a protected target branch rejects a direct push, create a pull request and complete the allowed merge method without waiting for separate user authorization.
- If a pull request has only simple conflicts whose intended resolution is unambiguous from the ticket, reviewed changes, and target branch, resolve them, rerun the required validation, and merge the pull request.
- If conflicts are complex, affect behavior outside the reviewed scope, or leave the intended result uncertain, stop before resolving or merging and ask Artanis for a decision.
- After merging, fetch the remote target again and verify the final target hash, merged content, ancestry where applicable, and worktree status.
