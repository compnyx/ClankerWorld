---
title: Known Bugs and Product Gaps
type: defect-register
status: active
updated: 2026-09-27
---

# Known bugs and product gaps

This is the canonical durable list of confirmed current problems. GitHub issues
may track active implementation work, but closing an issue does not remove an
entry here until the fix is merged and verified through the normal product
path.

## Live verification pending

The provider-held tick fix is installed on the live VPS host, but its paired
Windows client build has not been tested on computment's laptop and the world
is intentionally paused. Its live model-wait playtest remains pending; see
the resolution evidence below.

## September playtest reports and confirmed product gaps

- **Disconnected-land agent placement broke observation — deployed on
  2026-09-28, pending player verification:** after adding an
  adult on land with no foot route to the camp's berry patch, route preview
  threw during every reconnect. The preview now reports no reachable food route
  without taking down the world view. The client also accepts the host's pause
  receipt when leaving for the Main Menu, even if the subsequent refresh fails.
  The deployment gate additionally caught and repaired an old generated-map
  compatibility rejection before the live service was changed. The live save
  migrated from schema 17 to 20 with its tick, population and map identity
  preserved.
- **Final zoom-out cap needs playtest selection:** the client can now zoom to
  8 px terrain tiles (roughly 50% wider coverage than the old 12 px minimum on
  a 256-tile-wide map). Confirm that tile readability and frame cost are right
  on the player's target hardware before treating this as the final cap.
- **Bridges absent — current capability gap:**
  agents can now cross one-tile-wide river tiles on foot at half dry-ground
  speed; wider river sections remain impassable, and no Road or bridge system
  exists yet. Sufficiently used crossings can
  automatically gain bridges. Town-generated Roads, not walking alone, serve
  building placement and automatic links between Towns when a legal land route
  exists. Bridges need spacing against redundant crossings on the same river,
  without blocking needed crossings on a separate nearby stream.
  Exact routes, bridge radius and
  materials remain open.
- **Agents rarely pursue non-survival activity — playtest report, partly
  addressed:** stable adults now have a bounded local curiosity outing that
  remembers visited tiles and returns; whether that feels sufficiently useful
  requires owner playtesting. The bounded prototype is a useful safe starting
  point, but the recommended next direction is purpose-driven discovery of
  resources, terrain and Towns after the owner judges its pacing. Exploration
  does not yet search for distant
  resources or Towns, or create tradable knowledge. Low hunger or urgent
  weather exposure can still pause projects and narrow adult choices to routine
  survival. There is no separate numeric safety need; `safe_idle` is a fallback
  action, not a safety meter. The current runtime now removes energy and sleeping; food and urgent
  weather exposure still constrain some actions.
- **Save compatibility policy remains open:** Load World now preflights saved
  checkpoints and required local model credentials, labels compatible,
  incompatible or unknown, and preserves and blocks only proven-unloadable
  entries. Unknown entries can still be tried. Longer-term migration and
  cross-release/mod compatibility policy remain undecided.
- **Generated vegetation art and propagation remain provisional — confirmed
  gap:** generated broadleaf/conifer and generic orchard-fruit tree objects now
  occupy at most one tree per tile. Wood trees visibly become stumps or
  replanted saplings; orchard trees show fruiting, picked and growing stages.
  The Godot client draws simple top-down shapes, not approved final textures.
  Orchard species, yield, seasonality and cultivation are not finalized;
  replanting spends the prototype generic seed on an existing depleted wood
  tree site, while planting on new tiles and species-specific seed/art remain
  unbuilt.
- **Mountains/peaks not seen in playtest — unverified occurrence, confirmed
  visibility gap:** generation does classify dry-land elevation ≥215 as
  mountain and ≥245 as peak, so the terrain kinds exist. Their distribution
  depends on seed and water classification, and the current overview has no
  elevation inspection. Check the reported world's seed/map before calling
  this an absence or changing thresholds.
- **Agent-chosen names feel too similar — playtest report, cause not proven:**
  the provider prompt now asks for a full name with a given name and
  family/surname, with a middle name optional, matching the accepted name
  format. World-specific cultural context remains undecided, and the whole
  existing-name list is intentionally not sent to the model. The accepted goal
  is exact full-name rejection while allowing similar-but-distinct names. The
  runtime only trims chosen names and rejects empty, control-character or over-48-character
  values; it does not reject duplicate full names or implement explicit
  collision retry/fallback. Exact-comparison normalization and retry/fallback
  details remain unresolved before implementation.

## Original reports and resolution evidence

- **Main Menu Settings clicks blocked — fixed in repository build, laptop check
  pending:** the title overlay stayed visible above Settings in input order,
  swallowing clicks even though the Settings panel drew in front. It now passes
  pointer input through only while Main Menu Settings is open. A headless Godot
  pointer-click smoke failed on the original path and passes with the fix;
  computment's Windows playtest remains pending.
- **Ground hover and marker hitboxes — fixed in repository build, laptop check
  pending:** bare ground gains a square tile outline; agent markers take hover
  and selection priority. Four same-tile markers fit into separate bounded
  cells at 12–48 px test zoom, so neighboring tiles do not hit them. The Godot
  UI smoke exercises the client map path; the revised Windows build still
  needs computment's playtest.
- **Main Menu World Settings leak — fixed in repository build, laptop check
  pending:** during a generated-world playtest, Main Menu → Settings exposed
  World Settings and its click entered the world unexpectedly. Main Menu
  Settings now hides the category and refuses world-specific settings without
  a loaded world. The Godot UI smoke checks both visibility and blocked
  navigation; the revised Windows build still needs computment's playtest.
- **Black terrain grid — fixed in repository build, laptop check pending:**
  removing the tile gap keeps square terrain tiles but eliminates the dark
  lines between them. Future textured terrain needs its own art review.
- **Agent hover and condition flicker — fixed in repository build, laptop
  check pending:** observation refreshes recreated every agent button each
  second, terminating hovered tooltips. Agent markers now update in place.
  Warmth/illness/diet/equipment are pinned outside the refreshed scrolling
  details so the condition block remains visible in the selected-agent card.

- **Provider-held tick — fixed in build, live verification pending:** hosted
  provider calls now sit outside the tick transaction. Tests prove an
  indefinitely pending model does not hold the world or deterministic agents,
  and pause/reload cannot admit the old answer. The VPS server has been updated
  without changing the paused save, pairing authority or provider settings;
  a resumed paired-client playtest remains before calling the defect fully closed.

- **AW-B020 — fixed:** rollback deleted placed buildings and production history
  belonging to the package, despite committed costs and outputs. Referenced
  packages now reject removal before any mutation; recorded settlement projects
  also block removal. A compatible conversion/migration workflow remains future
  work, not a hidden destructive fallback.
- **AW-B021 — fixed:** removing an unrelated unused package changed every
  remaining running production/crop job to cancelled. Unused withdrawal now
  preserves those jobs and reservations; regressions prove normal completion
  and restart afterwards.

- **AW-B018 — fixed:** household-owned production reservations outlived a dead
  worker, allowing unfinished crafting and crops to complete. Running jobs now
  cancel before completion and release remaining inputs. Six regressions cover
  both job types, completion-boundary death, already-finished work, safe logs
  and restart preservation.
- **AW-B019 — fixed:** map object labels ignored mouse hover and were recreated
  on every observation refresh. Resource/object labels now accept hover and
  retain identity across updates. An engine check verifies actual mouse entry,
  updated stock/help and removal of absent markers.

The reports below describe the original defects; their resolution is recorded
in the following repair evidence, not implied to remain open.

| ID | Priority | Area | Original problem | Acceptance condition |
| --- | --- | --- | --- | --- |
| AW-B001 | High | Content/gameplay | A fresh/default private world has no active starter package, leaving zero building and recipe definitions and no meaningful planning-provider work. | A new and existing private world receive a versioned starter content pack with useful materials, tools, shelter, storage, fire/workshop and crop/food recipes; planning candidates occur in normal play. |
| AW-B002 | High | Cognition/cost | Routine cognition can repeatedly pay for `safe_idle` when no meaningful observation changed. | Idle decisions are reused/coalesced until relevant state changes or a bounded reevaluation deadline; telemetry proves materially fewer no-op calls. |
| AW-B003 | Medium | Settings UI | “Inhabitant cognition” is visually dense and too wordy for a normal settings screen. | The panel uses compact role/provider/model/key rows, progressive help and clear saved/error state without losing the two-role distinction. |
| AW-B004 | Medium | Menu layout | With no inhabitant selected, the pause/menu content drops toward the bottom of the screen. | Menu position and layout remain stable regardless of world selection state and supported window size. |
| AW-B005 | Medium | Player observability | World Settings now shows installation-lifetime paid-call and token totals by provider/model, while the selected-agent tooltip shows the last accepted decision. A compact activity history of accepted/fallback outcomes is still missing. | A compact in-game activity view shows provider role/model, accepted/fallback outcome and bounded usage/latency without exposing prompts, raw responses or secrets. |

## First repair increment

- **AW-B001 — implemented:** normal host ticks now stage a validated,
  versioned starter package for both fresh and old saves. Four buildings and
  three recipes lead to construction, production and household food pickup.
  A dependency-linked supplement adds stone/fiber/seeds and hearth/weaving/grain
  content; persistent projects acquire inputs and other inhabitants share
  requested materials. The ordinary 1,000-tick settlement regression covers
  three-input gathering, sharing, completion, eating and public gratitude.
- **AW-B002 — implemented:** persisted idle observation keys prevent repeated
  calls for unchanged choices; a 300-tick deadline bounds reuse. A regression
  test proves four calls remain four through 61 ticks and a reload.
- **AW-B003 — implemented:** compact controls, progressive help, and explicit
  world/inhabitant target selection retain both cognition roles.
- **AW-B004 — implemented:** container-owned centering replaces cached-height
  placement. Godot checks selection and settings toggles at three window sizes.
- **AW-B005 — implemented:** selected-inhabitant cards show accepted provider,
  action and fallback; tooltips show model, role and available usage/latency.
  Existing server logs remain the detailed operator diagnosis path.

These are implementation evidence, not a claim that Living Settlement's whole
acceptance gate is complete. Live migration remains deferred while the owner
keeps the existing world manually paused.

## Survival integration regressions

- **AW-B006 — fixed:** production could reserve fresh ingredients that spoiled
  before completion, causing the tick to throw repeatedly. Completion now
  cancels that job, releases remaining reservations and emits
  `production_input_unusable`; the regression verifies subsequent ticks advance.
- Food availability, consumption and production input selection all exclude
  spoiled/ruined lots, so an older unusable lot cannot shadow usable stock.
- **AW-B007 — fixed:** construction could repeatedly select a site occupied by
  an idle inhabitant. Candidate sites now exclude other inhabitants and require
  a reachable route. An urgent-cold bootstrap regression proves protective
  construction and heating remain possible instead of permanent path retries.

## Runtime audit issues

- **AW-B017 — fixed:** descendant IDs contain
  colons, making directly interpolated building IDs invalid. Placement rejected
  after completed work. Noncanonical society IDs now produce stable hashed
  building IDs; existing canonical founder IDs retain their original mapping.
- **AW-B012 — fixed:** continuing project work
  could suppress a mentor's decision to answer a teaching request until expiry.
  Pending requests now interrupt project continuation for an independent response.
- **AW-B013 — fixed:** unreachable shared food could outrank nearby berries.
  Shared-food candidates now require a reachable pickup point. The former
  shelter/bedding rest defect became obsolete when sleep was removed.
- **AW-B014 — fixed:** construction rescanned
  sites during work and could abandon a legal current site when another person
  vacated an earlier tile. The current legal site now takes precedence.
- **AW-B015 — fixed:** finite wild timber left
  later generations without renewable building/fuel inputs. A dependency-linked
  coppice package adds delayed cultivation without refilling depleted wild nodes.
- **AW-B016 — fixed:** activity/condition logs
  split descendant IDs at the first colon. Known complete IDs now resolve event
  ownership; private prose remains excluded.

Focused regressions cover mentor interruption, legacy bedroll-save migration, no-sleep survival,
current-site completion, additive forestry activation/rollback and descendant
condition logs. A controlled parenthood scenario with opt-in fast biological
aging verifies birth, real feeding, two save/reloads, adulthood, earned training
and completed construction within 12,000 ticks, using deterministic providers.
It is not a claim that arbitrary seeds or population growth are balanced.

- **AW-B010 — fixed:** partnership kinship checks discarded death-ended
  parentage and only checked direct parents, allowing siblings after parental
  death and missing grandparents. Historical parentage now supplies sibling
  and full direct-ancestor checks; runtime scenarios cover living/dead
  intermediate relatives and incorrectly revoked legacy parentage.
- **AW-B011 — fixed:** generic relationship revocation could revoke biological
  parentage despite the immutable-history contract. Revocation and acceptance
  of fabricated parentage proposals now reject without changing edges/births;
  proposal creation remains restricted to the birth transaction.

- **AW-B009 — fixed:** revoking one caregiver relationship removed the adult
  from household caregiver tracking even when another dependent still had an
  accepted care edge. Projection now retains the adult until the last relevant
  obligation ends; a two-dependent regression covers both transitions.

- **AW-B008 — fixed:** an idle decision could cache choices created by another
  inhabitant after its observation was taken, suppressing a response to a new
  offer. The cache is now bound at observation enqueue; barter tests prove the
  recipient sees the new choice on the next tick, including across restart.

The following confirmed GitHub reports are tracked here as well; their issue
pages retain reproductions and regression requirements.

| Issues | Status in implementation | Defect |
| --- | --- | --- |
| [#107](https://github.com/compoodment/ClankerWorld/issues/107) | Fixed; activation and rollback regressions | Dependency quarantine/activation order; active dependents must be rolled back first |
| [#108](https://github.com/compoodment/ClankerWorld/issues/108) | Implemented; archive/restart and stale-cursor regression coverage | Unbounded checkpoint event history and rewrite cost |
| [#109](https://github.com/compoodment/ClankerWorld/issues/109), [#116](https://github.com/compoodment/ClankerWorld/issues/116) | Fixed; stalled-provider, pause, cancellation and concurrent-tick tests | Partial ticks and provider-held authoritative locks |
| [#110](https://github.com/compoodment/ClankerWorld/issues/110), [#111](https://github.com/compoodment/ClankerWorld/issues/111), [#112](https://github.com/compoodment/ClankerWorld/issues/112) | Fixed; boundary and clock-advance regressions | Expired barter acceptance and reservation expiry |
| [#113](https://github.com/compoodment/ClankerWorld/issues/113), [#114](https://github.com/compoodment/ClankerWorld/issues/114), [#115](https://github.com/compoodment/ClankerWorld/issues/115) | Fixed; atomic rejection and restore regressions | Asset ordering, conflicting shared charges and overflow |
| [#117](https://github.com/compoodment/ClankerWorld/issues/117), [#118](https://github.com/compoodment/ClankerWorld/issues/118) | Fixed; projection and restore regressions | Missing crop jobs and empty-population observation crash |
| [#119](https://github.com/compoodment/ClankerWorld/issues/119) | Fixed; declared/chunked/misreported size tests | Unbounded provider response buffering |
| [#120](https://github.com/compoodment/ClankerWorld/issues/120) | Fixed; 600 signed polls with no authority writes | Idle authority write amplification |

- **AW-B022 — fixed:** the client ignored placed-building observations, so
  completed structures were absent from the map. Stable markers now show their
  names, purpose and actual footprint; engine tests cover rendering and removal.

## Register maintenance

- Record only reproduced defects or demonstrated product gaps.
- Put intended features in the [vision ledger](vision-interview.md), not here.
- Never store credentials, private world contents or raw provider payloads in a
  bug report.
- When a fix lands, update the affected canonical current-state document in
  the same change and preserve detailed evidence in tests or the relevant
  implementation ledger.
