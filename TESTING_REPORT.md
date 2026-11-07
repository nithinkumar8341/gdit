# Golden Hour — Testing Report

Date: 2026-11-07 · Unity 6000.6.2f1 · Windows 11 · Build: `Builds/GoldenHour/GoldenHour.exe` (polish pass)

## 1. Compilation (Unity batch mode)

| Check | Result | Evidence |
|---|---|---|
| Full project compile after the polish pass (runtime + editor assemblies) | PASS | `BuildProject`, `ValidateProject` and `BuildWindowsPlayer` logs: 0 `error CS`, 0 `warning CS` |
| First polish compile | Fixed | 2 files had edits that did not apply (`GoldenHourAudio` fields, a duplicate local in `PatientActor`). Fixed and recompiled clean. |

## 2. Editor automation and validation (`GoldenHour/Validate Project`)

Run in batch mode with `-executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.ValidateProject`. Exit code 0; the log shows `Golden Hour project validation passed.` Validation throws on any failure, so a pass means every item below held.

| Validated item | Result |
|---|---|
| Triage rule unit cases: walking → GREEN (even with RED signs); not breathing / severe bleeding / unresponsive → RED; otherwise YELLOW | PASS |
| Exactly 5 PatientData assets with unique IDs and complete identity/observation/reasoning/aid text | PASS |
| Every patient's observations produce its expected triage under the rule | PASS |
| Distribution: 2 RED (P02, P03), 2 YELLOW (P04, P05), 1 GREEN (P01) | PASS |
| 5 SupplyData assets; every patient has a recommended supply | PASS |
| Build Settings: `MainMenu` (index 0) and `Samplespace` | PASS |
| Blender FBX exports imported (mannequin, kit with `KitHinge`, barrier, cone, damaged car) | PASS |
| 7 procedural WAVs imported, including the new `Footstep` and `UI_Hover` | PASS |
| Gameplay scene keeps the existing Player, GameManager, ScoreManager and EventSystem. References are assigned, including the unlit and smoke materials. InputSystemUIInputModule is present, StandaloneInputModule is absent, and an AudioListener is present. | PASS |
| **New:** fog on and the procedural late-afternoon skybox saved in the scene. The player camera renders post-processing. The Global Volume uses `GH_PostProcessing` (bloom, vignette, colour adjustments, tonemapping). | PASS |
| Main menu has MainMenuController, InputSystemUIInputModule, Camera and AudioListener | PASS |
| Template leftovers removed | PASS |

## 3. Windows build

`GoldenHour/Build Windows Player` reported `Build Finished, Result: Success`, 112,022,023 bytes. The 3 build warnings are TextMesh Pro package shader pragma deprecations (`enable_d3d11_debug_symbols`), not project code.

## 4. Automated end-to-end playtest in the built player

Command: `GoldenHour.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -goldenhour-autotest <dir>`

Result: **107 / 107 checks passed**, exit code 0.

The player log contains:
- 0 exceptions;
- 0 TextMesh Pro "Ellipsis is not available" warnings.

The first run had 1 failure: the smoke check ran before any particle had spawned. The close-up screenshot showed the smoke working. I prewarmed the smoke and made the check wait for particles.

Full log and 16 screenshots: `Docs/TestEvidence/`. The playtest restores the player's saved settings when it ends.

| Area | What was exercised | Result |
|---|---|---|
| Main menu | Loads first and fades in. Buttons grow on hover and shrink on press. How to Play shows the disclaimer. Volume slider changes `AudioListener.volume`. Sensitivity slider saves. Difficulty switches to Easy. Start fades out, then loads. | PASS |
| HUD (polish item 1) | Every root canvas is Screen Space - Overlay, so it is drawn after the 3D world. Each HUD text has a dark backing, so road markings sit behind opaque panels (screenshots 05, 09, 15). Overflow uses Truncate, so no Ellipsis warning. | PASS |
| Patient visibility (2) | Start is at z = −13.5 (was −17), 11.8 m from the nearest person. All 5 beacons are on screen with clear line of sight from the start. Beacons pulse until aid, take the triage colour, then settle. The compass arrow matches the true bearing to the nearest unexamined person (22° = 22°), then to the kit (104° = 104°). | PASS |
| Visuals (3) | Warm low sun (24° elevation) with soft shadows. Linear fog. Post-processing on the camera; the profile has bloom, vignette and colour grading. Smoke emitting (37 particles). All 4 hazard lamps flash. Skid marks, fences, signs, grass and lamp bulbs present. | PASS |
| Game feel (4) | Footsteps play while walking (2 steps in 1 s of W). Pause menu has volume and sensitivity. Tutorial tip shows at start and is gone after ~15 s. Scene changes fade. | PASS |
| Difficulty (5) | Easy carries into the scenario. The examination shows the "EASY HINT" checklist without the answer. The final report says "Difficulty: EASY". After restart on Normal, no hint is shown. | PASS |
| Shuffle (5) | Restart reshuffles positions: layout `1,3,2,0,4`, 3 of 5 people moved. The triage rule holds for everyone. Five correct triages score exactly 5×100 + 2×25 = 550. | PASS |
| Core loop (unchanged) | Real W/E/Esc through the Input System. Pause freezes the timer. Kit refused before triage. Raycast examines all 5. Wrong triage → feedback and −15, then reassessed. Lid opens > 90°. Wrong supply → −10 and no credit. Aid for all 5. Final score 1024 = 500 + 375 + 125 − 15 − 10 + 49 (time bonus ≤ 50). Restart; Main Menu shows the cursor. | PASS |

### Visual review of screenshots

I reviewed the screenshots in `Docs/TestEvidence/`. The first polish run showed two problems, both fixed before the final run:
- The tutorial tip covered the beacons in mid-screen. It now sits below the crosshair (05).
- Beacons over lying people sat at eye height when standing beside them. They are now at least 2.6 m high.

Final screenshots show:
- the warm sun with long shadows and haze;
- glowing beacons and beams over every person;
- the compass and the dark-backed HUD text;
- hazard lamps and smoke on the car, and skid marks leading to it (15);
- fences, verges with grass and bushes, lamp bulbs, the "ACCIDENT AHEAD" board and the speed sign.

## 5. Limitations — what was NOT verified automatically

- **Frame rate on a student laptop.** I did not measure it. The budget is kept small:
  - shadows: 2 cascades up to 45 m;
  - SSAO at half resolution;
  - one extra unshadowed point light;
  - at most 60 smoke particles;
  - colliders removed from decoration.

  An integrated-GPU check is still needed.
- **Mouse look and sensitivity feel** with a physical mouse. The test sets the value and aims the camera from code.
- **Pointer hover and clicks.** The test calls the `ButtonFeedback` handlers and the button methods directly.
- **Audible output.** Footstep and hover playback were counted and triggered, not listened to.
- **Running out the 15-minute timer.** Reviewed in code, not run.
- **Fullscreen and other resolutions.** Tested windowed at 1600×900 only.
- **Third-party samples.** `Assets/TextMesh Pro/Examples & Extras/` still contains legacy `Input.*` calls. No game scene uses them.

*This is an educational simulation, not medical training or medical advice.*
