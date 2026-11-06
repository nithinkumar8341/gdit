# Golden Hour — Testing Report

Date: 2026-11-07 · Unity 6000.6.2f1 · Windows 11 · Build: `Builds/GoldenHour/GoldenHour.exe`

## 1. Compilation (Unity batch mode)

| Check | Result | Evidence |
|---|---|---|
| Fix for the 4 handoff errors (CS0246 `BuildFailedException`, 3× CS0103 `DestroyNamedObject`) | PASS | Batch compile log: 0 `error CS`, 0 `warning CS` |
| Full project compile after refactor (runtime + editor assemblies) | PASS | `BuildProject`, `ValidateProject` and `BuildWindowsPlayer` logs: 0 errors, 0 C# warnings |

## 2. Editor automation and validation (`GoldenHour/Validate Project`)

Run in batch mode with `-executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.ValidateProject` (exit code 0, log line `Golden Hour project validation passed.`). Validation throws on any failure, so a pass means every item below held:

| Validated item | Result |
|---|---|
| Triage rule unit cases: walking → GREEN (even with RED signs); not breathing / severe bleeding / unresponsive → RED; otherwise YELLOW | PASS |
| Exactly 5 PatientData assets with unique IDs and complete identity/observation/reasoning/aid text | PASS |
| Every patient's observations produce its expected triage under the rule | PASS |
| Distribution: 2 RED (P02, P03), 2 YELLOW (P04, P05), 1 GREEN (P01) | PASS |
| 5 SupplyData assets; every patient has a recommended supply | PASS |
| Build Settings: `MainMenu` (index 0) and `Samplespace` | PASS |
| Blender FBX exports imported (mannequin, kit with `KitHinge`, barrier, cone, damaged car) | PASS |
| 5 procedural WAVs imported | PASS |
| Gameplay scene keeps existing Player, GameManager, ScoreManager and EventSystem; GameplayController / AccidentEnvironment references assigned; InputSystemUIInputModule present; no StandaloneInputModule; AudioListener present | PASS |
| Main menu has MainMenuController, InputSystemUIInputModule, Camera and AudioListener | PASS |
| Template leftovers `Assets/TutorialInfo` and `Assets/Readme.asset` removed | PASS |

## 3. Windows build

`GoldenHour/Build Windows Player`: `Build Finished, Result: Success`, 111,357,687 bytes reported. The 3 build warnings are all TextMesh Pro package shader pragma deprecations (`enable_d3d11_debug_symbols`), not project code.

## 4. Automated end-to-end playtest in the built player

Command: `GoldenHour.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -goldenhour-autotest <dir>`

Result: **65 / 65 checks passed**, exit code 0, 0 exceptions in the player log. Full log and 14 screenshots: `Docs/TestEvidence/`.

| Area | What was exercised | Result |
|---|---|---|
| Main menu | Loads first; How to Play (with disclaimer); Settings slider changes `AudioListener.volume`; Start loads gameplay | PASS |
| Briefing / audio | Briefing modal with disclaimer before play; ambience AudioSource playing | PASS |
| Player | Timer starts; cursor locks; player settles on ground (y = 0.08); **real Input System W key** moved the player 4.21 m | PASS |
| Pause | **Real Esc key** opens pause; timer frozen ≥ 0.6 s; cursor unlocked; Esc resumes | PASS |
| Kit gating | Kit refused before any triage | PASS |
| Objectives | Reach-scene completes after passing the barriers; examine-all/triage-all; supply; aid-all → 5/5 | PASS |
| Examination | Raycast from the camera hits each of the 5 patients (P01 via a **real E key press**, P02–P05 via the same `TryInteract` raycast path). The expected triage and reasoning are not on screen before the decision | PASS |
| Triage | Wrong GREEN for P04 → feedback, no credit, −15; reassess → correct; 5/5 correct afterwards | PASS |
| First-aid kit | Raycast opens inventory; lid rotates > 90° (visually confirmed opening away from the player) | PASS |
| Supplies | Wrong supply → feedback, no aid credit, −10; correct supply for all 5 → simulated aid complete | PASS |
| Final report | Ends after all aid; score 1024 = 500 + 375 + 125 − 15 − 10 + 49 time bonus; bonus ≤ 50 < 100; disclaimer and rating present; timer stopped | PASS |
| Navigation | Restart reloads a fresh scenario with score 0; Main Menu returns to the menu with a visible cursor | PASS |

### Visual review of screenshots

The screenshots in `Docs/TestEvidence/` were reviewed. Findings, all fixed before the final run:
- The kit lid first opened downward, then opened toward the camera and hid the supplies.
- Modal panels had excess empty space.
- 3D patient labels were oversized at close range.

The final screenshots show:
- correctly oriented Blender models (standing and lying mannequins, car, barriers, cones, kit);
- live triage markers and status labels;
- readable white / red / charcoal UI.

## 5. Limitations — what was NOT verified automatically

- **Mouse look** with a physical mouse. The test aims the camera programmatically.
- **Button clicks with the pointer.** The test calls the same methods the buttons invoke. EventSystem/InputSystemUIInputModule presence was verified, not physical clicks.
- **Audible output** and loudness. Only playback state and volume values were checked.
- **Running out the 15-minute timer.** The end-on-timeout path is code-reviewed but not run.
- **Fullscreen, other resolutions and multi-monitor behaviour.** Tested windowed at 1600×900 only.
- **Third-party samples.** `Assets/TextMesh Pro/Examples & Extras/` still contains legacy `Input.*` calls. They are TMP package samples that no game scene uses, preserved as source assets.

Manual playthrough steps are listed in `README.md` (Play) and should cover the items above.

*This is an educational simulation, not medical training or medical advice.*
