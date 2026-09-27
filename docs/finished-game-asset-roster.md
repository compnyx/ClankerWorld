---
title: Finished-Game Asset Roster — Proposed Base Catalogue
type: design-review
status: proposal
updated: 2026-09-27
---

# Finished-game asset roster — first review draft

This is a **proposal for the first complete game**, including content that is
not in the prototype. It is a reviewable picture of what the world would
contain, **not** an approved shopping list, art-production order, recipe book,
or claim that these assets already exist. The [vision ledger](vision-interview.md)
is authoritative for computment's decisions; the [content catalogue](content-catalog.md)
compares those decisions with current content. Numbers below identify things to
react to, not implementation IDs.

**Status key:** **D** = the *role or direction* is already decided in the
vision; the particular name/species/recipe may still be a proposal. **P** =
Clanker's proposed addition or concrete choice. **O** = known role, but its
exact roster is deliberately open. A **D/P** row has a decided role and
proposed examples. Nothing marked P becomes canon merely by appearing here.

**Asset** means a player-visible world sprite/animation, inventory icon,
interface graphic, text/content template, or visual effect. A crop can need a
plant sprite, harvested-item icon, and cooked-food icon. A House needs exterior
footprint art and an inspection panel, **not** an enterable room interior.
One standard appearance per supported design is enough; do not multiply every
building by arbitrary roofs, colors, materials, and states. Agent appearance is
the intended exception. Variants listed for direction, growth, damage or
weather are functional states, not cosmetic skins.

## 1. Ground, water, and climate

| # | Status | Base asset family | Proposed visible set and purpose |
| --- | --- | --- | --- |
| 1.1 | D/P | Ground surfaces | Meadow/grass, forest floor, sand/beach, dry scrub, rocky upland, snow/tundra, and fertile/cultivated soil. Climate, elevation, surface and vegetation remain separate data, not one tile type per combination. |
| 1.2 | D/P | Water | Ocean, lake, continuous river, shoreline/water edge; shallow one-tile river crossing distinct enough to read. Depth/flow variants only if they affect crossing rules. |
| 1.3 | D/P | Elevation | Mountain and peak terrain silhouettes/edges; unmistakably unsuitable for construction. Exact travel treatment is still open. |
| 1.4 | D/P | Ground transitions | Coasts, river banks/corners, grass–sand, grass–snow, and rocky boundaries; seam-safe east/west wrapping where enabled. |
| 1.5 | D/P | Seasons and weather | Spring/summer/autumn/winter palette treatment; clouds, rain, snow and a severe storm effect. Weather is regional. Night affects temperature/weather, not sleep, travel, visibility, work or social rules; a cosmetic night tint is only a proposal. |
| 1.6 | P | Small terrain detail | Grass tufts, scattered stones, fallen leaves, and shoreline foam, as non-resource decoration. No extra gameplay requirement. |

The proposed surface list is a **small art vocabulary**, not a rigid list of
natural biomes. Forests and mountain regions should visibly appear in default
worlds; cacti, grass, stone and snow should suit their generated climate.

## 2. Harvestable plants, deposits, and farm visuals

| # | Status | World object / stages | Harvested item or role |
| --- | --- | --- | --- |
| 2.1 | D/P | Broadleaf tree stand: sapling → mature stand → stump → regrowth | Wood; plantable tree seed. A dense stand may be one resource object depicted as several trees. |
| 2.2 | D/P | Conifer tree stand, same functional stages | Wood; same functional tree-seed category unless species difference proves useful. |
| 2.3 | P | Berry bush: fruiting, picked, regrowing | Berries as wild food. |
| 2.4 | P | Wild edible greens patch: growing, picked | Greens as a second forage source. |
| 2.5 | P | Fiber plants/reeds: growing, harvested | Plant fiber for cloth, rope and basic crafting. |
| 2.6 | D/P | Stone outcrop: intact, depleted | Stone; finite deposit. |
| 2.7 | D/P | Iron ore vein, gold vein, diamond deposit | The named advanced materials; exact mining gates, abundance and uses remain open. They need distinct readable deposit and item icons. |
| 2.8 | P | Clay bank | Clay for pottery/storage construction, if an earthenware chain is wanted. |
| 2.9 | P | Grain field: prepared soil, seeded, sprout, mature, harvested | Grain, grain seed. Candidate: wheat-like grain; no species fixed by the existing vision. |
| 2.10 | P | Root-vegetable field: same functional stages | Roots, root seed. Candidate: potato or carrot; choose one for the base set. |
| 2.11 | P | Leafy-vegetable field: same functional stages | Greens, vegetable seed. Could share the wild-greens item if unnecessary duplication is avoided. |
| 2.12 | P | Orchard fruit tree: growing, fruiting, picked | Fruit. An orchard is optional; it would add a longer-term farm investment. |

**Decision to review:** the compact base-food proposal is berries, greens,
grain, one root vegetable, and possibly orchard fruit. It deliberately avoids
dozens of crops that would differ only in icon. Crop yield, seasonality, seed
rules, soil moisture, spoilage and farm count still need design.

## 3. Raw materials and processed goods

| # | Status | Carryable item/icon | Source → use |
| --- | --- | --- | --- |
| 3.1 | D | Wood | Trees → tools, buildings, cooking fuel and transport. Whether logs/planks are separate items is open. |
| 3.2 | D | Stone | Outcrops → stone tools and construction. |
| 3.3 | D | Iron ore / iron | Iron deposit → Blacksmith metal; whether ore and refined metal are two inventory items is open. |
| 3.4 | D | Gold / diamond | Named rarer materials. No assumed mining tier, currency role or automatic weapon upgrade. |
| 3.5 | D/P | Plant fiber / cloth | Fiber plants → clothing-making business. Cloth as an intermediate item is proposed. |
| 3.6 | D/P | Seeds | Tree seed; grain and vegetable seeds if those crops are accepted. The generic prototype `seed` item need not remain one universal seed. |
| 3.7 | P | Rope | Fiber → boat/building/transport ingredient; cut if it only adds inventory friction. |
| 3.8 | P | Clay / pottery | Clay bank → optional storage vessel or trade good. Not a required technological age. |
| 3.9 | P | Leather / wool | Possible later animal products, contingent on the animal and husbandry design; **not** assumed at the start. |

Use a **specific icon for each actual item**, including a generic fallback for
valid agent-invented materials. Do not commit to copper, bronze, coal, steel or
an endless ore ladder merely because they are conventional crafting-game items.

## 4. Tools, work gear, and starter package

| # | Status | Item family | Proposed jobs and representation |
| --- | --- | --- | --- |
| 4.1 | D/P | Axe | Wood gathering and tree work; wooden starter version, then stone and iron versions if they meaningfully improve work. Can also be a weapon under the combat design. |
| 4.2 | D/P | Pickaxe | Stone and ore extraction; wood → stone → iron progression. Gate advanced deposits by actual tool capability, not just icon color. |
| 4.3 | P | Hoe | Preparing and tending farm fields; wooden and iron versions only if both have meaningful differences. |
| 4.4 | P | Hammer | Building and Blacksmith work; share a basic form where possible rather than multiplying tiers. |
| 4.5 | P | Sickle | Crop harvest; optional if hand harvesting already gives a good loop. |
| 4.6 | P | Knife | Food preparation and general craft; optional dual-use combat item. |
| 4.7 | D/P | Everyday clothing | One basic outfit per agent appearance set; clothing can protect against weather. Cold/wet upgrades are proposed, not yet a decided gear ladder. |
| 4.8 | P | Carry aid | Basket, sack or handcart for physical stock transport. Exact carrying limits and whether carts exist are open. |

**Proposed first-Town guaranteed kit:** food portions stored in the two Houses,
plus at least one usable wooden axe and one usable wooden pickaxe available to
the four starting agents. Exact counts, ownership, placement and how agents
handcraft replacements without a Workshop or Blacksmith remain **open**. The
guarantee of food and some tools is decided; this particular pair is not.

## 5. Food, cooking, and care

| # | Status | Carryable item / visual | Proposed source and role |
| --- | --- | --- | --- |
| 5.1 | D/P | Wild berries; wild greens | Gathered food; distinct fresh-food icons. |
| 5.2 | P | Raw grain; root vegetable; leafy vegetable; orchard fruit | Harvested farm foods if the proposed crops are accepted. |
| 5.3 | P | Flour | Farmhouse-processed grain; include only if a meaningful processing/trade step. |
| 5.4 | D/P | Simple cooked meal | House-cooked from available food; a generic meal icon avoids requiring a bespoke recipe for every ingredient mix. |
| 5.5 | P | Porridge; bread; vegetable stew | Three concrete advanced foods to give grain/vegetables and Farmhouse/Restaurant distinctive uses. Exact ingredients, nutrition and quality are open. |
| 5.6 | D/P | Restaurant meal | Prepared and sold on site, potentially better than a basic House meal. A plated-meal icon is enough until menu variety matters. |
| 5.7 | P | Milk; eggs | Only if corresponding livestock are accepted. No meat or hunting loop is assumed. |
| 5.8 | O | Medicine / care goods | Illness and combat injuries exist in vision, but whether treatment needs a physical item, herb, bandage or medicine is open. Proposed minimal icons: bandage and medicinal herbs **if** item-based treatment is chosen. |

Food is physically stored in **Houses** or a business's own stock, not the
Town's resource Warehouse. Stores cannot sell food from a remote House. The
food roster should support daily life without forcing agents into constant
meal production; no calorie/energy meter is proposed here.

## 6. Buildings, fields, and infrastructure

Each row needs one exterior per **supported footprint**, a tiny map/overview
symbol, construction/repair states only where those states actually exist,
and an inspection-panel identity/icon. There are **no room-interior assets**.

| # | Status | Building / footprint | Finished-game function and art |
| --- | --- | --- | --- |
| 6.1 | D | House: 1×1, 1×2, 2×2 | Three readable exterior footprints; private household food/resources, cooking and storm refuge. Expansion raises storage, **not** occupancy. No beds or sleeping props required. |
| 6.2 | D | Warehouse: 2×2, 2×3 | Two exterior footprints; physical communal **non-food** resource stock for Town residents. |
| 6.3 | D | Store: 1×1, 1×2 | Two exterior footprints; household shop with goods physically delivered and stocked there before sale. |
| 6.4 | D | Farmhouse | Crop processing and farm household activity. Footprint and design open; propose one modest exterior. |
| 6.5 | D | Farm fields | Prepared/seeded/growing/ready/harvested states for each accepted crop, not visible building interiors. |
| 6.6 | D/P | Private farm Silo or linked store | Farm work stock; propose a small Silo exterior, but whether it is separate or attached remains open. |
| 6.7 | D | Blacksmith | Tool-making business and on-site work stock; forge/anvil silhouette on the exterior. |
| 6.8 | D/P | Clothing-making shop | Dedicated business for clothing; **Tailor Shop** is a proposed name, not yet selected. |
| 6.9 | D | Workshop | Inventions/mods and work access for outsiders. Do not imply it is guaranteed in the first Town. |
| 6.10 | D | Market | Trading venue open to agents of any Town; whether stalls are separate placeable objects is open. |
| 6.11 | D | Town Hall | Civic governance, laws and elections; exact footprint open. |
| 6.12 | D | Port | Shore-connected boarding/boat access; exact dock footprint open. |
| 6.13 | D | Restaurant | Optional agent-founded food business with on-site ingredients and meals; no required starter Restaurant. |
| 6.14 | P | Clinic / healer's shop | Possible later care business if injuries/illness warrant a physical treatment venue. Not yet part of approved building catalogue. |
| 6.15 | D | Road | **One** road type; straight, corner, junction, end, diagonal and connection pieces as needed for readable automatic routes. Roads remain after their building or Town disappears. |
| 6.16 | D | Bridge | Narrow river crossing sprite(s); generated from traffic, not hand-painted by player. Separate nearby streams may each need a bridge. |

**Do not include as base buildings:** Shelter, Storehouse, separate Cooking
fire/Campfire, Stone hearth, Weaving frame, bed/bedroll, or the player-facing
Create workbench. No village/city visual tiers are implied; all places are
**Towns**. Building costs, storage capacities, most footprints and material
alternatives are not fixed by this roster.

## 7. Agents, animals, transport, and combat

| # | Status | Visible assets | Proposed minimum visual behavior |
| --- | --- | --- | --- |
| 7.1 | D/P | Agent sprites | Infant, child, adult and elder silhouettes; a few appearance variants per stage without genders. Walking in cardinal/diagonal directions, working, carrying, talking, eating/cooking and basic hurt states as needed. No sleep cycle. Exact animation frames are open. |
| 7.2 | D/P | Social indicators | Chat bubble, selected/hover state, household/Town affiliation and conversation popup. Family lines, partnerships and deceased profiles belong in the family-tree UI, not automatically on the ground sprite. |
| 7.3 | D/P | Boat | One small crafted boat and an occupied/cargo state for water travel through ports; improved craft can be agent inventions. Exact water/boat rules open. |
| 7.4 | D/P | Livestock | Proposed compact set: chicken (eggs), sheep (fiber/wool), cow (milk). These species/products are **not** decided; husbandry detail is intentionally deferred. |
| 7.5 | D/P | Mount | Proposed horse with rider/cargo states. The animal and riding mechanics are not yet decided. |
| 7.6 | D/P | Weapon | Axe already appears as a work tool. Proposed additional spear and simple sword to make combat legible; avoid an arbitrary weapon glut. Their materials and lethality are open. |
| 7.7 | D/P | Protection | Proposed shield and basic armor/clothing layer. Whether protective equipment is separate from clothing remains open. |
| 7.8 | O | Injury and recovery | Hurt/treated indicators; physical bandage/herb assets only if medicine is item-based. No hostile predator sprites in the current plan. |

Animal slaughter, hunting, cavalry/war specialization, and named armor tiers
are **not** assumed. Combat exists for interpersonal self-defense, crime, feuds
and war; it is not a reason to add hostile wildlife by default.

## 8. Knowledge, law, money, and invented content

| # | Status | Asset family | Proposed representation |
| --- | --- | --- | --- |
| 8.1 | D/P | Map; written record; book | Carryable/readable knowledge goods to record discoveries and trade knowledge. Three icons; contents and who actually knows them are data, not art. |
| 8.2 | D/P | Law and property record | Inspectable Town law, claim, ownership, agreement and inheritance/will screens. A document icon can be reused; these need not all become physical inventory items. |
| 8.3 | D/P | Currency design | A coin/token/note visual family for **agent-created currencies**. No universal starting money or preselected gold standard. |
| 8.4 | D/P | Agent invention/mod package | Icon/thumbnail, author, validation/failed/active state, dependency/rights info; world and personal-library views. New valid designs may bring their own approved PNG assets. |
| 8.5 | P | Generic fallback asset | Clear placeholder silhouette/icon for a valid invention whose final art is unavailable; appearance cannot secretly add behavior or bypass content validation. |

The base roster is finite; **agent inventions are intentionally not enumerable**.
They may add buildings, tools, crops, machines, art, laws, currencies and more,
subject to valid gameplay and visual representation. Do not interpret the base
list as a cap on invention, or fabricate every possible invented sprite now.

## 9. Interface and presentation assets

| # | Status | Visual family | Must cover |
| --- | --- | --- | --- |
| 9.1 | D/P | Main Menu and New World | Background, buttons for Continue/New World/Load World/Settings/Mod Library/Quit Game; seed preview, size/climate/advanced controls, rough first-Town site and layout accept/redo, four-agent setup and Start World progress. |
| 9.2 | D/P | World HUD | Top-bar Map, pause/resume, date/time, population, World Info, Filters, Event Log, Add Agent and Pause Menu; tile/agent hover/selection markers. |
| 9.3 | D/P | Overview and filters | Map frame/camera rectangle; biome/terrain and local weather legend; established Town/household boundaries, property and similar fact overlays. No player fog-of-war texture is needed. |
| 9.4 | D/P | Inspection panels | Tile facts; agent profile, private thoughts, Memories, inventory, relationships, model/provider controls, deceased profile and Family Tree; building occupants/stock/ownership. |
| 9.5 | D/P | Economy/work | Item icons used consistently in inventories, stock panels, farm/work progress, Store/Market/Restaurant trade, and offers. Actual physical location and ownership must be visible. |
| 9.6 | D/P | Events and conversation | Event Log location markers, chat bubble, conversation summary/full view, decisions, law/election and invention notices inside their relevant panels. No optional out-of-view pop-up notice system. |
| 9.7 | D/P | Pause, settings, save, mods | Compact Pause Menu: Save World, Settings, Mod Library, **Quit to Menu last**; no Quit Game there. Game/World Settings, confirmation dialogs, save compatibility, AI-usage meter/limit and mod states. |
| 9.8 | P | Typography and common UI kit | One readable pixel-compatible font system, panel frames, 9-slice pieces, buttons, focus/disabled/error states, tooltips, scrollbars and icon legend. Exact look and accessibility treatment open. |

All important information remains readable in **text**. Generated voice,
music, ambient sound and sound effects are **outside the first complete game's
scope**; this roster therefore has no audio production list.

## 10. Cross-cutting visual states and production rules

- **World objects:** normal/depleted/regrowing only where the world actually
  tracks those states. Seasons and weather should use a shared treatment, not
  a full duplicate sprite for every climate × season × time combination.
- **Items:** one inventory icon per distinct item; ground/carry representation
  only when that item can actually appear there. No invented item has a free
  stock transfer: goods must be carried to Store stock before sale.
- **Buildings:** support the decided footprint sizes exactly. Add construction,
  expanded, damaged or abandoned states only if the simulation can produce
  them; do not multiply every building by speculative decorative variants.
- **Scale/style:** 32×32 logical ground tiles and PNG runtime assets are the
  initial decided standard; taller agents/trees and multi-tile buildings can
  exceed a tile with transparent anchored art. Palette, perspective, sprite
  resolution for characters, animation count and a reference scene remain open.
- **UI/icon parity:** an asset's name, icon and inspectable facts should refer
  to the same actual item or object. Pixel art does not make a fixture-only
  capability playable.

## Review decisions this draft exposes

The owner can accept/reject individual numbered rows or whole families. The
largest choices are: **(a)** compact crop/food set; **(b)** exact starter tool
types and where replacements are made; **(c)** whether clay/pottery, rope,
orchards and carry aids earn their complexity; **(d)** animal species/products;
**(e)** extra weapons, armor and a care building; **(f)** exact footprints and
storage capacities for buildings other than the decided House/Warehouse/Store;
and **(g)** art style/perspective and how invented designs obtain art.

After roster approval, each retained item/building can get a production row:
**source or recipe, tool gate, work site, use, location/ownership, trade role,
icon/world sprite, functional states, and current implementation status**.
Those values are intentionally not fabricated in this first visual pass.
