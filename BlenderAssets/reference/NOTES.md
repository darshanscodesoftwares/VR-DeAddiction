# Reference Notes — Batch 1 (19 Aug 2026)

Observations from photographs supplied by the user. These drive the Blender
asset work. Put the source images in this folder alongside this file.

---

## 1. Garden seating area (the key ambience reference)

The most useful image. Describes an **outdoor garden bar**, not an enclosed hall.

**Roof** — barrel-vaulted (curved arch, not pitched), covered in woven
bamboo/thatch matting laid in strips running along the curve. Supported on
slender **white-painted steel/concrete posts** with angled bracing. Weathered,
grey-brown, some straw hanging loose. Completely different structure from the
steel trusses currently in the scene.

**Fan** — white caged **wall/ceiling-mount oscillating fan** hung from the roof
frame, not a ceiling fan with exposed blades. Round protective cage, short stem.

**Chairs** — bright **red moulded plastic**, with a highly distinctive
**web/lattice pattern** in the backrest and seat — organic curved cells, like
cracked mud or a spider web. Armless, tapered legs. These are the signature
object in the frame and are worth modelling properly.

**Tables** — dark grey/near-black **laminate tops**, thin, with slim **black
steel folding legs**. Rectangular, low. Not the concrete tables currently built.

**Sofa** — worn **red vinyl/leatherette bench sofa**, low back, rolled arms,
visibly damaged with cardboard patching a torn corner. Sits against the wall
behind the tables.

**Surfaces** — **terracotta/red tile floor**, laid in squares. Grey-white
painted rendered wall, water-stained along the base. **Corrugated metal sheet**
fence behind, rusted.

**Signage** — large painted wall mural, green field, "THINK FRESH" and **7up**
logo with a red circle. Painted directly onto the wall, not a printed board.

**Planting** — dense tropical foliage: **areca palms**, bamboo poles, broad
leaves pressing in from both sides. Dappled daylight.

---

## 2. Food plate

**Steel/aluminium thali plate**, shallow rim, dull reflective. Holds fried
chicken (Chicken 65 style) with **curry leaves**, green chilli, fennel seeds.
Several **wooden toothpicks** standing in the pile. Small side plate with raw
onion.

Table surface: **mottled grey-brown stone/laminate**, heavily worn and stained.

Useful as a table-clutter prop and as a strong "local" signal.

---

## 3. Entrance gate with TASMAC board

**Signboard** — large **dark green horizontal board** on a steel frame spanning
the driveway. White text: Tamil "டாஸ்மாக்" above English **"TASMAC"**, then
"(அரசு அங்கீகாரம் பெற்றது)", "A/C & GARDEN BAR", and an address line
("66, G.P. Road, Royapettah, Chennai - 600 014"). A **white wine-glass graphic**
at each end.

**Decoration** — **orange/marigold flower garlands** strung in swags across the
board, plus coloured string-light wiring. A **floodlight** clamped on top,
angled down.

**Gate** — **vertical wooden slats**, yellow-brown, weathered, mounted in a
**black steel frame** with a lower cross-brace and a **wheel** at the bottom
(rolling gate, not hinged). Currently the scene has half-height hinged gates.

**Walls** — cream/white render, **heavily weathered**: black mould streaks,
peeling paint, water staining, exposed patches. **Jali ventilation blocks** set
into the wall. This weathering is the dominant visual character.

**Ground** — concrete/dirt, uneven, with **standing puddles**.

Overhead: **loose electrical wires** crossing the sky.

---

## 4. Street view

Multiple green boards clustered at the entrance:
- "டாஸ்மாக் TASMAC GARDEN A/C BAR" with a **beer bottle** graphic
- The main TASMAC arch board (as above)
- A backlit **light box**: pale blue sky background, a yellow hut/gazebo, a
  **frothy beer mug**, script text "Garden", and "A/c Bar" below
- A directional sign: "TASMAC WINE SHOP GARDEN BAR" with a **left arrow**

**Motorcycles/scooters** parked in a row along the entrance — a strong ambience
element currently missing entirely.

**Globe/pendant lights** glowing inside the entrance passage. Trees overhead.

---

## 5. Shop front (retail counter)

**Green boards** with Tamil text ("அரசு அனுமதி பெற்ற பார் வசதி உள்ளது") and
**bottle graphics**. A red/white TASMAC board with shop number (7107) and a
toll-free number.

**Service window** — **metal mesh/grille security door**, customers standing at
a small counter opening. This matches the existing teal grille counter, and
confirms the caged-service-window design is right.

**Green mesh fencing** panels. Corrugated awning. Parked scooters.

---

## Implications for the build

**Direction question.** The current scene is an *enclosed hall* — 12x20 m,
steel trusses, concrete tables, pitched corrugated roof. The references are
mostly an *outdoor garden bar* — barrel bamboo roof, plastic chairs, laminate
tables, sofas, heavy planting. These are different venues. Needs a decision
before modelling in bulk.

**High-value assets, in order:**

1. Red lattice plastic chair — signature object, 64 instances
2. Laminate-top folding table — replaces concrete tables
3. Caged oscillating fan — replaces exposed-blade ceiling fans
4. Green TASMAC signboard with wine-glass graphic
5. Wooden slat rolling gate
6. Steel thali plate + food props
7. Red vinyl sofa
8. Areca palms / bamboo
9. Motorcycles/scooters
10. Barrel-vault bamboo roof structure

**Weathering is the dominant character.** Mould streaks, peeling paint, water
staining, rust. Flat colours cannot carry this. Vertex colours baked in Blender
would get most of the way with no texture files and no new dependencies.

**Colour notes:** TASMAC green is a deep, slightly desaturated forest green.
Chairs are a saturated warm red. Floor tile terracotta. Walls cream going grey.

---

# Reference Notes — Batch 2 (19 Aug 2026)

## 6. TASMAC bar interior — THE KEY REFERENCE

This image **validates the existing build**. Almost everything already in
`PubEnvironmentBuilder.cs` is correct:

| Feature in photo | Already in the scene |
|---|---|
| Blue/teal steel trusses, zigzag web | Yes — `TrussCount = 6`, SteelBlue |
| Corrugated roof + translucent skylights | Yes |
| Ceiling fans on down-rods | Yes (6) |
| Pink/whitewashed brick walls, pilasters | Yes |
| Louvred ventilation windows high in wall | Yes |
| **Concrete tables** — thick slab top, solid block base | Yes (20) |
| **Plastic chairs in red / blue / green** | Yes (60) |
| Green banner on far wall | Yes |
| Bare concrete floor | Yes |

Differences worth fixing:

- Chairs are **monobloc plastic** — one moulded piece, curved back with a
  slotted/ribbed pattern, tapered legs, slight armrest flare. Currently built as
  boxes. Biggest single visual upgrade available.
- Table bases are a **solid concrete block/pedestal**, not two slab legs.
- Wall on the right is heavily **water-stained and mould-streaked**; the brick
  is coarse with visible mortar courses.
- Table props: **plastic water bottle** (branded PET, blue cap), **glass
  tumbler**, **ashtray**.

## 7. Outdoor drinking area

Stone flagstone paving, ochre wall, corrugated fence, tree branch overhead.

**Key prop:** a wooden table crowded with **~8 green quart beer bottles**
(650 ml Indian style — tall, green glass, paper label around the body, gold
foil neck). This is the definitive TASMAC drinking vessel and is far more
representative than spirit bottles. Steel plates of food between them.

Men on plastic chairs. Foreground concrete table with a paper bill and a phone.

## 8. Eatery / mess interior (blue walls)

- **Menu board** — large dark board, two columns of Tamil item names with
  prices in yellow/white. Very distinctive; currently the scene has a generic
  menu board.
- **Pepsi-branded cooler** mounted on the wall
- Round wall clock, small **shrine picture with garland**, paper calendar
- Steel serving counter lined with **round steel vessels** of food
- **Plastic stools** in blue, red, orange, teal — low, moulded, no back
- Large plastic **tubs/buckets** in orange, white, blue
- Worn wooden tables, concrete floor

## 9-11. Spirit bottle studies (Western brands)

Bacardi, Plantation, Smith & Cross, Crown Royal, Jägermeister, Malibu,
Italicus, Zirbenz, Amaro Montenegro, Mr Black, St-Germain.

These are **not TASMAC-typical stock** but they are excellent silhouette
references for the shelf behind the counter, giving variety in:

- Tall straight-sided with high shoulder (Bacardi, Smith & Cross)
- Squat with sloped shoulder (Plantation)
- Square-section (Jägermeister, Mr Black)
- Opaque white (Malibu)
- Ribbed/fluted glass (Italicus)
- Curved "hip flask" profile (Crown Royal)

Cork-and-wood tops, screw caps, foil necks all appear — worth varying.

## Revised asset priority

1. **Monobloc plastic chair** (red / blue / green) — 60 instances, closest to
   the player, currently boxes. Highest impact by a wide margin.
2. **Green quart beer bottle** — the defining object, and grabbable.
3. **Concrete table** — solid pedestal base, worn slab top.
4. **Steel tumbler** and **glass tumbler**.
5. **Spirit bottle set** — 4-5 silhouettes for the counter shelf.
6. **Steel thali plate** + fried snack.
7. **PET water bottle**, ashtray, paper bill.
8. **Menu board** with column layout.
9. **Plastic stool**.
10. Caged wall fan, wall shrine, clock, calendar.
