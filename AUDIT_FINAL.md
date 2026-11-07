# Golden Hour — Final Project Audit

**Date:** 2026-11-07
**Commit audited:** `3717f4b`
**Scope:** every scene, script, model, material, audio clip and data asset under `Assets/`, plus `ProjectSettings/`, `Packages/` and the docs.
**Method:** read the code and assets. Mapped scene → script references by GUID. Inspected the existing test evidence (`Docs/TestEvidence/`). Ran the **existing** `Builds/GoldenHour/GoldenHour.exe -goldenhour-autotest` into a temp folder outside the project. Nothing was compiled, built or edited; this file is the only change.

**Labels:**

| Label | Meaning |
|---|---|
| COMPLETED | Works and is verified. |
| PARTIALLY COMPLETED | Works, but part of the intent is missing. |
| NOT IMPLEMENTED | Absent. |
| NEEDS IMPROVEMENT | Works, but is weak, buggy or below the vision. |

## Audit run (fresh evidence)

| Run | Result |
|---|---|
| Existing build, `-goldenhour-autotest`, 1600×900 windowed, output to `%TEMP%` | **257/257 checks passed. 0 errors or exceptions logged. Exit code 0.** |
| First attempt | Shut down right after engine start, before any scene loaded, with no results file. The log shows a normal shutdown sequence, so this was treated as an environmental/window event. The immediate retry above passed. **Flag:** watch for this in manual testing. |
| Scene → script map (GUIDs) | MainMenu: `MainMenuController`, `AccidentEnvironment`, `GoldenHourAudio`. Samplespace: `IncidentDirector`, `FirstPersonPlayer`, `Viewmodel`, `AccidentEnvironment`, `GoldenHourAudio`, plus legacy `GameManager` and `ScoreManager` (inert, see §8). Everything else is created at runtime from code. **There are no prefabs in the project.** |

---

## 1. Existing functionality

| Area | What exists | Status | Evidence |
|---|---|---|---|
| Scenes and flow | MainMenu (build index 0) → Samplespace (gameplay) → debrief → retry, next scenario or menu. Loading screen with tips. | COMPLETED | `EditorBuildSettings.asset`; autotest "Main menu scene loads first", "Loading screen shows a tip", "Main Menu returns to the menu" |
| Simulation core | `GoldenHour.Core` assembly: START evaluator, physiology and deterioration, triage records, scoring ledger, handover and dispatch rules, adaptive difficulty, telemetry | COMPLETED | `Assets/GoldenHour/Core/*.cs`; 26/26 edit-mode tests (`Docs/TestEvidence/editmode_tests.xml`) |
| Scenario content | 4 scenarios, 31 casualty profiles, ScriptableObjects (`ScenarioData`, `CasualtyProfile`, `Campaign.asset`) | COMPLETED | `ScriptableObjects/V2/`; `ValidateProject` rules |
| Gameplay director | `IncidentDirector` (4 partial files, about 1,800 lines): phases, clock, interactions, panels, ambulances, scoring, telemetry | COMPLETED (see §8 for its size) | Autotest scenario runs |
| UI | Menu, campaign, How to Play, settings, credits, feedback form, HUD (objectives, clock, supplies, compass, radio, subtitles, toasts, tips), modals, debrief tabs | COMPLETED, with layout issues | Screenshots in `Docs/TestEvidence` and the audit run |
| Art | 15 FBX models (6 v1, 9 v2) from Blender scripts; environments built at runtime from primitives; 6 materials; post-processing profile | PARTIALLY COMPLETED (see §5) | `Blender/Exports`, `AccidentEnvironment.cs`, `ScenarioEnvironment.cs` |
| Audio | 19 procedural WAVs: siren, radio, crowd, heartbeat, breathing, weather, 3 music layers, UI and effect sounds | COMPLETED (synthetic quality) | `Audio/`; autotest "layered music and weather ambience play" |
| QA tooling | Autotest (257 checks), benchmark mode, edit-mode tests, `ValidateProject` | COMPLETED | `Testing/*.cs`, `TESTING_REPORT.md` |
| Docs | GDD, playtest plan, presentation outline, credits, README, testing report, dev log | COMPLETED; CLAUDE.md §Requirements is stale (v1) | Files in the root |

## 2. Completed gameplay mechanics (verified)

| Mechanic | Status | Evidence |
|---|---|---|
| First-person movement, look, sprint, kneel (camera lowers), pause | COMPLETED | Autotest: real W / C / Esc keys; `FirstPersonPlayer.cs` |
| Scene safety: mark hazards; danger-zone penalty | COMPLETED | Autotest "every hazard marked"; `IncidentDirector.UpdateDanger` |
| 112 dispatch minigame, scored per field | COMPLETED | Autotest "dispatch scored field by field" |
| Walking-wounded shout → NavMesh walk to green zone → auto GREEN | COMPLETED | Autotest "walking wounded reach the green zone on the NavMesh" |
| Interactive examination by timed actions (walk, breathing, airway, rate, circulation, commands, bleeding); findings never reveal the answer | COMPLETED | Autotest reconstructs START from the finding text only; screenshot `highway_06_examination` |
| START triage RED / YELLOW / GREEN / BLACK with physical tags (colour-blind shapes and numbers) | COMPLETED | `Triage.cs`; autotest "physical tag placed" |
| First-tag scoring; retriage only after re-examination; justified versus unjustified retriage | COMPLETED | Autotest highway and building retriage checks; unit tests |
| Deterioration, worsening YELLOW → RED, preventable death | COMPLETED | Autotest "an untreated YELLOW worsens", "untreated critical casualty dies at the defined time" |
| Treatments: hold-to-apply dressing, airway, recovery-position roll minigame, blanket, reassure, gloves-first penalty, limited supplies | COMPLETED | Autotest minigame and supply checks |
| CPR rhythm minigame (100–120/min), unscored, with the deprioritisation note | COMPLETED | Autotest "30 compressions in rhythm measure a rate of 100–120" |
| Timed ambulance waves, paramedics on NavMesh, stretcher carry, priority-scored handover | COMPLETED | Autotest tutorial NavMesh carry; building wrong-order penalty |
| Golden-hour clock, time bonus capped below accuracy | COMPLETED | `ScoreRules.TimeBonus`; autotest exact-score tallies (690 / 1330 / 1477 / 1892) |
| Debrief: Summary, Timeline, Casualties (START step), Map heatmap, Stats; 1–3 stars; feedback | COMPLETED | Autotest debrief checks; screenshots |
| Telemetry JSON, profile, campaign unlocks, adaptive difficulty | COMPLETED | Autotest "profile records the run and unlocks the next scenario"; unit tests |
| Stress system, cutscenes, photo mode, viewmodel hands | COMPLETED (visual quality: §5) | Autotest checks |
| Settings: 3 volumes, sensitivity, rebinding, difficulty, subtitles, colour-blind tags, text size, reduce motion, stress toggle, graphics preset, resolution | COMPLETED (rebinding and preset verified at runtime; the rest by code reading) | `SettingsPanel.cs`; autotest "Key rebinding changes the binding", "Medium preset ..." |
| Performance: 60 fps at 1080p Medium on Intel Iris Xe | COMPLETED | `Docs/TestEvidence/benchmarks/iris-xe_1080p_medium_vsync.txt` |

## 3. Incomplete functionality and defects

| Item | Status | Evidence / observation |
|---|---|---|
| **HUD objectives "Give indicated aid" and "Hand over to ambulances" never tick.** They are hard-coded `Check(false)`. | NEEDS IMPROVEMENT (bug) | `IncidentDirector.cs:981-982` |
| Hazard objective shows only the count marked, `(0)`, with no total, so players can't tell when they are done | NEEDS IMPROVEMENT | `IncidentDirector.cs:970`; screenshot `highway_02_scene` |
| Score toasts that arrive in the same frame overlap, and toasts draw over modal panels such as the examination screen | NEEDS IMPROVEMENT | Audit screenshot `highway_06_examination`; `IncidentHud.Toast` spawns every toast at y=60 |
| The tip panel still overlaps the radio panel at 1600×900 | NEEDS IMPROVEMENT | Audit screenshot `highway_02_scene` |
| "AID GIVEN" and the debrief "aid:" show raw enum names (`PressureDressing, RecoveryPosition`) | NEEDS IMPROVEMENT | `IncidentDirector.Exam.cs` (`Applied.ToString()`); debrief Casualties screenshot |
| The examination is a full-screen menu that hides the casualty. Examination is not physical or diegetic (no pointing at the wrist or chest). | NEEDS IMPROVEMENT | Screenshot `highway_06_examination` |
| Supply selection: the bag is collected in one action; treatments are buttons; only dressings, blankets and gloves are counted. There is no inventory or "choose the right item" step. | PARTIALLY COMPLETED | `IncidentDirector.CollectKit`, `BuildExamPanel` |
| Treatment results are invisible on the body: no dressing pad, no foil blanket, no visible airway position (except the recovery roll) | NOT IMPLEMENTED | `HumanoidBody.cs:33` "No injuries are depicted"; no blanket or dressing mesh code in `Characters/` |
| Paramedic stretcher carry: the stretcher is parented to the lead paramedic; the partner walks behind with no carry pose. The ambulance does not drive away visibly (crew disabled). | NEEDS IMPROVEMENT | `Ambulance.cs:155-163, 206-215, 300-306` |
| Gamepad play | PARTIALLY COMPLETED: bindings and `GamepadSelector` exist, but there is no runtime test | `GameInput.cs`, `GamepadSelector.cs` |
| Voices: barks are text only (with subtitles); there are no voice lines | NEEDS IMPROVEMENT | `SpeechBark.cs`; only `Voice_Murmur.wav` |
| Designer workflow: **Build Project overwrites every casualty and scenario field from code**, so edits made in the Inspector are lost | NEEDS IMPROVEMENT (architecture) | `GoldenHourContent.Profile()` resets all fields on each run |
| CLAUDE.md "Requirements" still describe v1 (five patients, supply screen) | NEEDS IMPROVEMENT (docs) | `CLAUDE.md:24-42` |
| Engine-start shutdown seen once in the audit run | NEEDS IMPROVEMENT (watch) | Audit run 1 log |

## 4. Missing features versus the original vision (and V2_SPEC)

| # | Vision item | Status | Evidence / gap |
|---|---|---|---|
| 1 | Realistic road accident or disaster environment | PARTIALLY COMPLETED | Four distinct scenes with fog, night, rain, fire, smoke and sparks. They are flat-coloured primitives (boxes and cylinders) with no textures, decals, terrain or wrecked-vehicle detail, so the look is stylised, not realistic. |
| 2 | Multiple injured victims with different conditions | COMPLETED | 31 profiles: haemorrhage, airway, shock, worsening, expectant, children, motorcyclist, elderly, walkers |
| 3 | Interactive patient examination | COMPLETED (mechanics) / NEEDS IMPROVEMENT (presentation) | Timed exam actions; menu-based rather than hands-on |
| 4 | Colour-coded triage | COMPLETED | Physical tags plus a light stick; BLACK added per START |
| 5 | Correct prioritisation | COMPLETED | Handover-order scoring; debrief |
| 6 | First-aid supply selection and simulated treatment | PARTIALLY COMPLETED | Treatments are simulated; there is no supply inventory or selection, and no visible result on the patient |
| 7 | Time management | COMPLETED | Golden-hour clock, every action costs time, deterioration, ambulance waves |
| 8 | Patient-specific objectives | PARTIALLY COMPLETED | Profiles hold indicated treatments and the debrief evaluates them, but the HUD objectives are global (and two never tick). There is no per-patient checklist on the tablet. |
| 9 | Scoring on triage accuracy and treatment | COMPLETED | Exact-tally verified |
| 10 | Final performance report | COMPLETED | 5-tab debrief, stars, JSON |
| 11 | Medical kits and emergency equipment | PARTIALLY COMPLETED | Kit with an animated lid, gloves, dressings, blankets, tags, hazard posts, stretcher. Missing: tourniquet, splint or sling, cervical collar, AED, oxygen, triage-tag bundle, scene tape, cones the player can place. |
| 12 | 3D models, animations, environmental assets | PARTIALLY COMPLETED | Rigid-segment rigs with procedural animation (no skinning or clips); vehicles are blocky; three exported models are unused (§G) |
| 13 | Engaging main menu and gameplay UI | COMPLETED / NEEDS IMPROVEMENT | Live 3D menu and full HUD; layout overlaps, raw enums, and flat slab hands |
| 14 | Complete progression | COMPLETED | Menu → campaign → 4 scenarios → debrief → unlock; verified end to end |
| V2 | M4 "rigged humanoids with animations" | PARTIALLY COMPLETED | The documented fallback (procedural animation) was used; there are no skinned meshes |

## 5. Visual and environmental improvements

| Improvement | Status | Notes |
|---|---|---|
| Textures and materials | NOT IMPLEMENTED | Everything is flat colour. Add a small trim sheet: asphalt with cracks, kerb, brick or concrete, metal, fabric, tyre tread. |
| Decals | NOT IMPLEMENTED | Skid marks are boxes. Use URP decals for oil and fuel spill, scorch, glass scatter, road markings, dust. |
| Environment geometry | NEEDS IMPROVEMENT | Primitive box buildings and lollipop trees. Wanted: modelled buildings with windows and doors, guard rails, verges and ditches, street furniture, rubble piles, a broken hall interior. |
| Vehicle damage | NEEDS IMPROVEMENT | Dents, a broken windscreen, an open door, airbag, deformed bonnet, a scattered bumper. |
| Characters | NEEDS IMPROVEMENT | Rigid segments show gaps at the joints. Wanted: a skinned mesh, faces, clothing variety, hi-vis paramedic and firefighter uniforms, and non-graphic injury cues (torn sleeve, dust, pallor tint, sling). |
| Treatment visuals | NOT IMPLEMENTED | Dressing pad on the wound site, foil blanket draped, head-tilt pose, tag on the wrist. |
| First-person hands | NEEDS IMPROVEMENT | Box hands read as planks (audit screenshot). Wanted: modelled hands with gloves and finger poses per action. |
| Lighting | NEEDS IMPROVEMENT | Night has a single headlight spot. Add emergency-light bounce on wet road, light probes, a reflection probe, lit street lamps. |
| Effects | COMPLETED / NEEDS IMPROVEMENT | Fire, smoke, sparks and rain exist; add wet-road puddles, steam, dust motes in the hall, an extinguisher spray. |
| UI | NEEDS IMPROVEMENT | Fix the overlaps and enum labels; a diegetic tablet or clipboard model; icons for actions and supplies. |

## 6. Required Blender assets (new files only)

| Priority | Asset | Purpose |
|---|---|---|
| P1 | **Skinned humanoid** (armature: Hips → Spine → Chest → Neck → Head; shoulders, arms, hands, legs, feet) in adult, child and elderly variants with clothing sets | Replace the rigid rigs; enables real animation |
| P1 | First-person **gloved hands** (skinned, about 20 bones) | Viewmodel |
| P1 | **Treatment props:** dressing pad, roller bandage, foil blanket (draped and folded), triage tag on a string, gloves box | Visible treatment and supply selection |
| P1 | **First-aid bag interior** with separate items (tourniquet, triangular bandage or sling, scissors, gauze, foil blanket, tags, gloves) | Supply-selection screen |
| P2 | Damaged-vehicle set: crumpled car (two variants), shattered glass, bumper and debris kit, airbag | Road-accident realism |
| P2 | Ambulance with opening rear doors and a loading ramp; a two-person carry stretcher with handles | Handover scene |
| P2 | Road kit: modelled guard rail, verge, kerb, drain, sign set, cones, scene tape, hazard tripod | Environment |
| P2 | Building-collapse kit: broken slabs, rebar, timber beams, hall interior walls, dust piles | Building scenario |
| P3 | AED (prop only), cervical collar, oxygen bag | Emergency equipment (display and education) |
| P3 | Tablet or clipboard model, radio handset | Diegetic UI |

## 7. Required animations

Currently all animation is procedural joint rotation (`HumanoidBody.ApplyPose`). There are no animation clips and no Animator.

| Priority | Animation | Who |
|---|---|---|
| P1 | Idle breathing (normal, laboured, rapid), lying, sitting injured, restless shift, limp/unresponsive | Casualties |
| P1 | Walk (normal, limping, supported), stand up from sitting | Walking wounded |
| P1 | Two-person stretcher lift, carry and load; kneel and assess; walk with kit | Paramedics |
| P1 | First-person hands: open the bag, put on gloves, hold pressure, tilt head and lift chin, roll into recovery position, attach a tag, unfold a blanket, CPR compressions | Player |
| P2 | Panicked bystander loop, pointing, calmed or seated; crying child held by a parent | Crowd |
| P2 | Recovery-position transition (full-body blend driven by the roll minigame value) | Casualties |
| P2 | Ambulance doors opening, vehicle drive-off | Vehicles |
| P3 | Firefighter extinguishing, police directing traffic | Ambient responders |

**Implementation:** use a Humanoid Animator with blend trees, keeping the current state machine (`BodyState`) as the driver.

## 8. Unity architecture: assessment and improvements

**Can it support expansion?** Yes, for content and rules. The weak points are the authoring workflow and the size of the director.

| Aspect | Status | Notes |
|---|---|---|
| Pure-C# `GoldenHour.Core` with unit tests | COMPLETED (strength) | New conditions and rules can be added safely |
| ScriptableObject scenario and casualty data | PARTIALLY COMPLETED | Good data model, but `GoldenHourContent` overwrites assets on every Build Project. Make code-generated content "create if missing", or move the source of truth to the assets. |
| `IncidentDirector` (≈1,800 lines across 4 partial files; owns phases, interaction, panels, scoring, ambulances, tutorial) | NEEDS IMPROVEMENT | Split into services: `IncidentClock`, `InteractionController`, `ExamController`, `TreatmentController`, `AmbulanceDispatcher`, `TutorialController`, `ScoreService`. Communicate through events instead of the static `Instance`. |
| Everything built in code: no prefabs; environments are hundreds of `Box()`/`Decal()` calls | NEEDS IMPROVEMENT | Move environments, casualties, ambulance, kit and HUD to prefabs or scenes so artists can iterate. Keep the builder for wiring only. |
| UI built in code (`UiFactory`, `ModalWindow`) | NEEDS IMPROVEMENT | Works and is consistent, but layout bugs come from hard-coded coordinates. Consider prefab UI or layout groups. |
| Static singletons (`IncidentDirector`, `Viewmodel`, `StressSystem`, `PhotoMode`) and static UI classes (`DebriefScreen`, `FirstAidMinigame`, `Cutscene`) | NEEDS IMPROVEMENT | Acceptable for a single scene; harder to test and reuse |
| Input: one code-built action asset, rebinding persisted | COMPLETED | Fine |
| Test hooks inside runtime classes (`InstantActions`, `TestPress`, `TestRotate`) | NEEDS IMPROVEMENT | Move them behind an interface or a `#if` test define |
| Animation | NOT IMPLEMENTED (Animator) | Needed before adding skinned characters |

### G. Duplicate, unnecessary or broken scripts and assets

| Item | Finding | Recommendation |
|---|---|---|
| `Assets/Scripts/GameFlow/GameManaer.cs` (class `GameManager`; the filename has a typo) | In the scene, **inert**: its own 180 s timer never starts; a duplicate of the IncidentDirector clock | Remove the component, or keep it only as the empty host object |
| `Assets/Scripts/Scoring/ScoreManager.cs` | In the scene, **inert**; holds v1 scoring constants that duplicate `ScoreRules` | Remove |
| `Assets/Scripts/Player/PlayerController.cs`, `PlayerLook.cs`, `PlayerInteraction.cs` | Not in any scene (the builder strips them); duplicate `FirstPersonPlayer` | Remove |
| `Assets/Scripts/Patients/*` (PatientController, PatientData, PatientFactory, PatientInteractable, PatientProfileTester), `Assets/Scripts/Triage/STARTTriageEvaluator.cs`, `TriageCategory.cs`, `Assets/Scripts/Testing/GoldenHour*Tester.cs`, `Assets/UI/PatientUIController.cs` | Not referenced by any scene; a second START evaluator and a second triage enum (`TriageCategory` against `TriageTag`) | Remove. `Palette.ForCategory/Label` uses `TriageCategory` but is never called; remove those too. |
| `Assets/GoldenHour_Prototype.unity` | Not in Build Settings; contains only a light and a camera | Remove |
| `Blender/Exports/LowPolyMannequin.fbx`, `SupplyBundles.fbx`, `V2Props.fbx` | Never used at runtime (only checked by validation) | Use them (props in scenes, supplies in the bag UI) or remove them |
| `Assets/TextMesh Pro/Examples & Extras/` (5.6 MB, 30+ scenes, 33 scripts) | Unused samples | Remove |
| Packages `com.unity.visualscripting`, `com.unity.collab-proxy`, `com.unity.timeline` (no usage found) | Template defaults | Optional removal (smaller build, faster compiles) |
| `CLAUDE_CODE_HANDOFF.md`, `PROJECT_ANALYSIS.md` | v1-era documents | Archive under `Docs/history/` |
| Broken scripts | **None found.** 0 compile warnings; 0 runtime errors in the 257-check run. | — |

### H. Verdict: improve or redesign?

**Targeted improvement, not a redesign.** The core loop, simulation, scoring, progression and QA are complete and verified. The gap to the vision is mostly **presentation and authoring**:

- realistic assets and treatment visuals;
- skinned characters with animation;
- hands-on examination and supply selection;
- three UI bugs;
- moving code-built content to prefabs and assets.

One area deserves a **partial redesign**: the examination and treatment interaction. Move it from a full-screen menu to an in-world, diegetic flow.

## 9. Recommended development order

1. **Fix the defects (small, high value).**
   - Objectives that never tick.
   - Hazard total.
   - Toast overlap and toasts drawn above modals.
   - Tip and radio overlap.
   - Readable treatment names.
   - Update CLAUDE.md requirements.
   - Add autotest checks for each fix.
2. **Clean up.** Remove the inert legacy scripts and components, the duplicate START evaluator and enum, the prototype scene, the TMP examples and unused FBX models. Make content generation non-destructive ("create if missing").
3. **Architecture.**
   - Split `IncidentDirector` into services.
   - Introduce prefabs for casualty, ambulance, kit, hazard and HUD.
   - Move test hooks behind an interface.
4. **Supply selection and patient-specific objectives.**
   - A bag inventory UI with real items.
   - "Choose the right item" (with wrong-item feedback).
   - A per-patient care checklist on the tablet.
   - Visible treatment results.
5. **Diegetic examination.** A compact radial or side panel that keeps the casualty visible; camera focus on the body part; hand animations for each check.
6. **Blender P1 assets.** A skinned humanoid family, gloved hands, treatment props, bag items.
7. **Animation.** A Humanoid Animator with P1 clips (casualty states, walking wounded, paramedic two-person carry, first-person hand actions).
8. **Environment realism.** A trim-sheet texture set, URP decals, vehicle damage kit, road and building kits, lighting probes.
9. **Audio.** Recorded or TTS voice barks (with subtitles kept), a better siren with Doppler, layered crowd.
10. **QA.**
    - Extend the autotest (gamepad path, new UI).
    - Re-run the Iris Xe benchmark after the art upgrade, since the budget will shrink.
    - Medical review by a qualified trainer.
    - Playtest per PLAYTEST_PLAN.

## 10. Final recommended game design

**Golden Hour** stays a first-person, scenario-based serious game teaching **START triage and simple first aid under time pressure**. It keeps the educational disclaimer, the not-medically-reviewed statement, and no gore.

- **Loop (unchanged, proven):** briefing → scene safety (hazards, gloves) → 112 dispatch → walking-wounded shout → examine → tag → treat → ambulance handover by priority → debrief → replay or unlock.
- **Examination:** in-world and hands-on.
  - The player kneels, the camera frames the casualty, and a compact action ring offers the checks: walk, breathing, airway, rate, refill or radial pulse, commands, bleeding.
  - Each check plays a short hand animation and writes to a diegetic clipboard.
  - The clock never stops.
- **Treatment:** open the bag → pick the item (dressing, bandage or sling, foil blanket, tags; tourniquet and collar as "beyond this course" items with an explanation) → perform a short interaction (hold, roll, wrap). The result is visible on the casualty. Wrong item: feedback, no reward, time lost.
- **Per-patient objectives:** each casualty has a care checklist (assessed → tagged → indicated aid → handed over), shown on the tablet and in the debrief.
- **World:** textured, decal-rich stylised-realistic scenes: highway pile-up, night bus rollover in rain, collapsed community hall. Damaged vehicles, emergency lighting, ambient responders.
- **Characters:** skinned humanoids (adults, children, elderly, motorcyclist, paramedics, firefighters, police) driven by an Animator. Breathing and pain states are readable without gore.
- **Feedback:** keep the 5-tab debrief, stars, telemetry, adaptive difficulty. Add per-patient checklist results.
- **Technical:** pure-C# core plus unit tests (keep); a director split into services; prefab and asset-driven content that the builder no longer overwrites; autotest and benchmark kept as release gates (60 fps at 1080p Medium on integrated graphics).
