# Base Skid — Modeling Rule Set

**Project:** AI-TEKLA 2025 — Modular Building / Pod Modeling (Section 14)
**Sub-module:** Base Skid (steel base frame for toilet pods)
**Status:** CONFIRMED — cross-verified against fabrication drawing + IFC model
**Verified against:**
- Architecture reference: `POD_PLAN.pdf` (Bathroom Pods layout)
- Fabrication drawing: `BP3-BF1_-_BASE_FRAME_-_Rev_00.pdf` (Tekla, Issued for Fabrication, Project MARINE-1-PODS)
- Structural model: `POD-IFC.ifc` (Tekla Structures 2025 SP3, IFC2X3 export) — parsed with `ifcopenshell`, geometry checked against BOM
**Date confirmed:** 2026-09-17

> This document is a structured rule reference for the Modeling Rule Engine (see master plan Section 13). It describes a *repeatable pattern*, not a fixed one-off geometry. Dimensions used as examples below come from the one verified instance (BP3-BF1); the rules themselves are written to generalize to other pod footprints, door counts/widths, and fixture layouts.

---

## 1. Profiles Used

| Role | Profile | Material | Color code | Presence |
|---|---|---|---|---|
| Main perimeter frame | RHS 120×60×4.0 | S275 | 🟩 Green | Always present |
| Door beam (spliced into perimeter) | RHS 60×30×4.0 | S275 | 🟦 Dark blue | One per door opening on the perimeter |
| Vanity / wash-basin / WC / partition glass-wall support | RHS 40×20×3.0 | S275 | 🟨 Yellow | Always present (quantity/layout depends on fixture layout) |
| WC cistern support | SHS 60×60×4.0 | S275 | 🟪 Violet | **Conditional** — see Rule 5 |
| End / splice plate | PL4×60×120 | S275 | ⬜ Grey | At every qualifying cut/splice joint — see Rule 4 |
| Base cladding (non-structural) | 3mm GRP sheet | — | — | Wraps the entire underside of the skid |

---

## 2. Orientation Rules

Each profile's orientation is fixed and was confirmed both from the fabrication drawing and directly from the IFC solid geometry (bounding-box check), not from labels alone:

| Profile | Vertical (standing) dimension | Horizontal (flat) dimension |
|---|---|---|
| RHS 120×60×4 (perimeter) | **120mm vertical** | 60mm horizontal |
| RHS 60×30×4 (door beam) | **30mm vertical** | 60mm horizontal |
| RHS 40×20×3 (partition/vanity/WC) | **20mm vertical** | 40mm horizontal |

---

## 3. Perimeter Frame Rule

- The main perimeter (green, RHS120×60×4) follows the outer footprint of the pod, standing on its 120mm face.
- The perimeter is a **closed loop** around the pod's full outer boundary, including any plan-shape steps/notches (e.g. an L-shaped pod) — steps in the footprint become real, short cut pieces in the perimeter (see the confirmed example: a 63mm-long piece, mark `FS7`, sits at an internal L-shape shoulder/step — this is a genuine fabricated piece, not just an architectural annotation).
- **All 4 corners of the closed perimeter loop are left OPEN END** (uncapped, no end plate). This is intentional: the open RHS section at each corner is used as a **lifting point** to pass a hook/shackle through when handling the pod. Never cap these corners with a plate.
- The perimeter is cut and re-spliced only where a **door beam** is inserted in-line (Rule 4). It is *not* cut for internal T-junction members (partition/vanity/WC beams) — those simply weld onto the face of a continuous perimeter run.

## 4. Door Beam & Splice Plate Rule

Whenever the perimeter passes a door opening:

1. The green perimeter beam is **cut on both sides of the door**.
2. A **door beam** (blue, RHS 60×30×4, 30mm face vertical) is spliced in, in-line with the perimeter.
   - **Door beam length = door width + 6mm**, centered symmetrically on the door's centerline.
   - Example confirmed: 950mm door → 956mm beam (`FS292`); 900mm door → 906mm beam (`FS88`).
3. A **4mm gap** is left between each cut end of the green perimeter and the door beam.
4. A **P15 end plate (PL4×60×120)** is welded across each cut face — both the perimeter's cut end **and** the door beam's end — i.e. two plates per door (one each side).
   - Confirmed from IFC geometry: each P15 plate stands **vertical**, 60mm wide × **120mm tall** (the plate spans the *full* 120mm height of the green RHS end face) × 4mm thick. It is a full-height cap, not a partial cover.

**P15 plates are also used at green-to-green perimeter joints** — e.g. where perimeter segments meet at an internal corner/shoulder junction (confirmed in the verified example at the `FS7`/`FS8`/`FS9`/`FS87` shoulder junction area).

**P15 plates are never used at the 4 OPEN END lifting corners** (Rule 3).

> General statement of the rule: *A P15 (4×60×120mm) end plate is applied to any cut face of a green perimeter member — whether that cut exists to splice in a door beam or to form an internal perimeter joint — except at the 4 designated OPEN END lifting corners, which must remain uncapped.*

## 5. WC Cistern Support Rule (Conditional)

- **SHS 60×60×4 (violet) is included in the base skid ONLY if the WC cistern is floor-supported.**
- If the cistern is **wall-mounted** (supported by the wall frame instead), **no SHS 60×60×4 appears in the base skid at all** — confirmed by the verified example (BP3-BF1), which has a wall-mounted cistern and correspondingly zero SHS60×60×4 members in its BOM/IFC model.
- **Rule engine implication:** the base-skid generator needs a `cistern_support_type` input (`floor_supported` | `wall_mounted`) to decide whether to place SHS60×60×4 members at all. This has not yet been validated against a floor-supported example — flagged as an open item (see Section 8).

## 6. Vanity / WC / Partition Beam Rule

- RHS 40×20×3 (yellow, 20mm face vertical) is placed at vanity/wash-basin locations, WC locations, and partition glass-wall areas.
- These are **internal members** — they attach to the perimeter or to each other as needed by the fixture layout; the perimeter is **not** cut for these (unlike the door beam rule).
- Layout/spacing is driven by fixture position, not a fixed spacing rule — each verified instance so far has a bespoke layout matching its specific fixture plan. **Not yet generalized into a parametric spacing/count rule** (see Section 8).

## 7. Vertical Alignment / Coordinate Rule

Confirmed directly from IFC solid geometry (not just from drawing convention):

- **Every structural member in the skid — green, blue, yellow, and P15 plates alike — is bottom-flush.** All undersides sit at the same Z level, regardless of each profile's own height. Tops vary naturally since profile heights differ (120mm vs 30mm vs 20mm).
- **The steel bottom-datum sits 3mm above the true base reference plane (Z=0).** This 3mm offset is the thickness of the **GRP (fibreglass) cladding** that wraps the entire underside of the skid. Z=0 represents the finished, GRP-clad outer face of the base; the steel frame is set back 3mm from it so the GRP wraps flush underneath with all steel fully encased above.

**Rule engine implication:** when placing any base-skid member, set its underside Z-offset = `+GRP_thickness` (currently 3mm) from the pod's base reference plane, not Z=0 directly. This should be a configurable parameter (`base_cladding_thickness`), not a hard-coded 3.

## 8. Open Items / Not Yet Generalized

These remain to be confirmed against further example pods before the rule engine can treat them as fully parametric:

1. **SHS 60×60×4 floor-supported-cistern case** — no verified example yet; geometry/placement pattern unknown until a floor-supported-cistern pod is checked.
2. **RHS 40×20×3 spacing/count logic** — currently only known per-instance (bespoke to each fixture layout); no parametric rule (e.g. "one per fixture centerline", fixed spacing, etc.) has been confirmed yet.
3. **Perimeter step/notch generalization** — confirmed that plan-shape steps (L-shapes) produce real cut pieces in the green perimeter (e.g. the 63mm `FS7` piece), but the general rule for *how* a given step geometry decomposes into cut lengths has only been observed, not yet formalized as an algorithm.
4. **GRP thickness** — confirmed as 3mm for this instance; assumed to be a project standard rather than pod-specific, but not yet confirmed against a second example.

---

## 9. Reference Example (Verified Instance)

**Drawing:** BP3-BF1 (Project MARINE-1-PODS, Rev 00, Issued for Fabrication)
**IFC:** POD-IFC.ifc (Tekla Structures 2025 SP3)

| Mark | Profile | Length (mm) | Qty | Role |
|---|---|---|---|---|
| FS4 | RHS120×60×4 | 3032 | 1 | Bottom perimeter (full width, no cuts) |
| FS6 | RHS120×60×4 | 622 | 2 | Top edge (left of top door) & left edge (below 2nd door) |
| FS7 | RHS120×60×4 | 63 | 1 | L-shape shoulder/step return |
| FS8 | RHS120×60×4 | 713 | 1 | Shoulder horizontal run |
| FS9 | RHS120×60×4 | 1556 | 1 | Upper room left edge |
| FS10 | RHS120×60×4 | 613 | 1 | Top edge, right of door |
| FS11 | RHS120×60×4 | 3159 | 1 | Right edge — full height, continuous |
| FS86 | RHS120×60×4 | 181 | 1 | Internal stub, vanity/WC area |
| FS87 | RHS120×60×4 | 2199 | 1 | Shoulder-to-right-edge horizontal run |
| FS292 | RHS60×30×4 | 956 | 1 | Top wall door beam (950mm door) |
| FS88 | RHS60×30×4 | 906 | 1 | 2nd door beam (900mm door) |
| FS89 | RHS40×20×3 | 833 | 1 | Vanity/WC/partition |
| FS90 | RHS40×20×3 | 212 | 1 | Vanity/WC/partition |
| FS91 | RHS40×20×3 | 596 | 1 | Vanity/WC/partition |
| FS92 | RHS40×20×3 | 1510 | 1 | Vanity/WC/partition |
| FS93 | RHS40×20×3 | 598 | 1 | Vanity/WC/partition |
| FS94 | RHS40×20×3 | 122 | 2 | Vanity/WC/partition |
| P15 | PL4×60×120 | 120 | 6 | End/splice plate at cuts |

No SHS60×60×4 present (wall-mounted cistern — see Rule 5).
**Total weight:** 157.0 kg. **Total area:** 5.4 m².

---

*This document is intended for `/docs/` in the project repository (see master plan Section 22) and as source content for the future `KnowledgeBase` / `ModelingEngine` rules config (Section 13/16). It should be updated as further example pods are verified, particularly to close the open items in Section 8.*
