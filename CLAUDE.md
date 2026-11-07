# GOLDEN HOUR — Claude Code project instructions

Current progress: `DEV_LOG.md`. Audit and approved improvement plan: `AUDIT_FINAL.md`. v1-era documents are archived in `Docs/history/`.
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
- Player prefs: `GameSettings` (volume, mouse sensitivity, difficulty). v2 gameplay runs on `IncidentDirector` (the v1 GameplayController stack was removed in M2); casualty positions are seeded-shuffled unless `Session.SeedOverride` is set. Benchmark: `-goldenhour-benchmark <dir> [-goldenhour-preset Medium] [-goldenhour-capped]`. Visual assets (unlit/smoke/sky materials, `GoldenHour/Settings/GH_PostProcessing.asset`) are created by Build Project.
- Imported Blender FBXs flip the depth axis: the kit hinge ends up on the model's +Z side (kit is placed at yaw -20 so it opens away from the player).
- v2 Blender exports (`generate_v2_assets.py`) are imported with `bakeAxisConversion` by `V2ModelPostprocessor`; Unity (x, y, z) = Blender (x, z, y).
- Content: Build Project creates missing casualty/scenario assets only and never overwrites Inspector edits. `GoldenHour/Regenerate Content` resets them to the defaults in `GoldenHourContent.cs`. Evidence method: `GoldenHourProjectBuilder.CheckContentPreservation`.
- Edit-mode unit tests: `-runTests -testPlatform EditMode` (`Assets/GoldenHour/Tests/Editor`).

## Requirements (v2)

Complete, playable Windows first-person educational serious game about the first minutes of a mass-casualty road or
disaster incident. Loop:
**BRIEFING → SCENE SAFETY (hazards, gloves) → 112 DISPATCH → WALKING-WOUNDED SHOUT → EXAMINE → START TAG → FIRST AID →
AMBULANCE HANDOVER BY PRIORITY → DEBRIEF → REPLAY / UNLOCK**

- Educational only. Disclaimers in the menu, How to Play, tutorial, every briefing and every debrief tab. The content has NOT
  been medically reviewed. Describe START accurately and mark the simplifications (children use adult START, illustrative
  timings, CPR reduced to rhythm). No gore and no unsafe procedures. Do not invent citations.
- Four data-driven scenarios (ScriptableObjects): Training Yard, Highway Collision, Night Bus Rollover, Collapsed
  Community Hall. Seeded physiology and deterioration, START RED/YELLOW/GREEN/BLACK, first-tag scoring, retriage only after
  re-examination, limited supplies, ambulance waves with NavMesh paramedics, priority-scored handover.
- Simulation rules live in the pure-C# `GoldenHour.Core` assembly with edit-mode tests; gameplay in `IncidentDirector`.
- New Input System only; namespace `GoldenHour`; keep the scenes and the `Player`/`EventSystem` objects; Editor automation
  wires scenes, references and build settings.
- Preserve existing source assets; new Blender assets go in new files. Blender runs in background mode; never claim an
  asset was generated unless the command succeeded, the files exist and the import validates.
- Quality gates for every change: 0 compiler errors/warnings in project code, unit tests, Validate Project, and the full
  player autotest (never reduce the number of checks). 60 fps at 1080p Medium on integrated graphics.
- Deliverables: `README.md`, `GDD.md`, `DEV_LOG.md`, `TESTING_REPORT.md`, `PLAYTEST_PLAN.md`, `PRESENTATION_OUTLINE.md`,
  `ASSET_CREDITS.md`, Windows build in `Builds/GoldenHour/`, `Release/GoldenHour_Windows.zip` (not committed).
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
