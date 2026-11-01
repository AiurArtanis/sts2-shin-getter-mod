# issue#238 v1.4.0 candidate art

These resources are review candidates. Artanis has not approved them. No shared asset library, Excel workbook, deployment, or release was modified.

The existing card-design chat produced five new illustrations (ART-001/002). The existing icon-management chat produced three relics and one potion (ART-003/004). Independent visual QA accepted the Symbiotic Filter and Mobility Catalyst, then requested revisions to the sapphire's four-beat structure and Ember's campfire-like shape. ART-005 redrew only those two items and passed a second independent review. The original rejected versions remain in the project archive.

`manifest.json` records selected files, source SHA-256 hashes, independent review receipts, and pending human approval. ART-003's acceptance applies only to Symbiotic Filter; its overall rejected status is retained in the receipt.

Cards use native per-model portrait paths and atlas entries. Full portraits are 1000×760 RGB/indexed PNG without alpha. Slots 78–82 contain 250×190 thumbnails. All other decoded pixels, including fixed Ancient slots 67/68/72/77, match main at `26c8da9a`.

Item icons use an independent 512×128 atlas and matching outline atlas, ordered Activated Sapphire, Symbiotic Filter, Ember, Mobility Catalyst. Each region is 128×128 with transparent borders. Relic large icons are 256×256. Existing shared icon atlases remain unchanged.

Offline checks performed:

- `python -X utf8 -B tools/validate_issue_238.py`: PASS — 15 events / 20 routes, save/rollback contracts, current Nov 1 text, and 32 legacy notes / 46 pages in three languages.
- `python -X utf8 -B tools/validate_issue_238_assets.py`: PASS — resource paths, SHA, dimensions, transparency, outline silhouettes, fixed-slot preservation.
- `python -X utf8 -B tools/validate_issue_89.py`: PASS — shared event presentation regression.
- Roslyn syntax parsing of 12 changed C# files: zero syntax errors. This was not a mod compilation.
- Authored JSON / color-tag balance / inline-payload scan: PASS.
- `git diff --check`: PASS.

DLL compilation, Godot import/resource loading, real gameplay, save/resume and chained-effect acceptance belong to the main development chat and remain pending. The manifest version stays at the current main version; release versioning is not performed by this feature branch.

This branch carries the previously unmerged issue#238 implementation as well as the Nov 1 continuation. Review against `origin/main...HEAD`; integrating only the final incremental commit would omit required earlier implementation.

Human review gallery: `游戏/杀戮尖塔2/开发工单/issue238-v1.4.0-美术审核清单.md` in the `all-in-one` Obsidian vault. The two redesigned events, Self-Help Book and Stone of Eternity, also await Artanis review.
