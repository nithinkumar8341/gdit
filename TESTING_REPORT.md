# Golden Hour v2 — Testing Report

**Date:** 2026-11-07
**Environment:** Unity 6000.6.2f1 · Windows 11 · i5-12500H laptop (Intel Iris Xe plus RTX 2050)
**Build:** `Builds/GoldenHour/GoldenHour.exe`
**Evidence:** `Docs/TestEvidence/` contains:
- `autotest_results.txt` and the screenshots;
- `editmode_tests.xml`;
- `benchmarks/*.txt`.

## 1. Compilation

| Check | Result | Evidence |
|---|---|---|
| Project code (runtime, Core, editor and test assemblies) | **0 errors, 0 warnings** | `BuildProject` and `BuildWindowsPlayer` batch logs: no `error CS` or `warning CS` lines. |
| Player build | **Success**, 113 MB | The 3 build warnings are TextMesh Pro package shader pragma deprecations, not project code. |

Errors found during v2 development were fixed in the compile → read log → fix loop. DEV_LOG records them: List methods used on `IReadOnlyList`, a stray `arrived` variable, and others.

## 2. Validate Project (`GoldenHour/Validate Project`)

The log shows `Golden Hour project validation passed.` Validation throws on the first failure, so a pass means every item held:

- START rule cases are correct. The campaign has the four scenarios in order. The tutorial has 3 casualties and at least 6 steps. The bus has at least 9 casualties, night, rain, fuel and fire. The building has at least 11 casualties, children and at least 4 walkers. A helmeted motorcyclist exists. Across the campaign, the casualties cover all four START categories.
- Build Settings list MainMenu first, then Samplespace.
- All procedural audio clips are imported and assigned.
- The gameplay scene has Player, EventSystem, IncidentDirector (references assigned), the Input System UI module only, fog and a skybox, a post-processing camera, and a volume with bloom and vignette.
- The main menu has its controller, campaign catalog, camera and AudioListener.
- **Blender v2 imports:**
  - `RiggedHuman` and `RiggedChild` have every joint, face +Z, have the left arm on −X and plausible heights. Logged values: hips 0.95 / 0.87 m, head 1.69 / 1.56 m.
  - Ambulance, PoliceCar, FireTruck and Bus are ground-standing, longest along Z and front on +Z.
  - Motorbike, Stretcher and V2Props are present.
- Template leftovers are removed.

## 3. Edit-mode unit tests (Test Framework 1.8)

**26/26 passed** (`editmode_tests.xml`; 24 test methods, some parameterised). The tests cover:
- **START steps:** walking → GREEN even with red-flag signs; no breathing after airway opening → BLACK; breathing only after airway opening → RED; the rate threshold at 30; the circulation threshold (refill over 2 s or no radial pulse); commands; deceased → BLACK; priority order.
- **Physiology:** haemorrhage deteriorates and dies untreated; a pressure dressing stabilises; YELLOW worsens to RED unless stabilised; airway opening delays death and the recovery position stabilises; CPR never changes the outcome, and an expectant death is not counted as preventable; seeded determinism, with the measured rate respecting the threshold; transport stops deterioration.
- **Triage records and scoring:** first-tag scoring; a tag needs an examination and a retag needs a re-examination; a retag without a change in condition earns nothing; handover priority; dispatch fields; time bonus, stars and ledger; adaptive difficulty bands; the profile unlocking the next scenario.

## 4. End-to-end automated playtest (built player)

The player is launched with `-goldenhour-autotest <dir>`. It uses a fixed seed (4242), Normal difficulty, an isolated profile folder, and plays real frames, including the NavMesh, ambulances and minigames. It keeps an **independent score tally** computed from the published rules and compares it with the game's ledger.

**Result: ALL 257 CHECKS PASSED. 0 errors or exceptions logged during the whole run. Exit code 0.**

| Area | Checks | Highlights |
|---|---|---|
| Menus and settings | 22 | Live 3D menu; disclaimer; campaign locks; How to Play (START plus simplifications); every setting, including rebinding and the Medium budget (30 m shadows, 1 cascade, no SSAO); feedback JSON saved; credits; loading tip; return to menu with a visible cursor. |
| Training Yard | 51 | Real keyboard input (W, C kneel, Esc pause, E, G gloves); guided tutorial; intro cutscene; dispatch; shout; NavMesh walkers; refill deterioration; recovery-position minigame (refuses early confirm, catches over-roll); a real NavMesh stretcher carry. Exact score 690 = tally. |
| Highway Collision | 58 | Photo mode (time frozen, PNG saved); an unneeded treatment is penalised; YELLOW worsens to RED; retriage refused without re-examination; justified retriage credit; handover; end cutscene; all five debrief tabs. Exact score 1330 = tally. |
| Night Bus Rollover | 60 | Stress system; wrong 112 number scored; an untreated airway casualty dies at the defined time (min 10.8); preventable death penalised; BLACK casualty resolved. Exact score 1477 = tally. |
| Collapsed Community Hall | 66 | Children; four walkers; unjustified retag penalised; wrong handover order penalised; CPR minigame (30 compressions, rate inside 100–120/min); CPR not scored and the deprioritised note shown. Exact score 1892 = tally. |

Every scenario also checks:
- briefing and disclaimer;
- spawn counts and runtime NavMesh;
- clock running;
- limited supplies and gloves;
- hazards marked;
- START from findings only (regex reconstruction);
- physical tags;
- ambulance with sirens and paramedics;
- all stretcher cases handed over;
- the scenario ending;
- telemetry JSON;
- 1–3 stars;
- debrief with feedback and disclaimer;
- profile unlock of the next scenario.

**Defects the autotest found and that were fixed:**
- The ambulance's instant-load path lost the casualty being fetched.
- Post-end actions closed the debrief and added points after the end.
- Green-zone casualties were offered for handover.
- Tagged-BLACK casualties blocked scene completion.
- Overlapping HUD panels, found from screenshots.
- A hover-test timing margin (1.020 against >1.02).

## 5. Performance (`-goldenhour-benchmark`)

**Method:** each scenario is loaded, started, warmed up for 2 s, then the player turns 360° over 10 s. GPU selection on this hybrid laptop was forced through the Windows per-app GPU preference, which was removed afterwards. Files are in `Docs/TestEvidence/benchmarks/`.

| GPU | 1080p preset | Mode | Average fps (4 scenarios) | Worst 1% low | Worst frame |
|---|---|---|---|---|---|
| Intel Iris Xe (integrated) | Medium | vSync, as played | **60.0 in every scenario** | 59.9 | 16.7 ms |
| Intel Iris Xe (integrated) | Medium | uncapped | 201–222 | 135 | 8.3 ms |
| Intel Iris Xe (integrated) | Low | uncapped | 263–338 | 172 | 7.4 ms |
| RTX 2050 | Medium | uncapped | 315–373 | 163 | 11.5 ms |

The **60 fps at 1080p Medium on integrated graphics** target is met on this machine.

Measures taken:
- Simple low-poly meshes and shared materials: tinted rig materials are cached, which preserves SRP batching.
- Medium uses render scale 0.9, a single shadow cascade, 30 m shadows and no SSAO. Low also turns post-processing off.
- Small character parts do not cast shadows.
- One real-time light per emergency light bar; capped particle counts.

An earlier capped run, before the Medium changes, showed misses down to 30 fps and a single 866 ms first-load hitch. Neither appeared after the changes.

## 6. Not automated: manual checks recommended

1. **Feel and readability with real hands on the controls.** Mouse look, kneeling, the dressing hold, the A/D recovery roll, the CPR rhythm by ear, and gamepad play end to end. The autotest drives some steps through test hooks: minigame inputs, dispatch values, handover calls.
2. **Audio mix:** music layers and stress heartbeat levels, the siren's 3D falloff, and rain loudness at night.
3. **Visual review:** characters in every pose (the rigs are rigid-segment, so check for joint gaps), the bus scenario's darkness at different monitor gammas, and outline visibility.
4. **Accessibility in practice:** colour-blind tags with a colour-blind tester, text size 130% on all panels, and reduce-motion comfort.
5. **Other hardware:** a laptop with only integrated graphics, and a 4K or ultrawide display.
6. **Medical content review by a qualified first-aid trainer.** Required before any teaching use; the content has NOT been medically reviewed.
7. **Playtest with participants,** following PLAYTEST_PLAN.md.
