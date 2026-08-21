# Seated Table Scenario — Spec

User-stated requirements for the first interactive de-addiction scenario.
Recorded 19 Aug 2026. This is the source of truth for what was asked for;
design options and recommendations live separately.

## The scenario

A specific table in the bar is marked, game-style. Approaching it and standing
on the marker seats the patient at a detailed table carrying bottles, glasses,
water, and food. The point is to bring the felt reality of a bar to patients.

## Confirmed decisions

| Question | Decision |
|---|---|
| Transition style | **Seamless in-place.** No Unity scene load. The patient stays in the bar; the view moves and the table gains detail. |
| Bar lighting | All lights shift from cool white to **gloomy warm** — dimmer, no white. |
| Seated lighting | **Warmer and brighter at the table**, background darkened so the table and its contents are the focus. |
| Interaction gate | Nothing on the table is grabbable **unless seated**. |
| Posture control | The patient can **sit and stand at any time**, freely, in both directions. |
| Containment | Interactables must **not roll or be thrown off the table**. |

## Implications already agreed

- **Lighting transitions are cheap**: changing colour/intensity on existing
  lights is free at runtime. The seated focus light should be taken from the
  4-pixel-light budget by demoting a distant pendant, NOT added as a fifth --
  exceeding four pixel lights previously halved the framerate (72 -> 36 fps).
- **Background darkening should use fog**, not per-light dimming. Fog is free,
  pushes the room back visually, and leaves the table lit. Dimming 23 lights
  individually is slower and reads flatter.
- **Sit/stand needs two routes**, not one:
  - physical (headset height changes), and
  - explicit (button or gesture),
  because a patient may remain physically seated for the whole session --
  a wheelchair user could never trigger the physical route.
- **The transition must be interruptible.** Standing halfway through a fade
  reverses smoothly rather than completing first.
- Being able to leave at any moment is a **safety property**, not just
  convenience: a patient in cue exposure must be able to break out without
  hunting for how.

## Standing constraints this must respect

From the project's architecture rules:

- No new packages/assets/textures without explicit approval
- OpenXR only; no Meta SDK (must stay portable to VIVE/PICO)
- Must hold 72 fps on Quest 3S
- Code is the source of truth -- edit the procedural builders
- Session logic stays plain C# with no XR types, so it remains portable
- Patient identity from the app/backend, never biometric

## Open

- Which table becomes the marked one, and does the marker move between sessions?
- Session governance: maximum length, spacing, and the criterion for ending a
  course of sessions for a patient who is not habituating. Raised by the design
  review; unresolved.
