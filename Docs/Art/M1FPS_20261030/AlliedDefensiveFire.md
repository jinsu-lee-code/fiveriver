# Allied defensive fire — 2026-10-30

Status: implementation connected in Unity; final runtime verification pending.

The ROK scene actors previously had AC_ROK and a valid crouched firing clip, but no combat component, Fire trigger caller, rifle, or muzzle. AllyCombatSetup adds one AllyCombatant and one reviewed M1 visual to Ally_1–3 and ParkYongpal, preserving the existing humanoid models and narrative controller. Re-running the setup updates references without duplicating the weapons.

AllyCombatant selects a living, non-retreating enemy from EnemyDirector’s roster, checks range and line of sight, turns into a crouched firing stance, then triggers the actual AC_ROK Fire_Crouch clip. The clip sample puts the palms at a -33.6-degree yaw relative to the body; shoulder orientation compensates for that angle. In LateUpdate, the rifle is anchored to the firing shoulder, aims toward the actual target height, and constrains both animated arms to the stock and foregrip using length-preserving two-bone IK. Neck/head gaze correction removes the source clip's downward look while preserving the crouch/fire animation. A single delayed fire cue spawns the M1 muzzle effect and plays existing non-verbal SFX_M1_Fire spatially. Nearby ground receives a suppression dust effect. CharacterAnimationEvents.Fire remains a no-op, preventing duplicate shots.

The existing WaveConfig supplies firing cadence and range. Support fire has no TakeDamage, kill, goal, tutorial, or wave-progression calls. The player retains all gameplay kills and progression credit. Fire is gated to Scout and Assault, continues during combat subtitles (there is no spoken TTS to interrupt), and stops during pause, player death, retreat, and all other sections, and clears pending triggers on transition, retry, and component disable. Park’s Silence Call animation remains owned by NarrativeDirector. Debug-only counters reset on retry and new run.

Modified files:

- Combat/AllyCombatant.cs — new ally presentation component.
- Combat/AllyRiflePose.cs — shoulder attachment, two-arm constraints, and target gaze without skeleton scaling or joint translation.
- Editor/AllyCombatSetup.cs — repeatable scene wiring and world-space M1 attachment.
- Combat/EnemyDirector.cs — read-only active enemy roster accessor.
- Audio/AudioDirector.cs — optional forced spatial playback; existing voice removal and M1 reload cue edits preserved.

Verification workflow (Unity 6000.3.25f1, existing project, no build): run AllyCombatSetup.Apply in Edit mode; enter Play; start Scout; observe crouched rifle recoil, muzzle flash, and spatial gunshot with live enemies. Compare 18-second observer logs for running Scout, paused Scout, and Silence. Inspect actual AudioSource clip/spatial blend, Animator state, muzzle aim angle, and counters; check that enemy count remains unchanged without player kills. Retry and restart must reset counters and pending fire. Check Park’s S4 Call after the transition.

External evidence collector lives only under Map_Import_20261030/ally_qa_*.cs.txt. It observes Editor play state and writes JSON, then unregisters itself; no measurement bot is added to the shipped game.

The user explicitly prohibited new builds. The supplied older FiveRivers.exe is unchanged; executable play verification is therefore not claimed.

## Verification evidence so far

- `ally_qa_Scout_Running_132947.json`: 280 observations over 18 seconds, live frame count 8725 to 10681. All four actors added 5–6 shots; actual Fire_Crouch state, non-null muzzle effects, and playing M1 AudioSources with spatialBlend=1 observed. Max muzzle aim error 0.063 degrees. Enemy roster stayed at four (a repeated debug Scout jump had retained two earlier scouts; final fresh-run QA removes this debug-only duplication).
- `ally_qa_Assault_Paused_133219.json`: 285 observations, gameplay time fixed at 216.339; shot counts 53/52/52/53 unchanged, no pending shots.
- `ally_qa_Silence_Running_133253.json`: 282 observations, gameplay time 217.049 to 235.028; same unchanged counts, no pending shots.
- `ally_qa_resets.json`: before 16/14/14/14, retry 0/0/0/0, new Tutorial run 0/0/0/0.
- `ally_qa_Scout_Running_134251.json`: target-present samples after two-arm constraints had max left-grip error 0.216 mm, right-grip error about 0.0021 mm, stock movement within the 2.2 cm recoil allowance, head aim below 0.028 degrees (independent High review).

The first firing images failed visual review because the rifle floated away from the support hand and the source clip looked down. The shoulder/two-arm constraints corrected that. The next images exposed head roll and faceted Park forearm skin; head roll and the wrist/forearm rotation transition were corrected, with final close visual review still pending. `AllySkinNormals` creates a derived arm-skin mesh with seam-welded normals while retaining all original positions, bone weights, UVs, and cloth normals. This is a targeted correction of the delivered skin slot, not a character replacement.

