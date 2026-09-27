# Selected-tile inspection prototype

Status: **reviewable prototype, not a finished-game decision** ([issue #142](https://github.com/compoodment/ClankerWorld/issues/142)).

Clicking empty ground in the Godot world view opens a bottom-left tile card and
outlines that tile. Placement mode still owns clicks during starting-agent or
Add Agent placement; agent markers still take click priority. The card updates
as observations change and closes without changing world state.

| Field | Source | Prototype treatment |
| --- | --- | --- |
| Coordinates and ground kind | Signed terrain observation | Shown |
| Camp objects, resource sites/stock, building footprints | Signed object/resource/building observation | Shown when present |
| Weather and soil moisture | Signed regional weather observation | Shown as regional values; unavailable when absent |
| Elevation, fertility, exact temperature, separate biome | Not projected to this client | Marked unavailable or omitted; never inferred from color |

The Godot smoke path checks selection, resource-stock refresh and unavailable
labels. It does not establish whether the panel is useful or whether a future
biome/elevation/fertility layer should use these exact labels. **Recommendation:**
keep tile selection and the honest unavailable treatment, then review the card
in a playable build before locking its final field list or placement. Do not
conflate player inspection with any individual agent's knowledge.
