GOLDEN HOUR v2 – SERIOUS GAME UPGRADE. Fully autonomous. Do not ask me for
permission, confirmation or to create/edit any file. Make all decisions yourself.

STEP 0 (first): save this ENTIRE message verbatim as V2_SPEC.md in the project
root, add a pointer to it in CLAUDE.md, read CLAUDE.md and DEV_LOG.md, and make a
git snapshot commit. Unity is closed.

CONTEXT: v1 is complete and passing 107 automated checks (menu, one highway
scenario, 5 patients, triage, kit, supplies, report). It is for my Game Design /
Immersive Technology course, which requires a very serious, full 3D game. You may
redesign or replace ANYTHING (rule engine, scoring, scenes, UI, art) if the result
is better. Keep git history, keep the game playable and the build working at every
milestone, and replace v1 parts only after the v2 version passes.

V1 WEAKNESSES TO FIX: triage is guessable by trial and error (free retriage, free
supply taps); patients never change over time; one short scenario; examination
data is simply handed to the player; no consequences.

DESIGN PILLARS: (1) every decision has consequences, (2) the golden hour is a real
clock, (3) strong immersion, (4) the player learns WHY through a debrief,
(5) replayability.

MILESTONES (do in order, commit after each, never skip M8–M10):

M1 SIMULATION CORE
- Patient physiology model: breathing rate, circulation (capillary refill / pulse),
  consciousness (can follow commands or not), bleeding level, mobility. Conditions
  deteriorate over simulated time unless treated; untreated critical patients can
  die at defined times, YELLOW can worsen to RED, aid stabilises. All data-driven
  (ScriptableObjects), seedable for tests.
- Implement the START triage algorithm properly with four categories:
  RED immediate, YELLOW delayed, GREEN minor, BLACK deceased/expectant (walking →
  GREEN; no breathing after airway opening → BLACK; breathing > 30/min → RED;
  poor circulation → RED; cannot follow commands → RED; else YELLOW). Mark it as a
  simplified educational model.
- Examination by ACTIONS: check breathing, check circulation, check responsiveness
  (give a command). Each takes in-game time, so the player gathers information
  instead of receiving it.
- Tag decisions: a physical triage tag is placed. Retriage is allowed only after
  re-examining, costs time, and is logged. The first decision is what is scored,
  with a smaller credit for a justified retriage after a real change in condition.

M2 SCENARIOS AND SYSTEMS
- ScenarioData ScriptableObjects. Scenarios: Tutorial (guided, 3 patients);
  Highway Collision (existing, upgraded); Night/Rain Bus Rollover (about 10
  casualties, fuel-leak and fire hazards); Collapsed Building Site (about 12
  casualties including children and a crowd of walking wounded).
- Scene safety phase: spot and mark hazards, then a dispatch report minigame
  (location, number of casualties, hazards, emergency number 112) that is scored.
- Limited supplies so choices matter. Ambulances arrive in timed waves with
  paramedic NPCs on NavMesh (AI Navigation is installed); the player must hand
  patients over in correct priority order, which is scored.
- Walking-wounded NPCs follow a "if you can walk, go to the green zone" command;
  panicking bystanders; radio messages from an incident commander on timers.

M3 INTERACTIVE FIRST AID
- Hold-to-apply pressure dressing with a progress bar, recovery position
  (rotate to the correct pose), blanket for warmth, airway opening, gloves first
  (skipping gloves is penalised), and a CPR rhythm minigame (100–120 bpm
  metronome) with an educational note that in mass-casualty triage CPR is
  deprioritised to save more lives. Safe, simple, educational actions only.
- Patient behaviour: chest breathing motion, restless or limp states, short text
  speech barks. Non-graphic injuries only (no gore).

M4 CHARACTERS AND ART (Blender background mode, new files only)
- Rigged low-poly humanoids with variations (adults, a child, a helmeted
  motorcyclist) and animations: idle breathing, lying, sitting injured, walking,
  stretcher carry. If armature export fails, use procedural animation in Unity and
  record this in DEV_LOG. Never claim an asset was generated without verifying it.
- Ambulance, fire truck, police car, stretcher, hazard props, fire and sparks
  particles, rain, fog, night lighting with headlight beams, flashing sirens,
  a cohesive stylised look. Keep it smooth on a student laptop.

M5 IMMERSION
- First-person viewmodel (gloved hands holding items), crouch/kneel to examine
  (camera lowers), subtle head sway, stress system (time pressure drives vignette
  pulse, heartbeat and breathing audio; toggle in accessibility), 3D spatial
  audio (approaching sirens, patient voices, rain, wind, radio static), dynamic
  layered music, diegetic UI (clipboard/tablet casualty list with vitals notes,
  radio), interaction outlines, an opening fly-through cutscene, an end cutscene,
  and a photo/trailer mode (F10 hides the HUD and enables a free camera).

M6 DEBRIEF AND ANALYTICS
- Log telemetry per run as JSON (decisions, timings, path, mistakes). Debrief
  screen: event timeline, per-patient "what happened and why" naming the START
  step that applied, survival outcomes, top-down movement heatmap, decision-time
  stats, 1 to 3 stars per scenario, personalised feedback.
- Saved player profile: campaign progress, unlocks, best scores. Campaign
  select screen.
- Adaptive difficulty from past performance (simple, documented model).

M7 UX AND ACCESSIBILITY
- Main menu with a live 3D background, campaign select, loading screens with
  tips, credits. Settings: graphics presets (Low/Medium/High), resolution,
  key rebinding (new Input System), subtitles, colourblind-safe tag shapes and
  patterns, text size, motion reduction, master/music/SFX volume, mouse
  sensitivity. Gamepad support. Pause menu.

M8 PERFORMANCE AND QA
- Target 60 fps at 1080p on integrated graphics at Medium: LODs or simple meshes,
  batching, limited real-time lights, pooled particles.
- Extend the automated playtest to cover EVERY scenario (full completion, exact
  scoring, deterioration and death logic, retriage rules, ambulance handover, zero
  exceptions). Add edit-mode tests for the START evaluator and scoring where the
  Test Framework is available. Compile with 0 errors and 0 warnings in project
  code, run Validate Project, update TESTING_REPORT.md and Docs/TestEvidence.

M9 COURSE DOCUMENTS
- GDD.md: vision, pillars, audience, core loop, mechanics, systems diagrams
  (mermaid), level design, art and audio direction, immersion techniques, UX and
  accessibility, learning objectives and assessment, ethics, scope and risks,
  architecture. PLAYTEST_PLAN.md with a questionnaire and an in-game feedback
  screen that saves answers to JSON. PRESENTATION_OUTLINE.md with a 5-minute demo
  script. ASSET_CREDITS.md. Update README.md and DEV_LOG.md.

M10 FINAL BUILD
- Windows build in Builds/GoldenHour, Release/GoldenHour_Windows.zip (without the
  BackUpThisFolder subfolder, kept out of git), final commit.

SAFETY AND ACCURACY: educational only, not medical training or advice. Show the
disclaimer in the menu, tutorial and every report. State that the content has NOT
been medically reviewed and should be checked by a qualified first-aid trainer.
Describe START accurately and mark simplifications. Do not invent citations; name
only well-known public guidelines and mark them "verify". No graphic gore, no
unsafe procedures.

RULES: prefer built-in packages; add a registry package only if clearly valuable
and record it in DEV_LOG. Loop: change, compile, read the log, fix, repeat. Never
claim anything works without verification evidence. Commit per milestone. Stop
ONLY for a hard blocker (Unity open, missing install, needed download) and name
the one action I must take. If usage runs low, finish the current milestone in a
stable state, commit, record the exact status and next step in DEV_LOG.md.

FINAL REPORT (once, at the end): what was built, how it was verified, what could
not be automated, and the manual checks I should do.
