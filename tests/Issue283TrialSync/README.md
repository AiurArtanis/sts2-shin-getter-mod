# issue#283 Trial challenge ordering regression

Run from the repository root:

```powershell
dotnet run --project tests/Issue283TrialSync/Issue283TrialSync.csproj --no-launch-profile
```

This CPU-only fixture directly links the production synchronization controller, commit message and Harmony patch methods. Test doubles provide players/events, native UI/synchronizer routing, transport, location, serialization storage, native combat entry, pending setup and first-hand consumption. Harmony itself is not loaded: its attributes are shims and production patch methods are invoked through reflection at the source-confirmed native boundaries.

## Entry correction and RED

Revision 2's 21 passing checks manually supplied BeforeChoice/Request/history/AfterChoice in Peer.Choose. They did not test actual START_FIGHT routing. Formal109 NEventRoom.OptionButtonClicked runs IsProceed options directly through option.Chosen and returns; those buttons do not ordinarily call ChooseLocalOption. The earlier assertion that START_FIGHT necessarily used the normal broadcast path was wrong. BeforeOptionChosen having no async wait does not establish the missing route. Independent review rejected that candidate; previous commits/reports remain historical evidence, not current entry coverage.

The new native-shaped button tests were added before the route fix. With the production route absent they followed IsProceed=true's local callback and failed with `IsProceed START_FIGHT bypassed the synchronized request/commit route`. Making the Owner double nullable also exposed the three production nullable warnings reported by review. The fixture now treats all warnings as errors.

## Current Coverage

The modeled NEventRoom dispatch first invokes the actual production button Prefix. Only eligible multiplayer Trial START_FIGHT is rerouted to native ChooseLocalOption. The synchronizer double invokes the actual production choice Prefix, option callback, modeled native history append, Postfix and Finalizer in that order. Peer.ClickStart does not supply controller Prefix/Postfix itself. Outgoing native option messages and host commits are delivered independently, including an original option arriving after commit/the first-hand hook. Peer.Choose is the native *receive-side* model, not evidence of a UI path.

27 checks cover host and Getter-client Proceed clicks, two real-entry-shaped clicks with reversed host arrival, repeated clicks, original option before/after commit or first hand, immutable first accepted owner, exactly-once entry/setup, mixed-party eligibility, sender/owner scope, authority, seed/location, foreign event, wrong/locked button/index, nullable Owner, unrelated Proceed, singleplayer route exclusion, ordinary-choice controls, duplicates/late requests, synchronous entry failure, reset/departure/disconnect/disposal.

The host's first valid request is accepted after native ChooseOptionForEvent returns (after SaveEventOptionToHistory). START_FIGHT keeps `isProceed: true` and its default history flag. The narrow route now deliberately uses native normal selection/broadcast/history for this one multiplayer action; the old local-only Proceed route did not record it. All peers should consequently record that native choice once, with no custom history edits. All later challengers are unaccepted requests, not accepted challengers whose setup may be dropped. Clients install the committed owner's setup before local native entry, independent of the original option message. Singleplayer/other Proceed buttons keep their original routing.

The packet double checks field order/preservation, not native bit encoding. First-hand consumption and history are doubles, not evidence that real spirit cards, original history, checksums, rewards, selection tasks, or mixed-party cleanup pass. The fixture does not load Harmony, Godot or sts2.dll, does not compile the mod, and does not launch the game. Native asynchronous entry failure is not observed by the synchronous exception test; a committed decision is never reopened for retry. All actual multiplayer/history/selection/reward cleanup and gameplay acceptance remain NOT_RUN.

## Initialization failure and complementary gate

Real candidate `4a70301b` failed Entry.Init because the constructor HarmonyPatch omitted its five argument types. This fixture's attribute shim and explicit hook calls did not cover that target lookup. Its 27 PASS remain modeled-behavior evidence only; candidate runtime admission is revoked/BLOCKED. The complementary `tests/Issue283HarmonyTargets` probe now compiles the actual production Patch file against real 0Harmony/formal109 metadata and resolves all 7 targets/9 hook contracts, including missing/wrong-signature negative controls. Neither probe alone proves complete mod initialization or real gameplay. The old 31 runtime business observations from partial initialization are not a candidate PASS.
