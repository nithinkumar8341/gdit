# Golden Hour — Playtest Plan

> Educational simulation only. The content has **NOT been medically reviewed**; a qualified first-aid trainer should check it before it is used for teaching.

## Goals

1. **Learning:** do players apply the START order correctly after playing?
2. **Usability:** can first-time players complete the tutorial without help?
3. **Engagement:** does the clock create pressure without overwhelming players? Do they want to replay?
4. **Comfort and safety:** is any content distressing? Are the stress effects acceptable?

## Participants and setup

- 6–10 participants: a mix of course students and people with no first-aid training. Optionally, one qualified first-aid trainer to review content accuracy.
- A Windows laptop running `Builds/GoldenHour/GoldenHour.exe`, with headphones.
- Before the session, delete the profile folder or use a fresh Windows user, so the campaign starts locked.
- About 30 minutes per participant.

## Procedure

1. **Consent and briefing (2 min).** Explain that it is an educational game, not medical training, and that the participant may stop at any time.
2. **Pre-test (3 min).** The participant answers the knowledge questions below on paper.
3. **Tutorial (5–8 min).** Use think-aloud. The observer does not help unless the participant is stuck for more than 60 s, and notes where that happened.
4. **Highway Collision (8–10 min).** The participant plays normally, then reviews the debrief tabs.
5. **Optional replay or Night Bus Rollover (8 min).**
6. **Post-test (3 min).** The same knowledge questions again.
7. **In-game questionnaire (3 min).** Main menu → **Feedback**: eight 1–5 Likert items plus a comment, saved as JSON.
8. **Short interview (3 min).** Ask: "What was confusing? What would you change? Did anything feel uncomfortable?"

## Knowledge questions (pre and post)

1. What is the first thing START asks of everyone at the scene? *(Can you walk? → GREEN)*
2. Someone is not breathing. What do you do before deciding on a tag? *(Open the airway; if they then breathe → RED, if not → BLACK)*
3. Above which breathing rate is a casualty tagged RED? *(Over 30 per minute)*
4. Name two circulation checks used in START. *(Capillary refill over 2 s; absent radial pulse)*
5. A casualty breathes 20 per minute, has good circulation and follows commands but cannot walk. Which tag? *(YELLOW)*
6. Why is CPR usually deprioritised in a mass-casualty incident? *(Time is better spent on people who can still be saved)*
7. Two RED casualties and one YELLOW are waiting, and an ambulance has one space. Who goes first? *(A RED)*

## In-game questionnaire (saved by the game)

The menu **Feedback** screen saves `feedback_<timestamp>.json` to `%USERPROFILE%\AppData\LocalLow\Golden Hour Learning\Golden Hour\Feedback\`. Each file contains the last scenario, score and stars.

Rating scale: 1 = strongly disagree, 5 = strongly agree.

1. I understood the START triage steps after playing.
2. Examining by actions (breathing, circulation, commands) felt meaningful.
3. The golden-hour clock created real time pressure.
4. My decisions had clear consequences.
5. The debrief explained why my decisions were right or wrong.
6. I felt immersed in the scene (sound, lighting, characters).
7. The controls and interface were easy to use.
8. I would like to replay a scenario to improve.

There is also an optional free-text comment.

## Data collected

| Source | Data |
|---|---|
| Run JSON (`...\LocalLow\Golden Hour Learning\Golden Hour\Runs\run_<scenario>_<time>.json`) | Every decision with simulated and real time, the path, the score ledger, per-casualty outcomes and decision times, difficulty band and seed. |
| Feedback JSON | Likert answers and the comment. |
| Observer sheet | Time to finish the tutorial, help requests, errors, quotes. |
| Paper test | Pre and post knowledge scores. |

## Success criteria

- At least 80% finish the tutorial without help.
- The mean post-test score is at least 2 points higher than the pre-test score.
- First-tag accuracy in Highway is 70% or higher.
- Likert items 1, 5 and 7 have a mean of 4 or higher.
- No participant reports distressing content.

## Analysis

- Compare pre- and post-test scores, using a paired comparison for each participant.
- Aggregate run JSON: accuracy, under- and over-triage, decision times, preventable deaths.
- Group the comments by theme.
- Turn every issue into a backlog item with a severity of 1–3.
