# M1 FPS / hands review — 2026-10-30

Validated in the existing Unity 6000.3.25f1 Editor. No new build was made; the supplied older FiveRivers.exe does not contain these source changes.

## Implemented

- (Superseded for the weapon by the Claude follow-up below: first person now uses the user-corrected `M1_Reviewed` prefab.) Retained the reviewed art M1 and original CorrectedGrip hand source. The derived hand mesh widens finger cross-sections, palm and thumb-base tissue without increasing finger length; distal taper and modest joint volume improve the adult-hand silhouette. Support-finger curl follows the fore-end. Original authored normals/tangents are retained: recalculating them produced visible palm facets even though all 3,086 duplicate-position groups had zero seam separation.
- Replaced separate sleeve tubes and elbow caps with one continuous shoulder–elbow–wrist surface. The measured corrected-hand cap controls the cuff endpoint and 6 mm overlap. Cached ring/tangent buffers avoid per-frame array allocation.
- Preserved real imported Mixamo rifle recoil and return, retimed into a short shot pulse. Camera look and spring recoil are independent; shot rays use the current frame's camera aim.
- Added restrained target movement, small impact marks and particle-color-aware dust. No HUD hit marker or reward sound was added.
- Preserved the original eight-cartridge clip geometry. Its approach angle reveals the brass before aligning for insertion; thumb contact follows the clip. Thumb rest rotations are restored on disable.
- Added derived skin material with subtle pores/roughness and tissue tint. Nail detection uses the actual untinted linear atlas values, not an unreachable bright-color threshold. Scanned cotton is weak and fine enough to avoid the earlier large checker/rib pattern.
- Earlier surface work includes persistent Volume subassets, derived character materials, photographed soil and restrained night/dawn reflection skies. Subsequent Claude map integration is a separate visual-data operation.

## Verification

- High visually accepted `evidence/FPS_Reviewed_1426` hip, ADS, reload rise, clip approach, insertion and return: thicker natural hand proportions, readable nails, no new major penetration, no wrist gap or sleeve opening, and no large palm facet artifact. This is a geometry and presentation pass, not a claim of photographic realism.
- `evidence/FPS_ActualInput_NoCapture/input_review.json`: 1,246 unique live frames over 7.8005 seconds with player/weapon controllers enabled. Look was injected 0.104 seconds after the ADS shot; base pitch changed by +0.108 degrees while recoil was recovering and remained at that new value. Recoil peaked at 1.5677 degrees and returned near zero after 0.603 seconds. The ADS shot hit TutorialTarget; the subsequent hip shot hit terrain after the deliberate look change. Earlier centered-input runs separately verified both ADS and hip target hits.
- At a real 1600×900 GameView, the no-capture run after its initial 0.5 seconds recorded median frame delta 5.73 ms, 95th percentile 6.98 ms, maximum 26.81 ms, and no frame above 33.3 ms. This is a short isolated Editor run, not a whole-game performance guarantee. PNG capture stalls are excluded from this measurement.
- `evidence/EditModeTests_1423.json`: 77 EditMode tests passed, zero failed/skipped.
- Original-source assets are retained; all revised meshes/materials are derived assets. Temporary input devices, bindings, focus settings and review callbacks are restored by the external harness.

## Related records

- [Claude follow-up: user-corrected M1, hands, reload (15:30–16:05)](ClaudeFollowup_M1Reviewed_20261030.md) — self-checked only, not High reviewed
- [Animation sources](AnimationSources.md)
- [Surface sources and licenses](SurfaceSources.md)
- [Allied supporting fire](AlliedDefensiveFire.md)
- [Independent High review](IndependentHighReview.md)

Remaining integration checks are recorded separately: final scene reopen/persistence, map contacts after the Claude visual import, actual Call gesture, and night/dawn/new-run roundtrip. A test pass alone does not certify visual quality.
