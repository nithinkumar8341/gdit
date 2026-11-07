# Development Log

- **Phase 1 (baseline):** Inspected project scripts, scenes, packages and UI. Confirmed Blender background scripting. Unity batch compilation was initially blocked because the Editor was open; see `PROJECT_ANALYSIS.md`.
- **Phase 2 (structure):** Created `Assets/GoldenHour/` and its subfolders. Preserved all original assets.
- **Phase 3–5 (player, environment, patients, triage — first pass):** Added `FirstPersonPlayer`, `GameplayController`, `PatientData`/`PatientActor`, `TriageEvaluator`, UI factory and menu controller. The builder did not compile: missing `UnityEditor.Build` using, and `DestroyNamedObject` called where the helper was named `DeleteNamedObject`.
- **Phase 6 (Blender assets):** The first export placed meshes along Blender's Y axis as if it were "up", so the models came out scrambled. `generate_assets.py` now maps (x, up, depth) into Blender's Z-up space. Regenerated `LowPolyMannequin`, `FirstAidKit` (with `KitHinge` lid pivot), `RoadBarrier`, `SupplyBundles`, `AccidentProps` (traffic cone) and a new `DamagedCar`. Blender exited 0 and printed `GOLDEN_HOUR_BLENDER_ASSETS_OK`.
- **Phase 7 (compile fix and refactor):** Fixed both compile errors. Batch compile: 0 errors, 0 warnings. Split the 860-line controller into `ModalWindow`, `GameplayHud`, `ObjectiveTracker`, `AccidentEnvironment`, `MaterialFactory`, `Palette` and `PatientBillboard`. Fixed bugs:
  - Modal replacement in the same frame attached buttons to a panel pending destruction.
  - The supply screen revealed the correct item.
  - P01 data (`canWalk=false`, expected GREEN) contradicted the rule.
  - A patient spawned inside the car.
  - Reassessment never corrected the score.
  - Runtime `Shader.Find` could be stripped from builds; a serialized URP material template replaces it.
- **Phase 8 (kit, scoring, menu, results):** State-based scoring (+100 / +75 / +25, −15 / −10, time bonus capped at 50). Pause menu with volume, Restart and Main Menu. Final report with rating, per-person results and disclaimer. The menu scene got a camera and AudioListener.
- **Phase 9 (builder run):** `GoldenHour/Build Project` created the MainMenu scene, 5 PatientData and 5 SupplyData assets and `GH_RuntimeBase.mat`. It configured Build Settings (MainMenu, Samplespace), rewired the gameplay scene and deleted `Assets/TutorialInfo` and `Assets/Readme.asset`. Validation passed.
- **Phase 10 (test):** Added `AutomatedPlaytest` (`-goldenhour-autotest`). Screenshots exposed a kit lid that opened downward and then toward the player. The hinge axis is now derived from the lid geometry and the kit is rotated so it opens away from the player. Tightened panel sizes. Final run: 65/65 checks passed, 0 exceptions in the player log.
- **Phase 11 (build and docs):** `GoldenHour/Build Windows Player` succeeded: `Builds/GoldenHour/GoldenHour.exe`, 111,357,687 bytes reported. The 3 warnings are TextMesh Pro shader pragma deprecations from the package. Wrote `README.md` and `TESTING_REPORT.md`, and copied the evidence to `Docs/TestEvidence/`.
- **Phase 12 (polish):** HUD text now has opaque backings on the overlay canvas, and TMP overflow is Truncate (fixes the LiberationSans Ellipsis warning). The player starts at z = -13.5. Patients get pulsing beacons, and a compass arrow points to the nearest unexamined person, then to the kit. Late-afternoon sun, procedural sky, linear fog, and a post-processing profile (bloom, vignette, grading, white balance, neutral tonemapping); SSAO is now half resolution. A consistent palette; skid marks, glass, hubcap, fences, grass, lamp bulbs and signs; flashing hazard lights and engine smoke. Footsteps and hover sound; sensitivity in Settings and Pause; scene fades; button hover and press scaling; a 15-second tutorial tip. Easy/Normal difficulty, and patient positions shuffle on Restart. Autotest extended to 107 checks: all pass, 0 exceptions, rebuilt player.

## v2 (V2_SPEC.md)

- **M1 (simulation core):** New `GoldenHour.Core` assembly (pure C#, no scene dependencies). It contains:
  - `StartTriage`: four START categories, the step that decided each, and explanations.
  - `PhysiologyState`: breathing rate, airway, capillary refill, radial pulse, commands, bleeding.
  - `CasualtyProfile` (ScriptableObject): deterioration (death/worsen minutes, refill drift) and stabilising treatments.
  - `CasualtySim`: seeded and deterministic. It handles deterioration, death, treatment, transport and the measured breathing rate.
  - `TriageRecord`: a tag requires an examination, a retriage requires re-examination, only the first tag is fully scored, and a justified retriage earns +40.
  - `ScoreRules`/`ScoreLedger`, `HandoverRules`, `DispatchReport`, `AdaptiveDifficulty`, and telemetry/profile data.
  - 26 edit-mode NUnit tests (Test Framework 1.8): 26/26 pass in batch mode. The first run failed 1 test because the test drew the random stream twice; the test was fixed.
  - The v1 game is unchanged and still builds; v2 gameplay integration follows in M2.
- **M2 (scenarios and systems):** The v2 gameplay runs on `IncidentDirector`; the v1 `GameplayController` stack is removed.
  - **Content:** `GoldenHourContent` builds the casualty profiles and four `ScenarioData` assets: Tutorial (3 casualties), Highway (6), Night/Rain Bus Rollover (10, fuel leak, fire), Collapsed Building (12, children, walking wounded).
  - **Scene phase:** hazard marking, then a 112 dispatch minigame scored field by field. Supplies are limited.
  - **Response:** ambulance waves timed from dispatch, with paramedics walking on a runtime-baked NavMesh and carrying casualties on a stretcher. Handover order is scored against START priority.
  - **Crowd and command:** the walking-wounded shout sends people to the green zone; panicking bystanders can be calmed; incident-commander radio cues; tablet; pause menu.
  - **Removed (v1):** GameplayController, ObjectiveTracker, PatientActor, PatientBeacon, PatientData/SupplyData (scripts and assets), TriageEvaluator, GameplayHud. The builder now strips missing scripts and validates the IncidentDirector.
  - **Fixes found by the autotest:**
    - `Ambulance.CompleteLoadingNow` lost the casualty mid-fetch; the queue is now dequeued only on load.
    - Actions after the scene was cleared closed the debrief and scored after the end; they are now guarded.
    - Green-zone casualties were offered for handover; they are now excluded.
    - Correctly tagged BLACK (expectant) casualties now count as resolved.
- **M3 (interactive first aid):** `FirstAidMinigame` runs on the overlay canvas while the clock keeps running.
  - **Recovery position:** roll the casualty with A/D into the on-side band; rolling too far is caught and eased back; then press Space to tilt the head and open the airway.
  - **CPR:** press Space in rhythm with a 110 bpm metronome; the rate is measured against 100-120 per minute over 30 compressions. The panel shows the simplification note and states that CPR is deprioritised in mass-casualty triage. CPR is never scored.
  - **Already in place from M2:** hold-to-apply pressure dressing, blanket, airway, the gloves-first penalty, breathing motion, restless/limp states, barks. No gore.
  - **Verification:** player autotest 226/226 checks pass (all four scenarios end to end: exact score tally, deterioration/death, retriage, handover, minigames with simulated input), 0 errors or exceptions. Edit-mode tests 26/26.
