# GOLDEN HOUR — Claude Code project instructions

Source of truth for spec/status history: `CLAUDE_CODE_HANDOFF.md`. Current progress: `DEV_LOG.md`.
**Active spec: `V2_SPEC.md` (Golden Hour v2 serious-game upgrade, milestones M1-M10). It overrides v1 requirements where they conflict.**

## Project and tools

- Unity project root: `C:\gdit\My project (3)\My project (2)`
- Unity: `C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe` (Unity 6.6.2f1)
- Blender: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (Blender 5.2.0 LTS)
- Main gameplay scene: `Assets/Scenes/Samplespace.unity`; menu scene: `Assets/GoldenHour/Scenes/MainMenu.unity`
- New Input System only (`activeInputHandler: 1`); never use the legacy `UnityEngine.Input` API.
- Editor automation: `Assets/GoldenHour/Editor/GoldenHourProjectBuilder.cs`
  (menu `GoldenHour/Build Project`, `Validate Project`, `Build Windows Player`, `Write Testing Report`).
- Batch command pattern (Unity Editor must be closed):
  ```powershell
  & 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe' -batchmode -nographics -quit `
    -projectPath 'C:\gdit\My project (3)\My project (2)' `
    -executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.BuildProject -logFile <log>
  ```

- End-to-end test of the built player (exit code 0 = all passed; writes `autotest_results.txt` + screenshots):
  `Builds\GoldenHour\GoldenHour.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -goldenhour-autotest <dir>`
- Player prefs: `GameSettings` (volume, mouse sensitivity, difficulty). Restart sets `GameplayController.ShuffleOnNextLoad`. Visual assets (unlit/smoke/sky materials, `GoldenHour/Settings/GH_PostProcessing.asset`) are created by Build Project.
- Imported Blender FBXs flip the depth axis: the kit hinge ends up on the model's +Z side (kit is placed at yaw -20 so it opens away from the player).

## Requirements

Complete, playable Windows 3D educational road-accident rescue and first-aid triage simulation. Loop:
**ACCIDENT → FIND PATIENTS → EXAMINE → TRIAGE → FIRST-AID KIT → SELECT SUPPLY → SIMULATED AID → OBJECTIVES → FINAL REPORT**

- Medical content is clearly labelled educational, not medical advice/training; disclaimer on How to Play and final report.
- Keep the existing `Player`, `GameManager`, `ScoreManager`, `EventSystem`.
- Small single-purpose C# scripts in namespace `GoldenHour`; ScriptableObjects for patient and supply data;
  Editor menu automation to create/configure scenes, objects, UI, references, events, build settings.
- Preserve existing source assets; new Blender assets go in new paths. Blender runs in background mode; never
  claim an asset was generated unless the command succeeded and files exist.
- Main menu → gameplay → final report: Start, How to Play, Settings/volume, Exit, Restart, Main Menu. Both scenes in Build Settings.
- First-person WASD/mouse, gravity/collision, E interaction raycast/range/prompts, Esc pause, sensible cursor lock.
- Low-poly accident environment: road/markings/sidewalks, damaged vehicle/debris, barriers, scenery, kit, patients.
- Briefing, objectives with live checkmarks, timer, final summary.
- Five data-driven patients (identity, age, injury/symptoms, breathing/responsiveness/bleeding/mobility,
  expected triage, reasoning, recommended supply/action, visible status). Distribution 1–2 RED, 2 YELLOW, 1–2 GREEN.
  Educational START-style rule: walking → GREEN; not breathing / severe bleeding / unresponsive → RED; else YELLOW.
- Examination UI shows observations without revealing expected triage; RED/YELLOW/GREEN choices; then feedback/reasoning, reassessment.
- First-aid kit with distinct lid/base and opening animation, inventory, simulated safe aid only, wrong-supply feedback without reward, live patient status.
- Scoring: correct triage/aid/objectives, small penalties, capped time bonus that never outweighs accuracy; results/rating/disclaimer.
- Readable TMP UI (white, emergency red, charcoal/light grey); working volume; procedural audio OK.
- Deliverables: `PROJECT_ANALYSIS.md`, `DEV_LOG.md` (one short entry per phase), `TESTING_REPORT.md`, `README.md`,
  Windows build in `Builds/GoldenHour/`.

## Recovery

- Baseline commit `9df93a0`. Never reset/discard work; fix forward.
- Template leftovers `Assets/TutorialInfo/` and `Assets/Readme.asset` are to be deleted.

## Autonomy rules

- Fully autonomous: do not ask the user for permission/confirmation or to create/edit files; decide from the spec.
- Fix errors in a loop: change → compile → read the log → fix → repeat.
- Commit after each verified phase.
- Never claim something works without verification evidence (logs, files present, exit codes).
- Stop only for a hard blocker (Unity Editor open, missing install, needed download) and name the one exact action needed.
- Finish with one final report: what was built, how verified, build location, manual playthrough checks.
