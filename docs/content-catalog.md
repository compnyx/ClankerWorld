---
title: Buildings, Objects, and Terrain — Current vs Vision
type: design-inventory
status: interview-working-sheet
updated: 2026-09-27
---

# Buildings, objects, and terrain — current vs vision

This is a **review sheet**, not a claim that intended content is implemented.
“Current” describes
the normal private-world prototype's source as of 2026-09-27. “Vision” points
to the [current decision ledger](vision-interview.md); the building and starter
catalogues are being reconciled. Put final choices in that ledger, then update
this sheet. The [current-state report](current-state.md) owns playable-status
claims.

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
| Shelter | 8 wood | 4 | Nearby weather protection; currently improves rest | No rooms or assigned household/home | **Replace with House**; private food/storage, cooking, storm refuge, childcare; unlimited household occupants, invited visitors, no sleep role |
| Storehouse | 6 wood | 4 | Presence reduces household food spoilage | No per-building inventory or shelves | **Replace with Warehouse**; inspectable resources at location, available to residents of its Town; no food storage |
| Cooking fire | 4 wood | 1 | Heat when fuelled; meal: 2 food + 1 wood → 4 food, 12 ticks | No interior | **Remove as separate building**; cooking belongs in House |
| Workshop | 10 wood | 1 | Tools: 3 wood → 1 tool, 20 ticks | No interior | **Keep** for inventions/mods, including access by outsiders; tool-production split with Blacksmith open |
| Stone hearth | 8 stone + 4 wood | 2 | Heat when fuelled; hearty meal: 2 food + 1 wood → 5 food, 12 ticks | No interior | **Remove** as separate building |
| Weaving frame | 4 fiber + 4 wood | 1 | Bedding: 4 fiber → 1 bedding, 18 ticks; clothing: 6 fiber → 1 clothing, 24 ticks | No interior | **Remove**; replace clothing function with accepted Tailor Shop |

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
| Workshop | 1 | Camp marker/occupied tile; separate from buildable Workshop | Not guaranteed at start; later invention role stays |
| Path | 1 marker | Does not constitute a Road network | Rename **Road**; generated Roads immediately connect starter Houses and Warehouse |

These come from [base-camp generation](../src/ClankerWorld.Simulation/Harness/SeededWorldHarness.cs).
The current generated world adds four agents during paused setup (the code
calls them `founders`); the fixture-only `founder` marker is **not** an initial
person in a new world. The intended player-facing term is simply **agent**.

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
| Mountain, peak | Impassable and not buildable | Finished vision: mountain passable but slower, peak impassable; neither permits construction. Current pathing still blocks both |
| River, lake, ocean, fixture water | Impassable and not buildable | One-tile river crossing slower on foot; traffic may create a spaced bridge; wider water needs boats/ports |

The terrain renderer currently draws colored tiles and a few glyphs, not a
finished foliage/object art layer. Sources:
[map rules](../src/ClankerWorld.Simulation/Harness/SeededWorldHarness.cs),
[renderer](../src/ClankerWorld.GodotClient/UI/WorldTerrainLayer.cs).

## Intended content roles — **not implemented as described**

The **first Town** is created during paused New World setup. Its guaranteed
starting buildings are **two Houses, one Warehouse, one Farmhouse and one
Blacksmith**, joined immediately by generated Roads. The two starting
households can claim the productive buildings. Food portions in the Houses
and at least one usable wooden axe and wooden pickaxe are also guaranteed;
further counts and supplies are open. Other
buildings may develop later; none is guaranteed at New World creation.
Building inspection uses a panel for
occupants, stocks and ownership, not visible room interiors.

| Concept | Intended role | Current interview status |
| --- | --- | --- |
| House | Starts 1×1; storage-driven expansion to 1×2 or 2×2; household members and invited visitors, no occupant limit, private food/resources only accessible to members inside; cooking, storm refuge, childcare/property | **Decided role and footprints**; storage capacities, costs, invitation details and cooking recipes open |
| Warehouse | Starts 2×2; expands to 2×3 for more storage; Town-resident communal resources, physically inspectable stock | **Decided role and footprints**; capacities, costs, residency and border changes open |
| Workshop | 2×2 invention/mod venue, accessible to outsiders | **Decided role and footprint**; mechanics/code staged for late development |
| Farm fields + Farmhouse | Household plants/tends/harvests fertile fields; 1×1 or 1×2 Farmhouse processes crops | **Decided role and footprints**; population and yield inform expansion, but shortage can justify another farm; exact rule open |
| Farm Silo | Adjacent 1×1 private farm work stock | **Decided form/footprint**; capacity and access details open |
| Blacksmith | 1×2 or 2×2 household tool business, refines ore, sells tools and takes tool requests directly with internal stock | **Decided role and footprints**; selling refined metal to other crafts is proposed, recipes/tier progression open |
| Tailor Shop | 1×1 or 2×2 clothing-making business | **Decided role/name/footprints**; chain open |
| Store | Household-run 1×1 or 1×2 shop; goods must be carried there and stored on site before sale, with expansion supporting more storage | **Decided role and footprints**; capacities, costs and other economy rules open |
| Market | 2×2 main building; 1×1 stalls and roughly 10×12 reserved clear area; trading open to agents from any Town | **Decided role and footprints**; management and reservation rule details open |
| Restaurant | Optional agent-founded 1×2 or 2×2 business buying ingredients and selling cooked meals | **Decided role and footprints**; quality/pricing open |
| Clinic | 1×1 or 1×2 care business | **Decided role and footprints**; treatment chain open |
| Town Hall | 3×4 Town governance building | **Decided role and footprint**; timing/mechanics open |
| Port | Rotatable 2×4 access: one tile of length on land, three over water, with side docking clearance | **Decided role and footprint**; exact clearance/docking rules open |
| Roads and bridges | Fast Roads appear with Town buildings and connect Towns if a legal land route exists; traffic or a generated Road crossing may add bridges, with spacing against redundant bridges on the same crossing but not separate nearby streams; Roads survive building/Town loss | **Decided direction**; layout, removal edge cases, bridge threshold/radius open |
| Trees, stumps, seeds and regrowth | At most one harvestable tree per tile, with regrowth and planting | **Decided direction**; ecology rates/art open |
| Iron, gold, diamond, more materials/tools | Wood tools → stone → stone tools → iron → iron tools → rarer materials | **Decided starting ladder**; higher tiers and uses open |
| Livestock and mounts | Chickens/eggs, sheep/wool, cows/milk, horses/mounts | Accepted finished-game roster; husbandry and animal-product mechanics staged for late development |

## Full-game content roster — accepted base index

Computment wants a complete, concrete catalogue of final-game tools, items,
foods, objects and visual assets rather than a vague list of building roles.
The [finished-game asset roster](finished-game-asset-roster.md) now records the
**accepted base entries**, with explicit open details and late-development
mechanics. This sheet retains the current-versus-vision comparison. Do not
infer that an entry locks its recipe, quantity or final sprite.
For every eventual entry, record its source or recipe, tool gate, purpose,
storage/ownership, trade use, visual asset/variants, and whether it is a
starting item, later production, agent invention or late-game content.

| Category to enumerate | Already named in vision | Needs a deliberate roster pass |
| --- | --- | --- |
| Raw materials and deposits | Wood, stone, iron, gold, diamond, fiber, seeds | Exact variants, abundance, renewability, mining gates and art |
| Tools and equipment | Wood/stone/iron tool tiers, clothing | Individual tools, durability, crafting stations, bonuses and sprites |
| Crops and wild foods | Wild food, fertile fields, planted seeds | Plant/food species, yields, seasons, farm work and art |
| Meals and ingredients | House-cooked meals, processed farm food, Restaurant meals | Recipes, quality, food effects, ownership and art |
| Buildings and work sites | Role table above | Storage capacity/costs for decided House, Warehouse and Store footprints; designs, materials, footprints for other buildings, functions and appearances |
| World objects and infrastructure | Trees, stumps, Roads, bridges, ports | Object variants, growth/decay, placement and sprites |
| Trade and knowledge goods | Tools, food, maps, records and books | What can be sold, copied, learned, carried and displayed |
| Animals and transport | Livestock, mounts, boats | Detailed roster deferred until later development |

**Next pass:** select exact starter supplies and first-tier recipes; then fill
the accepted entries in dependency order. No invented costs, occupant limits
or recipe stats are approved here.
