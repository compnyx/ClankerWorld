---
title: Buildings, Objects, and Terrain — Current vs Vision
type: design-inventory
status: interview-working-sheet
updated: 2026-09-27
---

# Buildings, objects, and terrain — current vs vision

This is a **review sheet**, not an implemented-content list. “Current” describes
the normal private-world prototype's source as of 2026-09-27. “Vision” points
to the [current decision ledger](vision-interview.md); the building and starter
catalogues are being reconciled. Put final choices in that ledger, then update this
sheet. The [current-state report](current-state.md) owns playable-status claims.

## Important distinction

The generated camp's **map objects** and the content registry's **buildable
building definitions** are separate systems. A label that says “shelter” on
the map is not evidence of a house interior, assigned residents, or a placed
content-package Shelter. There are currently no authored room layouts or
inspectable per-building containers. Inventory is held in world/household
ledgers. The building `capacity` number below is currently a limit on
**simultaneous production jobs at a workstation**, not a confirmed bed count
or occupant limit. Dynamic agent-created designs can add more definitions.

## Buildable built-in definitions — implemented data

Every built-in definition below is **1×1 logical tile**. Construction costs
are resource units. A definition being available does not mean a copy is
already placed in the world.

| Definition | Cost | Capacity field | Current function / recipes | Inside today | Vision review |
| --- | --- | ---: | --- | --- | --- |
| Shelter | 8 wood | 4 | Nearby weather protection; currently improves rest | No rooms or assigned household/home | **Replace with House**; household-exclusive food/storage, cooking, storm refuge, childcare; no occupant cap or sleep role |
| Storehouse | 6 wood | 4 | Presence reduces household food spoilage | No per-building inventory or shelves | **Replace with Warehouse**; inspectable resources at location, available to residents of its Town; no food storage |
| Cooking fire | 4 wood | 1 | Heat when fuelled; meal: 2 food + 1 wood → 4 food, 12 ticks | No interior | **Remove as separate building**; cooking belongs in House |
| Workshop | 10 wood | 1 | Tools: 3 wood → 1 tool, 20 ticks | No interior | **Keep** for inventions/mods, including access by outsiders; tool-production split with Blacksmith open |
| Stone hearth | 8 stone + 4 wood | 2 | Heat when fuelled; hearty meal: 2 food + 1 wood → 5 food, 12 ticks | No interior | **Remove** as separate building |
| Weaving frame | 4 fiber + 4 wood | 1 | Bedding: 4 fiber → 1 bedding, 18 ticks; clothing: 6 fiber → 1 clothing, 24 ticks | No interior | **Remove**; replace clothing function with dedicated business/building, name open |

Source: [starter definitions](../src/ClankerWorld.Simulation/Playtest/StarterContent.cs),
[settlement definitions](../src/ClankerWorld.Simulation/Playtest/SettlementContent.cs),
and [building schema](../src/ClankerWorld.Simulation/Content/ContentDefinitions.cs).
The `capacity` behavior is in
[production placement](../src/ClankerWorld.Simulation/Playtest/PrivateWorldRuntime.cs).

### Production without a building definition

| Recipe / site | Inputs → outputs | Work time | Current site rule | Review |
| --- | --- | ---: | --- | --- |
| Vegetable plot | none → 6 food | 60 ticks | Generated fertile-land site | Food-loop balance |
| Grain plot | 1 seed → 8 food + 2 seed | 60 ticks | Generated fertile-land site | Farming role |
| Managed coppice | 2 seed → 24 wood + 2 seed | 1,440 ticks | Generated fertile-land site | Forest/wood ecology |

These are recipes/crop jobs, **not yet farm buildings or drawn field interiors**.
Sources: [starter](../src/ClankerWorld.Simulation/Playtest/StarterContent.cs),
[settlement](../src/ClankerWorld.Simulation/Playtest/SettlementContent.cs),
[forestry](../src/ClankerWorld.Simulation/Playtest/ForestryContent.cs).

## Generated map objects — current normal starter camp

| Kind | Initial count | What it actually does / does not do | Review |
| --- | ---: | --- | --- |
| Bedroll | 1 | Hard-coded camp reference and fallback rest destination; no room | **Remove** with sleep/energy loop |
| Campfire | 1 | Map object at the camp; not the buildable Cooking fire definition | **Remove**; House cooks |
| Shelter | 2 | Camp markers/occupied tiles; not assigned agent homes | Replace with **two actual Houses** |
| Storage | 1 | Camp marker/occupied tile; not a per-building inventory | Replace with **Warehouse** |
| Workshop | 1 | Camp marker/occupied tile; separate from buildable Workshop | Whether it starts present is open |
| Path | 1 marker | Does not constitute a Road network | Rename **Road**; Town generation produces Roads with buildings |

These come from [base-camp generation](../src/ClankerWorld.Simulation/Harness/SeededWorldHarness.cs).
The normal generated world has four founders added during paused setup; the
fixture-only `founder` marker is **not** an initial person in a new world.

## Generated resource sites and terrain — current

| Resource kind | Where/meaning now | Stock and renewal in a newly generated map |
| --- | --- | --- |
| food | Wild food, including starter berry patch | Renewable: usually 8/12, +2 in Spring when due; future food storage is at House |
| construction | Timber/wood site, often on forest ground | Renewable: usually 8/12, +2 in Spring when due |
| fiber | Gatherable fiber | Renewable: usually 8/12, +2 in Spring when due |
| seed | Gatherable seed | Renewable: usually 8/12, +2 in Spring when due |
| stone | Gatherable stone | Finite: usually 3/3 |
| fertile_land | Site for crop jobs | Finite site marker: usually 3/3; soil is not three farm plots |

The later settlement supplement adds nearby stone/fiber/seed sites at **16/16**;
fiber and seed regain **4 in Spring when due**, while stone is finite. Counts
and quantities are implementation values, not vision targets; live quantities
change through harvesting. Source:
[resource generation](../src/ClankerWorld.Simulation/Harness/SeededWorldHarness.cs),
[ecology initialization](../src/ClankerWorld.Simulation/Playtest/PrivateWorldRuntime.cs),
[supplement](../src/ClankerWorld.Simulation/Playtest/SettlementProjects.cs).

| Terrain kind now | Movement/building now | Vision/status |
| --- | --- | --- |
| Meadow, sand, forest, snow | Passable and buildable ground | Forest is mostly a **color/ground kind**, not generated trees |
| Mountain, peak | Impassable and not buildable | Visible regions wanted; travel rules open; no construction remains current vision |
| River, lake, ocean, fixture water | Impassable and not buildable | Narrow-river foot crossing and later bridge preferred; wider water needs boats/ports |

The terrain renderer currently draws colored tiles and a few glyphs, not a
finished foliage/object art layer. Sources:
[map rules](../src/ClankerWorld.Simulation/Harness/SeededWorldHarness.cs),
[renderer](../src/ClankerWorld.GodotClient/UI/WorldTerrainLayer.cs).

## Intended content roles — **not implemented catalogue entries**

| Concept | Intended role | Current interview status |
| --- | --- | --- |
| House | Household-only entry, unlimited household occupants, private food/resources, cooking, storm refuge, childcare/property | **Decided role**; footprint, contents UI and cooking recipes open |
| Warehouse | Town-resident communal resources, physically inspectable stock | **Decided role**; residency and border changes open |
| Workshop | Inventions/mods, accessible to outsiders | **Decided role**; other crafting jobs and starter presence open |
| Farm fields + Farmhouse | Household plants/tends/harvests fertile fields; Farmhouse processes crops | **Decided role**; work/recipe details and farm cap by Town size open |
| Farm Silo or linked store | Private farm work stock | **Preferred**, exact form and access open |
| Blacksmith | Household-owned tool business with internal work stock | **Decided role**; recipes/tier progression open |
| Clothing-making building | Makes clothing as a distinct business | **Decided role**, name and chain open; Tailor Shop is Clanker's suggestion |
| Store | Household-run shop to sell/trade farm, tool or clothing goods | **Decided role**; economy rules open |
| Market | Trading venue open to agents from any Town | **Decided role**; distinction from Store and management open |
| Restaurant | Optional agent-founded business that buys ingredients and sells cooked meals | **Decided possible building**; quality/pricing open |
| Town Hall | Town governance | **Decided role**; timing/mechanics open |
| Port | Boat access | **Decided role**; timing/mechanics open |
| Roads and bridges | Roads generated with Town buildings and perhaps between Towns; traffic may trigger bridges | **Decided direction**; layout, permanence, bridge thresholds open |
| Trees, stumps, seeds and regrowth | Physical harvestable forest with regrowth and planting | **Decided direction**; ecology rates/art open |
| Iron, gold, diamond, more materials/tools | Advancement and crafting | **Decided direction**; tool-resource tier order must be clarified to avoid cycle |
| Livestock and mounts | Care, production or transport | Finished-game scope, detailed design deferred |

**Next pass:** settle tool tiers, the starter extras, exact field/Silo model,
business ownership and building access, and what “inside” means visually.
No invented costs, occupancy limits or recipe stats are approved here.
