---
title: Selected-Tile Inspection Prototype Review
type: prototype-review
status: proposal
updated: 2026-09-28
---

# Selected-tile inspection prototype

Status: **reviewable prototype, not a finished-game decision** ([issue #142](https://github.com/compoodment/ClankerWorld/issues/142)).

Clicking empty ground in the Godot world view opens a bottom-left tile card and
outlines that tile. Placement mode still owns clicks during starting-agent or
Add Agent placement; agent markers still take click priority. The card updates
as observations change and closes without changing world state.

| Field | Source | Prototype treatment |
| --- | --- | --- |
| Coordinates and terrain kind | Signed terrain observation | Shown as the stable terrain projection |
| Climate, elevation, hydrology, surface and vegetation cover | Signed generated-map layers | Shown separately; vegetation cover is not a count of individual trees |
| Camp objects, resource sites/stock, building footprints | Signed object/resource/building observation | Shown when present |
| Weather and soil moisture | Signed regional weather observation | Shown as regional values; unavailable when absent |
| Fertility and exact temperature | Not projected to this client | Marked unavailable; never inferred from color |

The Godot smoke path checks the separate observed facts, their use in rendered
ground colors, resource-stock refresh and honest unavailable labels. It does
not establish whether the panel is useful or whether a future biome model
should use these exact labels. **Recommendation:** keep tile selection and the
honest unavailable treatment, then review the card in a playable build before
locking its final field list or placement. Do not conflate player inspection
with any individual agent's knowledge.
