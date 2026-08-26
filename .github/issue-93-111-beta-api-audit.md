# issue#93 — Slay the Spire 2 0.111 Beta API audit

This audit compares the released `mod-v1.2.0` source against the current formal-game source and the read-only 0.111 Beta source before applying compatibility changes.

## Inputs and method

- Mod baseline: `patch/support-111-beta@671c62d8a25caec8c49bbb5a9fa7475902e636a3`, identical to `mod-v1.2.0^{}`.
- Formal source: `E:\Work\SlaytheSpare2` (read-only).
- 0.111 Beta source: `E:\Work\SlaytheSpare2-111-beta` (read-only).
- Formal `sts2.dll`: 10,163,200 bytes, SHA-256 `C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`.
- Beta `sts2.dll`: 10,657,280 bytes, SHA-256 `6896BBA91CEDDC661B3F789749E9F0AAC338F5DDBBB92C598FC344DEC822DC19`.
- Both use `0Harmony` 2.4.2.0, SHA-256 `EF1898322C9F5C86DC1B0758B272A9C440823B4A41CA9A0B82A3AA6B3D206387`.
- Both projects use Godot.NET.Sdk 4.5.1 and .NET 9. The Beta adds `CheckForOverflowUnderflow=false`, upgrades Sentry, and adds `Sentry.Godot`/explicit `SharpGen.Runtime`; none of those are directly referenced by the mod.
- CodeGraph was used first for Beta definitions and call paths. Targeted source reads then verified signatures and private targets. The rebuilt Beta assembly inventory contains 377 game type references, 698 direct game member signatures, 33 members on constructed generic game types, and 1,015 override edges back into `sts2`.

## Compatibility checklist

| Mod reference point | Formal definition | 0.111 Beta definition | Conclusion / implementation |
| --- | --- | --- | --- |
| `ShinGetter.GenerateAnimator` | `GenerateAnimator(MegaSprite)` | `GenerateAnimator(MegaSprite, Creature)` | Breaking override. Accept the new `Creature` without changing the custom idle-only animator behavior. |
| Eight Shin Getter damage powers | `ModifyDamageAdditive/Multiplicative(..., CardModel?)` | Same hook plus trailing `CardPlay?` | Breaking override. Add and forward the new context parameter; keep every power's existing formula and owner checks. |
| 63 card attack builders | `AttackCommand.FromCard(CardModel)` | `AttackCommand.FromCard(CardModel, CardPlay?)` | Breaking call. Pass the active `cardPlay` at every card-origin attack so X-cost/resources and per-play hooks stay associated with the correct play. |
| Manual card damage | Card-source overloads end in `CardModel?` | Card-source overloads end in `CardModel?, CardPlay?` | Breaking call. Card/enchantment damage passes the active `cardPlay`; power/event damage explicitly passes `null`. |
| Avalanche block consumption | `LoseBlock(Creature, decimal)` | `LoseBlock(PlayerChoiceContext, Creature, decimal, Creature?)` | Breaking call. Use the active choice context and `remover=null`, matching Beta's non-creature removal convention. |
| Getter Chop block theft | `LoseBlock(Creature, decimal)` | Same new four-parameter overload | Breaking call. Pass the active context and `Owner.Creature` as the remover, matching Beta's Expose-style creature-caused removal. |
| Getter Landing form choice | `SignalPlayerChoiceBegun(PlayerChoiceOptions)` | `SignalPlayerChoiceBegun(Player, PlayerChoiceOptions)` | Breaking call. Pass the actual choosing player; remote choice reservation and completion remain unchanged. |
| Random character selection patch | `LobbyPlayer` and `PlayerChanged(LobbyPlayer, bool)` | `StartRunLobbyPlayer` and `PlayerChanged(StartRunLobbyPlayer, bool)` | Removed type/signature. Replace only the patch argument type; `id`/`character` semantics are preserved. |
| Potion rarity patch | private `CreateRandomPotion(...): List<PotionModel>` | private `CreateRandomPotions(...): IEnumerable<PotionModel>` | Removed Harmony target. Retarget the plural method and change `__result` to `IEnumerable<PotionModel>`; retain the mod's rarity roll and no-duplicate selection. |
| Open Get final-damage patch | `Hook.ModifyDamage(..., CardModel?, ModifyDamageHookType, ...)` | Same method with `CardPlay?` after `CardModel?` | Harmony signature changed. Target name remains unique and the postfix's named subset remains valid; the final `__result` stage is still after additive, multiplicative and cap hooks. |
| Damage-cap hook family | `ModifyDamageCap(..., CardModel?)` | Adds trailing `CardPlay?` | No mod override. Runtime call order remains additive → multiplicative → cap. |
| `CardCmd.Exhaust` | `Task` | `Task<CardPileAddResult?>` | Source-compatible: every mod caller awaits and ignores the result. |
| `CardPileCmd.Add(IEnumerable<CardModel>, CardPile)` | Existing overload | Adds `isChangingOwners` to this overload | No affected mod call; used overloads retain their signatures. |
| `CardCreationOptions` | Includes custom `IEnumerable<CardModel>` constructor | That constructor is removed | No affected mod call; the mod uses the retained `CardPoolModel + filter` path. |
| `EventOption` | Existing constructors | Adds a copy constructor | Additive, no change. |
| `MegaSprite` | Class without `IDisposable` | Implements `IDisposable` | Additive lifetime contract; mod-owned sprites remain Godot-node-owned and require no new disposal path. |
| `ModManifest` | `class ModManifest` | `record ModManifest` with the same JSON properties | Serialization-compatible; `id`, `version`, `has_pck`, `has_dll`, dependencies and minimum-game-version fields remain available. |
| Mod discovery / initializer | `[ModInitializer("Init")]`, same-name DLL/PCK next to manifest | Same attribute and `CallModInitializer` behavior | Compatible. `Entry.Init`, explicit Harmony registration, and the 77-card success log remain intact. |
| Resource loading | `ProjectSettings.LoadResourcePack(<id>.pck)` and `res://` lookup | Same DLL/PCK naming and load order | Compatible. Critical character-select, transition, config, localization, atlas and animation paths remain in the mod PCK namespace. |
| Private reflection fields | `_internalData`, Vigor `commandToModify` / `amountWhenAttackStarted`, UI container fields | Same names and assignable shapes | Compatible; guarded by `validate_issue_93.py`. |
| Other Harmony/`AccessTools` targets | 119 explicit patch/reflection calls plus two bare `TargetMethods` patch classes | All unchanged targets remain present except the three rows called out above | Compatible; the mechanical audit records every call, owner/name candidate and source location, while the runtime probe covers changed and critical private targets. |

## Version and package mapping

| Game channel | Mod manifest | Minimum game | Release tag | ZIP |
| --- | --- | --- | --- | --- |
| Current formal game | `v1.2.0` | `0.107.0` | `mod-v1.2.0` | `shin-getter-mod-v1.2.0.zip` |
| Slay the Spire 2 0.111 Beta | `v1.2.0-beta.111` | `0.111.0` | `mod-v1.2.0-beta.111` | `shin-getter-mod-v1.2.0(111-beta).zip` |

The Beta package is a complete v1.2.0 feature build, not a reduced compatibility build. Its distinct semantic version keeps the manifest, tag and archive name aligned with `AGENTS.md` and prevents users from confusing it with the formal-game package.

## RED baseline

Building the untouched `mod-v1.2.0` source against the audited Beta assemblies produced 0 warnings and 10 errors: one `GenerateAnimator` override, eight damage-hook overrides, and the removed `LobbyPlayer` type. The remaining call/Harmony differences were identified by the pre-edit CodeGraph, member-reference and private-target audit rather than waiting for compiler failures.

## Full 0.109 → 0.111 CodeGraph re-audit

The follow-up audit requested before release is reproducible with:

```text
python shin-getter-mod-godot/tools/audit_issue_93_codegraph.py --check
```

- Both `.codegraph/codegraph.db` files are opened with SQLite `mode=ro&immutable=1`.
- The complete file and symbol/declaration diff is committed as `.github/issue-93-109-vs-111-codegraph-diff.json`; the bounded review table is `.github/issue-93-109-vs-111-codegraph-diff.md`.
- The audit traverses every production and compatibility-probe C# file, records each file SHA-256/line count, then cross-references CodeGraph changes against compiled TypeRef/MemberRef metadata, runtime-resolved override bases, and Harmony/`AccessTools` calls.
- Exactly 26 changed symbol groups intersect the mod. Each has an explicit `adapted` or `compatible` conclusion; the gate fails if a future changed-symbol candidate lacks a review conclusion or if a recorded conclusion becomes stale.
- Removed `LobbyPlayer`/`CreateRandomPotion` and their `StartRunLobbyPlayer`/`CreateRandomPotions` replacements are explicitly covered even though the removed symbols are no longer present in the rebuilt Beta assembly.
