# Third-party model credits

## Jack Daniel's Whiskey No.7 Bottle

- Source: Sketchfab, "Jack Daniels Whiskey No.7 Bottle" by **3DDave247**
- Licence: **Creative Commons Attribution (CC-BY)** — attribution required,
  commercial use permitted
- File: `Assets/Models/Asset_JackDaniels.fbx`
- Textures: `Assets/Textures/JD_Label.jpg`, `Assets/Textures/JD_Cap.png`

The mesh committed here is a decimated derivative: 1,518,458 triangles reduced
to 11,999 and scaled to a life-size 0.29 m, produced by
`BlenderAssets/scripts/jackdaniels.py`. The original 61 MB FBX is deliberately
NOT in this repo — it is a render asset, not a game asset. Re-run that script
against a fresh download to regenerate.

**Trademark note:** "Jack Daniel's" is a registered trademark of the Jack
Daniel Distillery. Real brand cues are used here because brand recognition is
the therapeutic mechanism in cue-exposure work. Clearance is a question for any
commercial release, not for this prototype.

---

## Footstep audio

- File: `Assets/Audio/Footsteps/Step_{L,R}*.wav`
- Source: "walking sound effect" (`u_3x9ga8wevj-walking-sound-effect-272246.mp3`),
  downloaded by the project owner from a free sound-effects site
- **Licence not yet recorded — confirm before any release.** It was taken from a
  free download rather than a subscription library, but the exact terms have not
  been captured here.

Derived, not used as-is: a single 4.32 s walk cycle was decoded, its nine
footfalls located by transient detection, and each cut, normalised and
tail-faded into a separate clip by hand-run tooling. Odd footfalls became the
left bank and even the right, so the two feet are genuinely different recordings
rather than one sample played twice.

## Grab clink

- File: `Assets/Audio/Grab/Clink.wav`
- Source: `freesound_community-bottle-clink-101000.mp3`, from the Freesound
  community collection
- **Confirm the exact Freesound licence before release.** Freesound items are
  variously CC0, CC-BY or CC-BY-NC, and the three have very different
  obligations. CC-BY-NC would rule out commercial use entirely.

Derived: the source is 5.18 s, of which only the first ~0.35 s is sound and the
rest digital silence. Trimmed to 0.42 s, normalised and tail-faded.

---

## Ground texture

- Files: `Assets/Textures/Ground_Dirt.png`, `Ground_Dirt_Normal.png`
- Source: **ambientCG "Ground 109"** — https://ambientcg.com/a/Ground109
- Licence: **CC0**. No attribution required; recorded here for provenance only.

Derived: the 2K Color and NormalGL maps, downscaled to 1024 because
TextureImportRules caps non-sky textures there anyway. NormalGL, not NormalDX --
Unity expects the OpenGL convention. Tiled at 2.8 m, the capture's stated
real-world size, so the pebbles are life-size.

## Round bar table

- File: `Assets/Models/Asset_RoundTable.fbx`, `Assets/Textures/Table_Wood*.png`
- Source: Sketchfab, "Round Bar Table" by **TabbieCat**
- **Licence not recorded — confirm before release.**

Derived: 1,242 triangles as downloaded, not decimated. Scaled from its native
0.945 m bar height to the project's 0.775 m table height, which takes the
diameter to 0.734 m. Textures are the 2K BaseColor and Normal at 1024.

## Sky

- File: `Assets/Textures/Sky_Overcast.png`
- Source: "PS1 cloudy skybox" (`ps1-cloudy-skybox-2`, texture `nightsk.png`)
- **Licence not recorded — confirm before release.**

Derived: the source is a 4-bit PALETTE-INDEXED image -- 16 colours faking a
gradient with ordered dithering, which in a headset reads as a sky made of dots.
De-dithered by a wrapped Gaussian blur (a dither pattern's local mean IS the
colour it approximates), contrast restored, saved as 8-bit RGB.

## Hall floor

- Files: `Assets/Textures/Floor_Concrete.png`, `Floor_Concrete_Normal.png`
- Source: **ambientCG "Concrete 034"** — https://ambientcg.com/a/Concrete034
- Licence: **CC0**. No attribution required; recorded for provenance.

Derived: the 2K Color and NormalGL maps at 1024, tiled every 2 m across the
12.4 x 20.4 m slab.

## Cardboard cartons

- Files: `Assets/Models/Asset_CardboardBox.fbx`,
  `Assets/Textures/CardboardBox.png`, `CardboardBox_Normal.png`
- Source: **"Low Poly Cardboard Box"**, downloaded as `lowpoly-cardboard-box`
  (44 triangles, 24 verts, one UV map)
- **Licence not recorded — confirm before release.** The download contained no
  licence file.

Derived by `BlenderAssets/scripts/cardboardbox.py` from
`BlenderAssets/source/CardboardBox.fbx`: scaled uniformly from the 2 m cube it
ships as down to 0.55 m, and the base re-seated on z = 0.

Uniform scale on purpose. The cases these replace were 0.66 x 0.36 x 0.66, and
squashing the model to that would have stretched the printed cardboard texture
and folded the flap geometry flat.

The source's `Box_Height.png` is not used — the Built-in Standard shader's
parallax needs a height map packed differently, and at 0.55 m across a carton
gains nothing from it.

## Drinks shelf

- Files: `Assets/Models/Asset_DrinksShelf.fbx`,
  `Assets/Textures/DrinksShelf.jpg`, `DrinksShelf_Normal.jpg`
- Source: **"Supermarket Drinks Shelf"**, downloaded as
  `supermarket-drinks-shelf-asset` (105,652 triangles as published)
- **Licence not recorded — confirm before release.** No licence file in the
  download.

Derived by `BlenderAssets/scripts/drinksshelf.py` from
`BlenderAssets/source/DrinksShelf.fbx`: collapse-decimated 105,652 -> 16,000
triangles, re-origined centred in width and depth with its base on z = 0.

Decimated to 3,893 first, which was too far: the bottles are small and densely
packed, so they are the first thing collapse decimation eats, and every one
collapsed into a triangular smear. 16,000 keeps the stock readable and was
affordable once the scene was holding a locked 72 fps.

Its stocked face is Blender -Y, which the FBX axis conversion delivers as Unity
+Z -- so against the back wall it needs **yaw 180**, not 0. Placed the other way
the back panel faces the room.

Its Metallic and Roughness maps are unused — the Built-in Standard shader wants
them packed into one metallic-smoothness texture, and two more 1024 maps on a
back-wall prop is not worth it. Colour and normal only.

**Contains real, legible alcohol brand labels** (the scan is of a real
supermarket chiller). Every other drink surface in this project is deliberately
unbranded — see the "generic red - no branding reproduced" note on the coolers in
`PubEnvironmentBuilder.cs`.

**Decided on 3 Sep 2026: keep the branding, unaltered.** Asked directly, the
project owner chose to ship the labels as scanned rather than blur or repaint the
atlas. This is a deliberate exception to the no-branding rule and applies to this
asset only — the coolers, bottles and signage stay generic. Revisit it alongside
the outstanding licence question, since reproducing trade dress and reusing the
scan are separate permissions.

## Counter timber

- Files: `Assets/Textures/Counter_Wood.png`, `Counter_Wood_Normal.png`
- Source: **ambientCG "Wood 027"** — https://ambientcg.com/a/Wood027
- Licence: **CC0**. No attribution required; recorded for provenance.

Derived: the 2K Color and **NormalGL** maps (not NormalDX — Unity wants the
OpenGL convention), tiled 4x along the counter's 6.6 m length so a plank reads
at about 1.7 m. At 1:1 a 2K sheet stretches its grain over the whole span and
looks like a photograph of wood rather than boards.

Applied to every part of the counter — kick, base, top slab, chamfer lip,
drinking ledge, and the return wing with its own kick. The teal ledge brackets
stay metal. The counter was cast concrete before this.

Unused: Displacement and Roughness. The Built-in Standard shader wants roughness
packed into a metallic-smoothness map, and parallax on a counter edge is not
worth a third 1024 texture on a Quest.

## Building walls

- Files: `Assets/Textures/Brick_Wall.png`, `Brick_Wall_Normal.png`
- Source: **ambientCG "Bricks 090"** — https://ambientcg.com/a/Bricks090
- Licence: **CC0**. No attribution required; recorded for provenance.

Derived: the 2K Color and NormalGL maps, applied to every wall of the building
inside and out — both side walls, the back wall, both front segments, the
entrance lintel and the piers. The side walls were whitewashed brick and the
back and front were pink plaster; both are now this brick.

NOT brick, deliberately: the door leaves and frames, the dado band, and the
entrance jambs.

Tiled at **2 m per repeat via `BrickWall(width, height)`**, which builds one
material per distinct wall size. Unity's cube UVs run 0-1 per FACE, so a single
tiling value would print bricks five times larger on the 20 m side wall than on
the 4 m lintel and the building would stop reading as masonry. Sizes of 1.5
repeats or more are rounded to whole numbers so the pattern wraps seamlessly;
anything smaller keeps its exact fraction, because forcing a 0.5 m pier up to one
whole repeat is a worse error than a seam nobody can see on a 0.5 m strip.

## Street

- Files: `Assets/Textures/Road.png`, `Road_Normal.png`
- Source: **ambientCG "Road 008 C"** — https://ambientcg.com/a/Road008C
- Licence: **CC0**. No attribution required; recorded for provenance.

Derived: the 2K Color and NormalGL maps, **rotated 90 degrees**, tiled at 4 m per
repeat from the street's real dimensions (8.125 x 2.5 over 32.5 x 10 m).

The rotation is the whole story. The texture's yellow lane markings run up its V
axis, and a Unity cube's top face maps V to Z -- the road's 10 m depth -- so
applied as shipped the markings lay ACROSS the carriageway like a pedestrian
crossing instead of along it.

**The normal map could not simply be rotated with it.** Turning a tangent-space
normal map rotates the surface but not the vectors stored in it, so its R and G
channels were remapped (R' = 1 - G, G' = R) to match the 90 degree turn. Without
that every bump is lit from the wrong side -- a mistake that looks fine when you
open the file and is wrong only once it is in the scene.

Both were processed in Blender with the colorspace forced to Non-Color on read
and write, so values round-trip untouched; left as sRGB the transform is applied
twice and the asphalt comes back washed out.

## Entrance doors

- Files: `Assets/Textures/DoorSteel.png`, `DoorSteel_Normal.png`,
  `DoorSteel_NormalBack.png`
- Source: **ambientCG "Corrugated Steel 007 B"** —
  https://ambientcg.com/a/CorrugatedSteel007B
- Licence: **CC0**. No attribution required; recorded for provenance.

Derived: the 2K Color and NormalGL maps, plus a **generated back-face normal**.
No rotation needed here — the corrugations already run up the texture's V axis
and the rust streaks run down it, which is the correct orientation for a door.

`DoorSteel_NormalBack.png` has X and Y negated (R' = 1-R, G' = 1-G), because the
reverse of a corrugated sheet is the NEGATIVE of its front: a groove wherever the
front has a ridge. A box carries one material, so each leaf is the slab plus a
12 mm skin on its reverse using this second material. Sharing one normal map
across both faces would bulge the ridges toward the viewer from either side,
which is physically impossible and reads as a printed sticker rather than metal.

Tiled from the leaf's real size (2.02 x 2.54 m) at 1 m a repeat, giving 2 x 2.54
— 1.01 m across and 1.00 m up, so the corrugations are not stretched. The ACROSS
count is rounded to a whole number so a corrugation is never sliced in half at
the door's edge; the vertical is left exact, where the rust streaking has no
repeating feature to misalign.

The doors were steel-blue frames with raised wooden panels before this. Metalness
and roughness maps unused; `_Metallic` is a flat 0.30, old painted sheet rather
than bare steel.

## Ceiling pendants

- Files: `Assets/Models/Asset_BarLamp.fbx`, `Assets/Textures/BarLamp.png`,
  `BarLamp_Normal.png`
- Source: **"Bar Ceiling Light"**, downloaded as `bar-ceiling-light-2048px2`
  (8,296 triangles as published)
- **Licence not recorded — confirm before release.** No licence file in the
  download.

Derived by `BlenderAssets/scripts/barlamp.py`: the body and bulb meshes JOINED
with the body active, so slot 0 is the lamp and slot 1 the bulb, letting Unity
drive the bulb with the existing emissive material instead of a painted-on
highlight. Decimated 8,296 -> 2,000 triangles, six of them in the hall.

Its origin is the top of the stem -- the point that meets the ceiling -- so a
lamp placed at a mount height hangs from it with no offset arithmetic. The script
re-seats z = 0 on that point anyway, because a silent 1 cm error would hang every
lamp wrong.

Unused: Metallic and Roughness maps.

## Exterior wall lights

- File: `Assets/Models/Asset_WallLight.fbx`
- Source: **"alton wall light"** (`~/Downloads/source 3`), 1,444 triangles
- **Licence not recorded — confirm before release.** No licence file supplied.

Derived by `BlenderAssets/scripts/walllight.py`. No textures ship with it, only
Maya lamberts, so the body is a flat weathered brass and the emitter strip is
kept as a separate MATERIAL SLOT driven by the emissive bulb material — so the
fixture reads as switched on rather than painted.

Two corrections were needed before it could be placed by coordinate:

- Its meshes hang off an `alton_light` EMPTY carrying Maya's 0.01 centimetre
  scale, and `transform_apply` on a CHILD does not include its parent's
  transform. Applied without unparenting first it exports 100x oversized — a
  39 metre light fitting.
- Its origin is a bounding-box CORNER, not the mounting point. The script moves
  it to the centroid of the wall plate (every vertex within 5 mm of minimum Y),
  so a fixture placed AT a wall surface point mounts flush to it.

Placed five times: over the entrance and at both ends of each side wall. The
light is put at the SHADE, 0.28 m out from the wall and 0.13 m down, not at the
plate — a light left at the mount would pour out of the brickwork behind it.

