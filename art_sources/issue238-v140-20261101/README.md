# issue#238 v1.4.0 approved art integration

All nine selected artworks have Artanis's approval, completed on 2026-11-02 with Drill Missile ART-037. They are integrated only into the isolated feature branch. No shared asset library, Excel workbook, deployment, or release was modified.

Final cards: Tablet of Truth ART-007, Triple Cyclone ART-027, Drill Missile ART-037, Broken Drill ART-001 and Annotations ART-016. Final icons: Activated Sapphire ART-005, Symbiotic Filter ART-009, Ember ART-017 and Mobility Catalyst ART-008. Earlier candidates and rejected revisions remain in the external production archive.

`manifest.json` records the selected source/export hashes, human approval and review receipts. ART-009's acceptance applies only to the selected Symbiotic Filter; its original overall revision-requested verdict remains recorded. ART-037's visual preview approval and its later production export verification are separate. The previous ART-033 compression failure is preserved in the export archive; the approved ART-037 source passed all three standard quality bands, with 75–80 selected.

Cards use native per-model portrait paths and atlas entries. Full portraits are 1000×760 indexed PNG without alpha or tRNS. Slots 78–82 contain 250×190 thumbnails. All other decoded pixels, including fixed Ancient slots 67/68/72/77, match main at `26c8da9a`. Four approved indexed portraits were copied losslessly; only Drill Missile's approved RGB portrait required quantization. Thumbnails were derived from the selected production portraits.

Item icons use an independent 512×128 atlas and matching outline atlas, ordered Activated Sapphire, Symbiotic Filter, Ember, Mobility Catalyst. Each region is 128×128 with transparent borders. Relic large icons are 256×256. Existing shared icon atlases remain unchanged.

Offline checks performed:

- `python -X utf8 -B tools/validate_issue_238.py`: PASS — 15 events / 20 routes, save/rollback contracts, current Nov 1 text, and 32 legacy notes / 46 pages in three languages.
- `python -X utf8 -B tools/validate_issue_238_assets.py`: PASS — resource paths, SHA, dimensions, transparency, outline silhouettes, fixed-slot preservation.
- `python -X utf8 -B tools/validate_issue_89.py`: PASS — shared event presentation regression.
- Roslyn syntax parsing of 12 changed C# files: zero syntax errors. This was not a mod compilation.
- Authored JSON / color-tag balance / inline-payload scan: PASS.
- `git diff --check`: PASS.

Godot import/resource loading, real display, gameplay and save/resume acceptance remain subject to the main development chat's independent review and runtime admission. The separately authorized formal109 compile and CLR checks for this save fix are documented in `review-r5-map-save-and-final-art-20261102.md`; they do not constitute real-game acceptance. The manifest version stays at the current main version; release versioning is not performed by this feature branch.

This branch carries the previously unmerged issue#238 implementation as well as the Nov 1 continuation. Review against `origin/main...HEAD`; integrating only the final incremental commit would omit required earlier implementation.

Human review gallery: `游戏/杀戮尖塔2/开发工单/issue238-v1.4.0-美术审核清单.md` in the `all-in-one` Obsidian vault. Self-Help Book and Stone of Eternity's redesigned stories await separate Artanis content review.
