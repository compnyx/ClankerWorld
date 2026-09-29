---
title: Tailor Shop replacement contract
type: design-proposal
status: proposal
updated: 2026-09-29
---

# Tailor Shop replacement contract (unapproved)

Review proposal for [#363](https://github.com/compoodment/ClankerWorld/issues/363).
This does not settle recipes or implement a Tailor Shop. The current Weaving
frame remains usable until its replacement works; old buildings are not deleted.

## Already agreed

The [content roster](finished-game-asset-roster.md) assigns clothing to a Tailor
Shop, with a 1×1 or 2×2 footprint. Plant fiber becomes a distinct, holdable,
tradeable cloth intermediate before clothing. Exact recipes, tool requirements,
costs, throughput and sales workflow remain undecided. The
[vision interview](vision-interview.md) removes the Weaving frame from the
finished roster, not by breaking existing saves or the only current clothing
source. Late animal husbandry is not a prerequisite for initial clothing.

## Decisions still needed

| Decision | Choices to evaluate; none selected here |
| --- | --- |
| Cloth production | Tailor converts fiber in-house; or a separate agreed producer supplies cloth |
| Ownership and access | Household stock and direct sales; or another explicitly agreed access model |
| Labour and tools | Required role, skill gate, tool input/durability and novice fallback |
| Recipe balance | Fiber-to-cloth ratio, cloth-to-clothing ratio, work time and batch size |
| Initial availability | Starter supply and behaviour before any Tailor Shop exists |
| Building | Choose footprint, costs and legal placement with the ordinary layout system |
| Legacy transition | Keep existing frames usable; decide when to stop offering new ones after replacement validation |

## Implementation acceptance contract

1. Define stable content IDs for cloth, Tailor Shop and its approved recipes;
   preserve item quantities, ownership and existing save references.
2. Offer legal, actor-reachable acquisition, production and collection actions.
   Material availability, reservation and start/completion use the same owner
   and physical storage rules. No cross-household stock is silently consumed.
3. An adult without clothing can obtain and wear it through ordinary decisions.
   No-workshop and unavailable-material cases remain retryable, not fake success.
4. Test production/consumption accounting, access denial, missing inputs,
   interrupted work, save/reload and old-frame preservation. Do not substitute
   a mocked candidate for the ordinary offer/start/complete path.
5. Record a separate normal game-window playtest showing clothing acquisition,
   use and the no-shop case. Neither this proposal nor future CI is that evidence.
6. Retire new Weaving-frame offers only after approved replacement production
   and access pass. No deployment or active-world rewrite is part of this draft.
