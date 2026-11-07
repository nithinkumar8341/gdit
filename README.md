# Golden Hour

A first-person, low-poly **educational** road-accident rescue and first-aid triage simulation for Windows, built with Unity 6.6 (URP, Input System).

> **Disclaimer:** Golden Hour is an educational simulation. It is **not** medical training, medical advice or a substitute for emergency services. In a real emergency, call local emergency services and follow qualified professional guidance.

## Play

1. Run `Builds/GoldenHour/GoldenHour.exe` (Windows 64-bit). Ship or copy the whole `Builds/GoldenHour/` folder **except** `GoldenHour_BackUpThisFolder_ButDontShipItWithYourGame`.
2. Main menu: **Start Scenario**, **How to Play**, **Settings** (master volume, mouse sensitivity, difficulty; all saved), **Exit**. Scene changes fade through black.
3. Read the briefing and press **Begin Scenario**.

### Controls

| Input | Action |
|---|---|
| W A S D | Move |
| Mouse | Look |
| Left Shift | Walk faster |
| E | Examine the person / open the first-aid kit you are looking at (shown by the on-screen prompt) |
| Esc | Pause (volume, mouse sensitivity, Resume, Restart, Main Menu) or close the current panel |

### Scenario loop

**Accident → find patients → examine → triage → first-aid kit → select supply → simulated aid → objectives → final report**

- You start just behind the barriers with the crash in view. A tip explains the controls for the first 15 seconds.
- Five people are at the scene. Each has a pulsing beacon above them, and the arrow at the top of the HUD points to the nearest person you have not examined (then to the kit while aid is pending). Look at a person and press **E** to see their observations: walking, breathing, responsiveness and bleeding. The expected answer is **not** shown until you choose **RED / Immediate**, **YELLOW / Delayed** or **GREEN / Minor**.
- Feedback explains the reasoning. You can **reassess** anyone at any time; correcting a decision updates the score.
- Open the red first-aid kit (the lid animates open). Choose a triaged person (highest priority listed first), then pick a supply. A mismatched supply gives feedback and no credit.
- Objectives on the HUD tick off live. The scenario ends when everyone has received simulated aid or the 15-minute timer runs out. The timer pauses while a panel is open.
- The final report shows the score out of 1050, a rating, per-person results and the disclaimer, with **Restart** and **Main Menu**.

**Difficulty:** *Easy* adds a checklist of what to look for to each examination (walking, breathing, severe bleeding, responsiveness) and a hint on the supply screen. *Normal* gives no hints. Scoring is identical.

**Replay:** each **Restart** puts the five people in different positions. Their data, the triage rule and the scoring do not change.

**Simplified START-style rule used by the game:** able to walk → GREEN; not breathing, severe visible bleeding or unresponsive → RED; otherwise → YELLOW.

**Scoring:** correct triage +100, simulated aid +75, each of 5 objectives +25. Each wrong triage decision −15, each wrong supply −10. The time bonus is capped at +50, so it can never outweigh a single correct triage.

## Visuals and performance

Warm late-afternoon sun with soft shadows, a procedural sky, linear haze and URP post-processing: bloom, vignette, colour grading, white balance, and the renderer's screen-space ambient occlusion at half resolution. The damaged car has flashing hazard lights and engine smoke. The roadside has skid marks, glass, fences, grass, street lights and signs. To keep it smooth on laptops, shadows use 2 cascades up to 45 m, there is one extra point light, and the smoke is limited to 60 particles.

## Project layout

| Path | Contents |
|---|---|
| `Assets/Scenes/Samplespace.unity` | Gameplay scene (keeps the original Player, GameManager, ScoreManager, EventSystem) |
| `Assets/GoldenHour/Scenes/MainMenu.unity` | Main menu scene (build index 0) |
| `Assets/GoldenHour/Scripts/` | Runtime code, namespace `GoldenHour`: `Core/` (GameplayController, ObjectiveTracker, Palette, MaterialFactory), `Environment/`, `Patients/`, `Triage/`, `FirstAid/`, `Player/`, `UI/`, `Audio/`, `Testing/` |
| `Assets/Scripts/` | Original project scripts. `GameManager` and `ScoreManager` are extended and still used |
| `Assets/GoldenHour/ScriptableObjects/` | `Patient_P01..P05` (PatientData) and five `Supply_*` (SupplyData) assets |
| `Assets/GoldenHour/Blender/` | `generate_assets.py`, `generate_audio.py`, the `.blend` source and FBX exports |
| `Assets/GoldenHour/Audio/` | Procedurally generated WAVs (ambience, UI click/hover, footstep, kit, objective, patient) |
| `Assets/GoldenHour/Materials/`, `Assets/GoldenHour/Settings/` | Runtime material templates, smoke material/texture, sky material and the post-processing profile (all created by **Build Project**) |
| `Assets/GoldenHour/Editor/GoldenHourProjectBuilder.cs` | Editor automation (menu **GoldenHour**) |
| `Docs/TestEvidence/` | Latest automated playtest log and screenshots |

## Rebuild from source

Requirements: Unity **6000.6.2f1** and Blender **5.2** (only needed to regenerate assets). **Close the Unity Editor** before running batch commands.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe'
$proj  = 'C:\gdit\My project (3)\My project (2)'

# Optional: regenerate Blender models and procedural audio
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup `
  --python "$proj\Assets\GoldenHour\Blender\generate_assets.py"
python "$proj\Assets\GoldenHour\Blender\generate_audio.py"

# Create data assets + scenes, wire references and build settings, then validate
& $unity -batchmode -nographics -quit -projectPath $proj -logFile build.log `
  -executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.BuildProject

# Validate only
& $unity -batchmode -nographics -quit -projectPath $proj -logFile validate.log `
  -executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.ValidateProject

# Windows player -> Builds/GoldenHour/GoldenHour.exe
& $unity -batchmode -nographics -quit -projectPath $proj -logFile player.log `
  -executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.BuildWindowsPlayer
```

The same three actions are available in the Editor under the **GoldenHour** menu. `Build Project` is idempotent: it updates existing assets in place.

## Automated playtest

The player build contains an end-to-end playtest that only runs when explicitly requested:

```powershell
.\Builds\GoldenHour\GoldenHour.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 `
  -goldenhour-autotest .\autotest-output
```

It covers:

- the menus, fades and button feedback;
- settings: volume, sensitivity and difficulty;
- WASD (with footsteps) and E/Esc through the Input System;
- the compass, beacons and tutorial;
- the visual setup: overlay HUD, fog, post-processing, smoke and hazard lights;
- raycast interaction with every patient;
- triage, including a wrong-then-reassessed decision and the Easy hint;
- kit opening, and wrong and correct supplies;
- the final report;
- Restart with shuffled positions and Normal difficulty;
- Main Menu.

The playtest restores your saved settings when it finishes. It writes `autotest_results.txt` and screenshots, then exits with code 0 (all passed) or 1. See `TESTING_REPORT.md`.

## Editing content

Patient and supply data are ScriptableObjects. Edit them in the Inspector, or change `CreateGameData()` in the builder and rerun **Build Project**. **Validate Project** rejects any patient whose observations do not produce its expected triage under the game's rule, and checks the 1–2 RED / 2 YELLOW / 1–2 GREEN distribution.
