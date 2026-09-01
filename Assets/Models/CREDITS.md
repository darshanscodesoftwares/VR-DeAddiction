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
