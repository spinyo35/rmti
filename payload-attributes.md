# RTMI — Payload Attributes Reference

Every key in every payload the system produces or consumes. Mental model: **`MatchEvent` says *what happened*, `MatchState` says *what it adds up to*, `Insight` says *what it means and why you should believe it*.**

---

## 1. `MatchEvent` — the atom of the whole system (produced by: Simulator)

| Key | Plain meaning | Example |
|---|---|---|
| `id` | Unique, permanent handle for this event. Every insight and referee check cites events by this ID so claims are traceable. | `"evt_0042"` |
| `matchId` | Which match this belongs to (only one ever — `m1` — but keeps the model multi-match-ready) | `"m1"` |
| `minute` / `second` | Match-clock time (0–90, not wall-clock). The sim compresses 90 min into ~3 real min. | `37` / `12` |
| `type` | What happened, from a fixed 14-value taxonomy (`Pass, Shot, Goal, Foul, YellowCard, RedCard, Corner, Save, Substitution, Offside, KickOff, HalfTime, FullTime, MomentumShift`). Determines which stats update and whether agents fire. | `"Shot"` |
| `team` | Who it happened to/for — `HOME` (Harare Lions) or `AWAY` (Bulawayo Eagles). | `"HOME"` |
| `player` | The player who did it (from a fixed synthetic roster — never invented elsewhere). | `"J. Moyo"` |
| `x` / `y` | **Pitch coordinates on a 0–100 grid.** `x=0` your goal line, `x=100` opponent's goal line; `y=0` right touchline, `y=100` left. So `x:82, y:40` = deep in the opponent's half, slightly right of centre — a dangerous attacking position. The AI uses it to judge *where* something happened. | `82` / `40` |
| `outcome` | Result of the action: `"Saved"`, `"Scored"`, `"Blocked"`, `"Yellow card"`, `"Sent off"`, etc. Adds the *what happened next* detail. | `"Saved"` |
| **`xg`** | **Expected Goals — the one real analytics stat in the payload.** A 0–1 probability that *this shot becomes a goal*, computed from shot difficulty (distance, angle, location). `0.21` = a 21% chance of scoring — a decent-but-not-guaranteed chance. Lets the Stats Nerd persona say "that was only a 0.08 xG chance — what a save!" instead of just "they shot". Shots only; `null` otherwise. | `0.21` |
| `isKey` | **The AI trigger flag.** `true` = "this is a moment worth commenting on" → the orchestrator fires agents. Goals, shots on target, cards, momentum shifts are key; routine passes are not. Controls model spend. | `true` |
| `relatedEventIds` | Only on derived `MomentumShift` events: which up-to-3 recent events *caused* the swing — so the "Why?" expander can point at the actual sequence. | `["evt_0038","evt_0040"]` |

## 2. `MatchState` — the scoreboard snapshot (produced by: StatsEngine)

| Key | Plain meaning |
|---|---|
| `matchId`, `minute`, `status` | Match identity, current clock, and phase: `NotStarted / Live / Paused / HalfTime / FullTime` |
| `home` / `away` → `name` | Team display names (Harare Lions / Bulawayo Eagles) |
| → `score` | Goals scored (from `Goal` events) |
| → `shots` | Total shot count |
| → `xg` | **Sum of xG so far** — "the scoreboard says 0–0, but our xG says 1.5 vs 0.4, Lions are dominant" |
| → `possession` | **Share of passes** as a whole % (54 = 54% of all passes). Both sides always total 100 (or 0/0 pre-first-pass). |
| → `fouls` | Fouls committed (incl. cards, which are fouls too) |
| `momentum.home/away` | **Who's on top right now** — a 0–1 rolling dominance score from the weighted last-5-minutes of actions (Goal counts 3, Shot 1, Corner/Save 0.5). `0.68/0.32` = Lions pressing hard. Drives the momentum chart and the `MomentumShift` key event when it swings > 0.25. |

## 3. `Insight` — every AI output ever rendered (produced by: agents or fallback)

| Key | Plain meaning |
|---|---|
| `id` | Unique insight handle (`ins_0019`) |
| `kind` | What type of AI output: `Narration` (commentary) · `Analysis` (a desk take) · `Verdict` (final call) · `Answer` (reserved) |
| `persona` | Who it was written for — `Casual / StatsNerd / Kid / Tactician`. Same event, different wording per persona. |
| `source` | **Which agent (or fallback) said it** — `NarratorAgent`, `TacticsAgent`, `Fallback`… auditability + tells judges "AI vs template". |
| `text` | The actual sentence(s) shown on screen, persona-styled (≤40 words for narrator) |
| `why` | **The receipt, in plain language** — e.g. "Three shots (evt_0038, evt_0040, evt_0042) inside the box since minute 33." Powers the "Why?" expander. |
| `evidenceEventIds` | **Machine-checkable receipts** — the exact event IDs backing the claim. Validator rejects the insight if empty or if any ID doesn't exist in the EventStore. Non-negotiable contract. |
| `confidence` | **0–1 how certain the AI is** (`0.82` = high). Shown as Low/Med/High badge; fallbacks are fixed at `0.5`. Teaches users not to blindly trust AI. |
| `createdAt` | UTC timestamp — ordering + debugging |

## 4. `AnalystDeskResult` — the multi-agent showcase (produced by: desk)

| Key | Plain meaning |
|---|---|
| `eventId` | The key moment being debated (e.g. the red card) |
| `persona` | Whose lens the whole desk took |
| `takes[]` → each `DeskTake` | Three **parallel** agents' independent opinions: `role` = `Tactics` (shape/pressing) · `Stats` (numbers only) · `Fan` (emotion/storyline); each with its own `text`, `why`, `evidenceEventIds`, `confidence` |
| `verdict` | A full `Insight` written **after** seeing all three takes — the orchestrator's synthesis ("Final Verdict"). This parallel-then-handoff chain is the Best Multi-Agent evidence. |

## 5. `RefereeVerdict` — the video feature (produced by: `rtmi-video-referee`)

| Key | Plain meaning |
|---|---|
| `id`, `createdAt` | Unique handle + time |
| `mode` | `Referee` = cold rule-check voice (VAR style) · `Analyst` = broadcast pundit voice. User picks per request. |
| `decision` | The actual call: `GoalConfirmed / Offside / Penalty / Foul / NoAction` |
| `text` | Short on-screen call announcement |
| `why` | Justification referencing what's visible ("defender's trailing foot is beyond the last defender at the moment of the pass…") |
| `evidenceFrameIndexes` | **Video receipts** — which of the ~8 sampled frames support the call (e.g. `[2,5]` = frame 3 and 6 of the strip). The UI highlights those frames like timeline evidence. |
| `evidenceEventIds` | Any live-match events the clip is linked to (`linkedEventId` request field), tying the video check back to the match timeline |
| `confidence` | 0–1 certainty of the decision (refs can be "unsure" — honest AI) |
| `personaText` | The same verdict reworded for the active persona (Kid: "The player was way offside!" · Stats Nerd: "0.3s ahead of the line…") |

**Referee request keys:** `persona` (whose voice), `mode` (Referee/Analyst), `frames[]` (base64 JPEGs sampled in the browser), `linkedEventId` (optional tie to a match event).

## 6. `ScenarioScript.json` — director's script (authored by: us)

| Key | Meaning |
|---|---|
| `matchId`, `home`, `away` | Match identity + team names |
| `homeRoster` / `awayRoster` | Fixed name pools — filler events only ever pick from these (no hallucinated players) |
| `fillerSeed` | Random seed `42` → **demo mode is byte-identical every run** (same dull pass, same goal minute — you can rehearse the demo) |
| `fillerEventsPerMinute` | How much connective tissue between big moments (~2/min keeps the timeline alive without noise) |
| `keyBeats[]` | The guaranteed narrative beats: `minute/second` (when), `type`+`outcome` (what), `team`+`player` (who), `x/y` (where), `xg` (how good the chance), `isKey` (fire the agents). Deliberate arc: away chance 12' → Lions goal 33' → yellow 40' → **red 58'** → away steal one back **88'**. |
