# issue#238: formal109 Brain reward ownership

The a655 native attempt passed three real save comparisons, then selecting
NOSTALGIA failed in `RunState.AddCard` with an already-owned card. It rolled back
the cost and never reached `CardPileCmd.Add`. Preserve both historical REDs and
the save-comparison subtest successes; neither is rewritten by this source fix.

Only the Brain route changes: create each cached canonical candidate once with
native `owner.RunState.CreateCard`, upgrade, show the same required selection,
then keep the existing transaction/cost and add the selected card to Deck.
Validate `CardPileAddResult.success`, non-null `cardAdded`, its actual owner and
Deck membership. Do not register or set Owner again. Do not require the original
selected instance to be in Deck: official109's FresnelLens can clone/enchant it
through `ModifyCardBeingAddedToDeck`, and the native receipt carries that clone.

Official109 `C2D3E15...` was decompiled read-only for RunState, CardModel,
CardPileCmd, CardPileAddResult and FresnelLens. CreateCard performs mutable clone,
registration and AfterCreated. Add-to-Deck requires prior registration, permits
the native hook replacement and reports the actual added instance. The full
mod compiles against that exact official reference with zero warnings/errors;
external obj/bin and an empty CopyCustomOutput override avoid owner/shared output.

The focused source gate rejects 12 unsafe mutations: unregistered/manual/duplicate
ownership, ignored success/null/Owner/Deck receipt, rejected valid replacement,
changed cost/count/cancel rules and foreign selection. Cached IDs/order, event
RNG, cost, transaction, journal and save boundaries are unchanged by the delta.
The a655 comparator and Annotations cancellation source remain unchanged.

No game, PCK export, push, merge or deployment is part of this check. A new native
attempt must prove actual reward delivery and one cost; a necessary native
replacement representative remains separate from source/API verification.
