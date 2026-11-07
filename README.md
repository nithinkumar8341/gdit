# Golden Hour (v2)

A first-person **educational** serious game about the first minutes of a mass-casualty incident:
- make the scene safe;
- call 112;
- sort casualties with **START triage**;
- give simple first aid;
- hand people to ambulances in the right order while the golden-hour clock runs.

It is a Windows game, built with **Unity 6000.6.2f1** (URP, Input System, AI Navigation).

![Examination beside the casualty](Docs/Screenshots/examination.png)

| | |
|---|---|
| ![Supply bag](Docs/Screenshots/supply_bag.png) | ![Visible treatment](Docs/Screenshots/treated.png) |
| ![Stretcher carry](Docs/Screenshots/stretcher_carry.png) | ![Body-part marker](Docs/Screenshots/body_marker.png) |

## Features

- **Four data-driven scenarios** (ScriptableObjects): Training Yard (guided tutorial), Highway Collision, Night Bus Rollover and Collapsed Community Hall.
- **START triage** with physical RED/YELLOW/GREEN/BLACK tags. First-tag scoring; a retriage needs a fresh examination.
- **In-world examination.** You kneel beside the casualty, who stays visible next to a compact side panel. Timed checks show a world-space marker on the body part (chest, head, forearm, wound).
- **Supply bag** with limited dressings, blankets and gloves, plus items that are not used in triage and explain why.
- **Live patient tasks** ("Control bleeding on T02"), each revealed only by a finding.
- **Visible aid:** dressings and foil blankets (Blender models) appear on the body.
- **Seeded physiology** with deterioration and preventable deaths; adaptive difficulty.
- **112 dispatch report**, walking-wounded shout, hazards, bystanders, stress system and gloved first-person hands.
- **Ambulance waves.** Two NavMesh paramedics carry casualties on a stretcher in carry poses, and handovers are scored by priority.
- **Tabbed debrief:** timeline, your tag against START, heatmap, telemetry JSON, stars and unlocks.
- **Accessibility:** rebinding, gamepad, subtitles, colour-blind tags, text size, reduce motion.

## Tech stack

Unity 6000.6.2f1 · URP 17 · Input System 1.20 · AI Navigation (runtime NavMesh) · TextMesh Pro · Unity Test Framework · Blender 5.2 (background-mode Python generators for all models) · Python for procedural audio.

> **Disclaimer:** Golden Hour is an educational simulation only. It is **not** medical training or medical advice. The content has **NOT been medically reviewed** and should be checked by a qualified first-aid trainer. In a real emergency, call your local emergency number and follow professional guidance.

Further documents:
- [GDD.md](GDD.md): design, systems, learning objectives, ethics.
- [PLAYTEST_PLAN.md](PLAYTEST_PLAN.md)
- [PRESENTATION_OUTLINE.md](PRESENTATION_OUTLINE.md)
- [ASSET_CREDITS.md](ASSET_CREDITS.md)
- [TESTING_REPORT.md](TESTING_REPORT.md)
- [DEV_LOG.md](DEV_LOG.md)

## Play

1. Unzip `Release/GoldenHour_Windows.zip`, or run `Builds/GoldenHour/GoldenHour.exe`. When you ship the folder, leave out `GoldenHour_BackUpThisFolder_ButDontShipItWithYourGame`.
2. Main menu (live 3D backdrop):
   - **Campaign** (four scenarios, unlocked in order by earning at least one star);
   - **How to Play** (START steps, simplifications and disclaimer);
   - **Settings**, **Feedback** (playtest questionnaire), **Credits**, **Exit**.
3. Each scenario opens with a skippable fly-in and a briefing.

### Scenarios

| Scenario | Casualties | What's new |
|---|---|---|
| Training Yard | 3 | Guided 8-step tutorial. |
| Highway Collision | 6 | Traffic, fuel leak and fire; a motorcyclist; a casualty who worsens. |
| Night Bus Rollover | 10 | Night, rain, one dressing for two bleeders, an airway emergency, an expectant passenger. |
| Collapsed Community Hall | 12 | Unstable structure, gas, a live wire, children, a walking-wounded crowd. |

### Controls (rebindable in Settings)

| Keyboard / mouse | Gamepad | Action |
|---|---|---|
| W A S D, mouse | Left stick, right stick | Move, look |
| Left Shift | Left stick press | Walk faster |
| E | A / South | Interact: examine, mark a hazard, take the bag, calm a bystander, talk to the paramedic |
| C | B / East | Kneel |
| G | D-pad right | Put on gloves |
| R | D-pad up | Radio: 112 dispatch report and radio log |
| Q | D-pad left | Shout: "If you can walk, go to the green zone" |
| Tab | Select / View | Casualty tablet |
| Space / left mouse | Right trigger | Hold pressure, confirm minigame steps, CPR compressions |
| A / D | Left stick X | Roll the casualty (recovery position) |
| F10 | Right stick press | Photo mode (freezes the clock; Space saves a PNG) |
| Esc | Start | Pause, close a panel, skip a cutscene |

### How a scenario plays

1. **Scene safety.** Collect the first-aid bag, mark hazards and put on gloves. Touching a casualty without gloves costs points.
2. **Dispatch.** Radio 112 with the location, casualty count and hazards. Each field is scored.
3. **Walking wounded.** Shout. Everyone who can walk goes to the green zone and is tagged GREEN.
4. **Examine and tag.** Kneel and use timed checks: breathing (open the airway if absent), rate, capillary refill or radial pulse, commands. Then attach a physical tag: RED, YELLOW, GREEN or BLACK.
   - Only your first tag is scored in full.
   - A retag needs a fresh examination.
5. **First aid.**
   - Hold-to-apply pressure dressing.
   - Airway opening.
   - A recovery-position roll minigame.
   - A blanket.
   - Reassurance.
   - A CPR rhythm minigame (100–120/min). It is never scored, and the game explains why CPR is deprioritised in mass-casualty triage.
6. **Ambulances.** They arrive in waves. Hand over the highest START priority first; the paramedics carry casualties on a stretcher.
7. **Debrief.** Tabs show:
   - **Summary:** stars and feedback.
   - **Timeline:** with decision times.
   - **Casualties:** your tag against START, with the START step that applied.
   - **Map:** a movement heatmap.
   - **Stats.**

   Each run is saved as JSON.

Casualties deteriorate on a seeded physiology model, and untreated life threats can become preventable deaths. **Adaptive difficulty** uses the mean of your last three score ratios: 80% or more speeds up deterioration (×1.2); 45% or less slows it (×0.85) and turns hints on. Settings also offer Easy and Normal.

### Accessibility and settings

- Graphics presets Low/Medium/High and resolution.
- Key rebinding.
- Subtitles; colour-blind tags with shapes, stripes and priority numbers; text size; reduce motion; stress effects on/off.
- Master, music and SFX volume; mouse sensitivity.
- Full gamepad support.

### Saved data

`%USERPROFILE%\AppData\LocalLow\Golden Hour Learning\Golden Hour\` holds:

| File / folder | Contents |
|---|---|
| `profile.json` | Campaign, best scores, recent ratios |
| `Runs\*.json` | Telemetry per run |
| `Feedback\*.json` | Questionnaire answers |
| `Photos\*.png` | Photo-mode captures |

## Project layout

| Path | Contents |
|---|---|
| `Assets/GoldenHour/Core/` | `GoldenHour.Core` assembly: START evaluator, physiology and deterioration, triage records, scoring, adaptive difficulty, telemetry (pure C#) |
| `Assets/GoldenHour/Scripts/V2/` | Runtime: `Director/` (IncidentDirector partials, FirstAidMinigame), `Characters/`, `World/` (environments, hazards, ambulances, paramedics, bystanders), `UI/`, `Input/`, `Presentation/` (cutscenes, viewmodel hands, stress, photo mode), `Debrief/`, `Data/` |
| `Assets/GoldenHour/Scripts/` (other folders) | Shared code: player controller, audio, menu, UI factory, environment, settings |
| `Assets/GoldenHour/ScriptableObjects/V2/` | `Casualties/*.asset` (CasualtyProfile), `Scenarios/*.asset` (ScenarioData), `Campaign.asset` |
| `Assets/GoldenHour/Blender/` | `generate_assets.py` (v1 props), `generate_v2_assets.py` (rigs, vehicles, stretcher, props), `generate_v3_assets.py` (treatment props, gloved hands), `generate_audio.py`, sources and FBX exports |
| `Assets/GoldenHour/Editor/` | `GoldenHourProjectBuilder` (menu **GoldenHour**), `GoldenHourContent` (scenario data), `V2ModelPostprocessor` |
| `Assets/GoldenHour/Tests/Editor/` | Edit-mode NUnit tests |
| `Assets/Scenes/Samplespace.unity`, `Assets/GoldenHour/Scenes/MainMenu.unity` | Gameplay and menu scenes |
| `Docs/TestEvidence/` | Latest autotest log, benchmark and screenshots |

## Rebuild and test

Requirements: Unity **6000.6.2f1**; Blender **5.2** and Python 3 are needed only to regenerate assets. **Close the Unity Editor** before running batch commands.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe'
$proj  = 'C:\gdit\My project (3)\My project (2)'
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'

# Optional: regenerate models and audio
& $blender --background --factory-startup --python "$proj\Assets\GoldenHour\Blender\generate_assets.py"
& $blender --background --factory-startup --python "$proj\Assets\GoldenHour\Blender\generate_v2_assets.py"
& $blender --background --factory-startup --python "$proj\Assets\GoldenHour\Blender\generate_v3_assets.py"
python "$proj\Assets\GoldenHour\Blender\generate_audio.py"

# Content, scenes, references, build settings + validation
& $unity -batchmode -nographics -quit -projectPath $proj -logFile build.log `
  -executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.BuildProject

# Edit-mode unit tests
& $unity -batchmode -nographics -projectPath $proj -runTests -testPlatform EditMode -testResults tests.xml -logFile tests.log

# Windows player -> Builds/GoldenHour/GoldenHour.exe
& $unity -batchmode -nographics -quit -projectPath $proj -logFile player.log `
  -executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.BuildWindowsPlayer

# End-to-end playtest of all four scenarios (exit code 0 = all checks passed)
Builds\GoldenHour\GoldenHour.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -goldenhour-autotest out\autotest

# Frame-rate benchmark (preset Low|Medium|High)
Builds\GoldenHour\GoldenHour.exe -screen-fullscreen 1 -screen-width 1920 -screen-height 1080 -goldenhour-benchmark out\bench -goldenhour-preset Medium
```

## Architecture

- **`GoldenHour.Core`** (pure C#, no scene dependencies) holds the rules: START evaluation, physiology and deterioration, triage records, scoring, patient tasks and telemetry. Edit-mode NUnit tests cover them.
- **`IncidentDirector`** (partial classes: core loop, Exam, Framing, Supplies, Panels, FirstAid) runs a scenario. It spawns casualties, hazards and ambulances, advances the clock and drives the HUD and panels.
- **World and characters:** `Casualty` + `HumanoidBody` (procedurally animated Blender rigs), `TreatmentVisuals`, `Ambulance`/`Paramedic`/`Walker` (NavMesh), `Hazard`, environments.
- **Editor automation:** `GoldenHourProjectBuilder` (menu **GoldenHour**) generates missing content, wires scenes and references, validates the project including Blender imports, and builds the player. Content generation never overwrites Inspector edits; **GoldenHour/Regenerate Content** resets it on purpose.

## Tests

- **Edit-mode unit tests:** 29, covering START rules, physiology, scoring, retriage, patient tasks and treatment names.
- **Validate Project:** content rules, scenes, references, audio, and Blender import checks (rig joints, vehicle orientation, treatment props, hands).
- **End-to-end autotest of the built player** (`-goldenhour-autotest`): plays all four scenarios with real frames. It uses an independent score tally that must equal the game's ledger exactly. It writes `autotest_results.txt` and screenshots. See [TESTING_REPORT.md](TESTING_REPORT.md).

## Known limitations

- The medical content has **not** been reviewed by a qualified trainer. Timings are illustrative; children use adult START; CPR is reduced to a rhythm exercise.
- Characters are rigid-segment rigs animated in code (no skinned meshes), so joints can show small gaps in extreme poses.
- The autotest drives minigame inputs and some panel choices through test hooks. Feel, audio mix and readability need human playtests (see PLAYTEST_PLAN.md).
- Windows only; tested on one laptop (Intel Iris Xe and RTX 2050).
- The docked examination panel is laid out for 16:9. Very narrow aspect ratios or text size above 100% can crowd it.

## License

Code and original assets: [MIT](LICENSE). Third-party packages keep their own licences (see [ASSET_CREDITS.md](ASSET_CREDITS.md)).
