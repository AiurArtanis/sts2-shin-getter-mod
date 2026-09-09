# Saint Dragon Naming and Save Migration

## Scope

Use `SGC_SaintDragonRoar`, `CARD.S_G_C_SAINT_DRAGON_ROAR`, and the
`SAINT_DRAGON` Mandala option/page keys consistently. This covers model and
VFX names, three localization tables, console completion, original art filenames,
related validators, and current English release copy where present on each line.
Existing packed portrait/VFX paths already use the canonical spelling.
Card effects, values, rarity, targeting, voice timing, and pool membership do not change.

The unit name is grounded in the read-only SRWY finding:
`E:/SRWY_unpack/analysis/getter_saint_dragon_probe/finding.json`,
`robot_name/GTRARK_010`, official English `Getter Saint Dragon`.
The source does not establish an official weapon name for this mod's Roar card.
The Obsidian card design already specifies `SGC_SaintDragonRoar`.

## Compatibility Contract

`ShinGetterSaintDragonMigrationPatch.cs` contains the only production legacy
input literals: one exact CARD entry and five exact table/key pairs.
Constructor prefixes normalize both native ModelId JSON forms and historical
LocString values. The Exists prefix covers GetIfExists's pre-construction lookup.
Other categories, unrelated names, and longer prefix matches remain untouched.
No legacy card class or second ModelDb entry is registered.

Loading an old card uses native SerializableCard and CardModel.FromSerializable;
upgrades, Corrupted enchantment, and floor metadata survive. Discovered cards,
history decks, nested SavedProperties and Mandala choice titles normalize too.
Native serialization writes the new identifiers. Real saves are never edited by
this test: all fixtures are in memory. Start the updated mod normally; this is
not a live-object hot-reload migration or a downgrade guarantee.
Mixed-version multiplayer is not supported by this rename: all peers must use
the same mod build because model network indices may change.

## Formal 109 Component Test

Run from the worktree root, with the correct 109 libraries in the ignored lib directory:

```powershell
dotnet build shin-getter-mod-godot/ShinGetterMod.csproj --nologo -v:minimal
$env:DOTNET_TieredCompilation='0'
dotnet run --configuration Release --project tests/SaintDragonSaveMigration/SaintDragonSaveMigration.csproj
python shin-getter-mod-godot/tools/validate_saint_dragon_naming.py
```

The production build copies only to this worktree's ignored build directory.
The test installs only the three production migration patches, never Entry.Init
or all game patches. It uses the real native generated serializer, ModelDb,
Corrupted, CardModel and event-history packet reader. It prewarms native readers
before patching and also passes with optimized JIT (tiering disabled).

2026-09-09 result: old baseline failed `legacy string ID was not migrated`;
the new naming gate also failed on missing canonical localization keys.
After the change, 50 managed assertions PASS and the static naming gate PASS.
The gate separately checks the actual pool's 77 unique entries, including exactly
one renamed card; unrelated legacy placeholder classes are not pool membership.
Formal build: 0 warnings / 0 errors; 41 tracked JSON valid; diff-check PASS.
Related static validators: issue#198, issue#21/#31, issue#181, issue#10 PASS.

The allowed residual legacy literals are restricted to the migration file and
`Program.cs` fixture/negative inputs. The gate scans tracked and non-ignored new
text files plus filenames. Original art bytes and the renamed script UID are
unchanged. No image/audio editing is part of this task.

## 111 Beta White-Box Sync

109 and 111 ModelId, LocString, ModelIdRunSaveConverter and
EventOptionHistoryEntry source files are byte-identical. The card retains the
existing Beta-only `FromCard(this, cardPlay)` signature. Migration source and
fixtures are identical between lines. The component test is delivered on Beta
for traceability but is not executed there.

Authorized isolated 111 build: 0 warnings / 0 errors; 42 tracked JSON valid;
related issue#198, issue#21/#31, issue#10 and issue#93 static gates PASS.
The issue#93 audit is regenerated with the existing metadata-only tool and passes
`--check`. Its data refresh is a separate Beta commit, including explicitly
authorized corrections of already-merged baseline inventory; see the Beta-only
audit refresh note for details.

No Godot/game process, Beta dynamic acceptance, PCK export, shared deployment,
real save write, pr, merge, tag or release is performed by this work.

## Remaining In-Game Acceptance

- [ ] On formal 109, load an older run containing the card and confirm the card remains usable instead of becoming deprecated.
- [ ] On formal 109, reload an upgraded, Corrupted copy and confirm upgrade and enchantment remain intact.
- [ ] In English, inspect the card library and confirm a single Saint Dragon Roar entry with its existing artwork.
- [ ] Enter the Mandala event, choose the relevant reward and confirm card preview, reward and ending text display correctly.
- [ ] Open an older run history containing that Mandala choice and confirm the option title displays without a missing-localization error.
- [ ] Switch between Chinese, Japanese and English and confirm the card and event text remain complete.

These remain unsigned. They are not replaced by the managed component result,
and do not authorize restarting any paused game-automation queue.
