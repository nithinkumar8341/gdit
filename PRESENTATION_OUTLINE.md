# Golden Hour — Presentation Outline (5-minute demo)

**Setup before presenting:**
- Launch `Builds/GoldenHour/GoldenHour.exe` at 1920×1080.
- To show locks, start from a fresh profile. Otherwise use a profile where everything is unlocked, so you can jump straight to the Highway scenario.
- Volume on, Settings → Graphics: Medium.

| Time | On screen | Say |
|---|---|---|
| 0:00–0:30 | Main menu over the live 3D crash scene; point at the disclaimer line. | "Golden Hour is a first-person serious game about the first minutes of a mass-casualty incident. It teaches START triage: who to help first. It's educational only, and the content has not been medically reviewed." |
| 0:30–0:50 | Campaign screen: four scenarios, locks, stars. | "Four scenarios, from a guided training yard to a night bus rollover and a collapsed hall with children. One star unlocks the next." |
| 0:50–1:20 | Start **Highway Collision**. Let the opening fly-in play for a few seconds, then the briefing. | "Every scenario opens with a fly-in and a briefing. The golden-hour clock never stops while you work." |
| 1:20–2:00 | Mark the fuel leak and fire (E). Put on gloves (G). Radio the 112 report (R): fill in the 4 fields. | "Scene safety first. Hazards are scored, standing in a danger zone costs points, and the dispatch report is checked field by field." |
| 2:00–2:20 | Shout (Q). Walkers head to the green zone on the NavMesh. | "START step one: everyone who can walk goes to the green zone. They're GREEN in seconds." |
| 2:20–3:10 | Kneel at H02 and examine: breathing, rate, circulation. Tag RED. Hold Space for the pressure dressing; the hands hold the dressing. | "There are no labels. You examine with timed actions and decide from the findings alone. Refill over two seconds means RED. Every action costs simulated time, and people deteriorate on a seeded physiology model." |
| 3:10–3:40 | H03 (snoring): open the airway, tag RED, then the recovery-position roll minigame. | "Interactive first aid: roll them onto their side and stop in the band; go too far and they roll onto their front." |
| 3:40–4:10 | Ambulance arrives with sirens; the paramedics walk over. Handover panel: send RED first. | "Ambulances come in timed waves. Handing over the wrong priority is penalised." |
| 4:10–4:40 | Debrief: Summary (stars), Casualties (START step per person), Map heatmap, Timeline. | "The debrief is the teaching moment. For each casualty it shows the START step that applied, plus decision times, a movement heatmap and personalised feedback. Adaptive difficulty uses your last three runs." |
| 4:40–5:00 | Press F10 in a scene (photo mode), or show the Settings accessibility list. | "Accessibility: rebinding, gamepad, subtitles, colour-blind tag shapes, text size, reduce motion, and a toggle for stress effects. It's verified by 26 unit tests and a 250+ check automated playthrough of every scenario." |

**Backup if something fails:** use the screenshots in `Docs/TestEvidence/`.

**Likely questions:**
- *Is it accurate?* "START is modelled as published; simplifications such as children and CPR are listed in How to Play. It needs review by a qualified trainer."
- *Why no gore?* "It isn't needed for the learning goal, and it keeps the game usable in class."
