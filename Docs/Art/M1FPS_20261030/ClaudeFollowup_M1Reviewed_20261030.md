# Claude follow-up — user-corrected M1 in first person (2026-10-30 15:30–16:08 KST)

Status: **implemented and self-checked by Claude with automated input. Not reviewed by Astra High.** No build, no commit/push. The supplied `Downloads/FIveRiver/FiveRivers.exe` does not contain these changes.

ROOT = `C:/Users/mbc/Documents/ChatGPT/다섯개의강/Map_Import_20261030`. Backups of every edited script before change: `ROOT/ClaudeMapIntegration_20261030/Backups/ClaudeM1_20261030/`.

## Scene (`Assets/_Project/Scenes/Dabudong.unity`, saved)

- First-person weapon is now `Art/Weapons/M1Refined/Prefabs/M1_Reviewed.prefab` (same prefab the allies use; SHA256 609B1177…454EB), unpacked under `M1_ShotMotion/ArtM1`. Active LOD0 = 72,392 triangles; LOD1/2 disabled, LODGroups reduced to LOD0. The previous art-base visual is kept disabled as `ArtM1_ArtBase_Unused` (not deleted).
- `BoltMotion`: identity wrapper at (0,.05977,.03158) holding the rotated bolt covers L0–2 and op rod L0–2 (world pose kept). `M1Presentation.bolt` points to it.
- Sockets from the prefab markers: muzzle (0,.06197,.72401), clip eject (0,.06681,.04493).
- ADS `aimPosition` = (0,-.0794,.11454): eye relief 94 mm behind the reviewed rear sight (z −.021), eye height on the line from the round peep-hole centre to the front post top.
- Right hand (`Grip_Right` and grafted `wrist.R`) raised 27 mm: the reviewed stock wrist is higher/slimmer than the art FBX pistol-grip line, so fingers previously hung 2–3 cm below it. Now fingers wrap the wrist, thumb over the top, index along the side above the trigger guard.
- Reload: `reloadHandRotation` = palm down onto the clip, fingers forward/across, straight wrist (after the user's reference live-fire loading video). `clipGripOffset` recomputed so the loaded clip finishes 1 cm above the reviewed receiver well at p=.66.
- Support hand (`Grip_Left` and `wrist.L`) lowered 8 mm so its fingertips stay below the ADS eye line.
- Fill light kept at the approved .4.

## Code / assets

- `Scripts/Combat/M1Presentation.cs` (runtime, visual only — user approved touching hands/arms): reload hand path now starts just above-right-behind the receiver (0.045,.14,-.035), presses down 45 mm, lifts 60 mm clear as the bolt closes; approach clip tilt reduced to (-4,-8,0); thumb IK removed (it flipped the thumb straight up in the palm-down pose). Fire, ammo, timing and reload progress are unchanged.
- `Scripts/Combat/M1ArmRig.cs` (runtime, visual only): reload elbow hint moved low/outboard so the arm rises from lower right instead of covering the view; sleeve radius .047/.041 → .043/.038; elbow/cuff fold depth 4.5/1.5 mm → 10/3.5 mm so the sleeve shades as cloth at viewmodel distance.
- `Art/Animations/FP/MixamoSource/M1_FP_Cloth.mat`: existing PolyHaven stretch poplin diffuse (already in project, CC0, see SurfaceSources.md) as base map at tiling (2,3); tint (.280,.254,.396) so the linear product averages the olive drab (.19,.205,.135); smoothness .12 → .08; normal strength .08 → 1.0. Previous: colour (.245,.28,.18), no base map, tiling (3,6).
- Viewmodel FOV: an overlay camera at FOV 54–60 was test-rendered (post-processing works) but magnifies and shifts the rifle/hands ~1.2–1.3×, which would require re-laying hip/ADS/reload. Not applied; single camera FOV 70/50 unchanged.
- `Scripts/Editor/RefinedM1Setup.cs`: regenerates the same result (reviewed prefab, no M1MeshRefinement, LOD0/BoltMotion, sockets, ADS, fill .4, right-hand raise, reload rotation/clip offset).
- `Scripts/Editor/M1FpsRigSetup.cs`: cloth defaults only when the material is first created.
- `Scripts/Editor/ClaudeMapGrounding.cs`: only the STAGE GPU re-upload line (vertices/normals/tangents + `UploadMeshData(false)`). Not re-run; sandbag shapes unchanged.

## Evidence (automated input, not human play)

- Final state: `ROOT/evidence/ClaudeM1Reviewed_FPS_1606` — ADS and hip shots hit TutorialTarget, reload ends at 8 rounds. Hip hit is not deterministic after the injected look change (`_1551`, `_1558` second shot hit terrain, as in earlier records).
- `ROOT/evidence/ClaudeM1Reviewed_EditModeTests_1607.json`: 77/77 passed after the last runtime change.
- No exceptions in the Editor log after the 15:41 restart. Editor left in Edit mode, scene saved, runInBackground false, timeScale 1.

## Not done / open

- Independent High review of all of the above.
- The static clip in the reviewed receiver stays visible while empty / during insertion (runtime has no hook for it).
- Sandbag close-ups and representative wall bullet-dust check, fresh-Play full console/performance audit, night/flare/dawn camera pass, dawn reinforcement foot sliding, blanket seam — untouched since the 15:27 handoff.
- Unity Editor closed unexpectedly at 15:41 (no crash dump) and was reopened at the user's request.
