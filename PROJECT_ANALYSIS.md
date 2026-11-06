# GOLDEN HOUR Project Analysis

## Project baseline

- Unity `6000.6.2f1`, URP `17.6.0`, Input System `1.20.0`, UGUI, and TextMesh Pro are installed.
- `ProjectSettings/ProjectSettings.asset` selects the new Input System only (`activeInputHandler: 1`).
- The active-looking gameplay scene is `Assets/Scenes/Samplespace.unity`; `Assets/GoldenHour_Prototype.unity` is a separate minimal light-and-camera scene.
- Existing project code consists of 14 scripts under `Assets/Scripts/` and one UI controller under `Assets/UI/`.
- The scene already has Player, GameManager, ScoreManager, GoldenHourUI, EventSystem, TestGround, and Patient_Test objects. The EventSystem uses `InputSystemUIInputModule`.
- Blender `5.2.0 LTS` is available at `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`; Unity `6000.6.2f1` is available at `C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe`.

## What currently works

- Player movement and look use `UnityEngine.InputSystem` keyboard/mouse APIs, and patient interaction uses a forward physics raycast.
- `STARTTriageEvaluator` contains a basic walking / breathing / respiration / perfusion / command-following classification.
- `GameManager` has a countdown timer; `ScoreManager` can record correct or incorrect triage selections.
- The scene has a functional TMP canvas and input-system EventSystem components, although its current content is placeholder UI.

## Broken or incomplete

- The current canvas is a placeholder: `PatientInfoText` starts as “PATIENT INFORMATION”, and `GreenButton` displays “New Text”. The scene has no functioning triage-choice flow or clean examination/close flow.
- Only one `Patient_Test` object is present. There are no PatientData or SupplyData ScriptableObjects, no complete patient set, no first-aid kit/inventory/treatment loop, no objective tracking, and no results/report flow.
- `GameManager` only logs timer start/end; it does not coordinate briefing, objectives, pause, transitions, or completion UI. Scoring only records triage category equality.
- `PlayerInteraction` only handles patients and has no interaction prompt or other interactable types. Player cursor/pause behavior and interaction UI are absent.
- The Player has `PlayerLook` on both the Player and its child Camera in the scene. The controller adds a CharacterController at runtime instead of serializing one. The patient is placed at `(0, 1, 2)`, directly in front of the camera at the start, matching the reported visible capsule.
- No gameplay-ready environment, five characterized patient models, kit/supplies, or audio assets are present. `Assets/Audio/`, `Assets/Prefabs/`, and `Assets/ScriptableObjects/` contain no non-meta files.
- The game has no main-menu scene, tested build settings, Windows build, README, or testing report.

## Redundant or template content

- `Assets/TutorialInfo/` and `Assets/Readme.asset` are template leftovers and are candidates for the explicitly permitted cleanup.
- `Assets/TextMesh Pro/Examples & Extras/` contains TMP samples, not game content. Preserve it unless a later verified cleanup establishes it is unused.
- `Assets/GoldenHour_Prototype.unity` duplicates neither the active gameplay scene nor any systems; inspect before deciding whether to keep or repurpose it.
- `PatientProfileTester`, `GoldenHourLogicTester`, and `GoldenHourIntegrationTester` are test helpers; they are not currently listed as components in `Samplespace.unity`.

## Plan

1. Finish baseline verification, retain this analysis, and establish clean project folders without moving or replacing existing assets.
2. Fix the existing player and build a playable accident scene, preserving the existing Player, managers, and EventSystem.
3. Implement data-driven patients and examination, then triage, results, and correct reassessment/scoring.
4. Generate new low-poly assets with Blender, export into new versions under `Assets/GoldenHour/Blender/`, and add kit, supplies, inventory, and safe educational action feedback.
5. Add objectives, pause/settings/menu transitions, final report, procedural audio, and polish.
6. Run the project validation and complete build, test the full player loop, write the requested testing report and README, and make the Windows build.

Design decisions and implementation must keep all medical content explicitly educational and non-clinical.

## Verification state (original analysis — superseded)

The statements in this document describe the project **before** implementation. At that time Unity was open, so batch compilation could not run, and no Git repository existed. Both have since been resolved. For current status see `DEV_LOG.md`, and for verification evidence see `TESTING_REPORT.md` and `Docs/TestEvidence/`.
