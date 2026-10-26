# RTMI — Final Plan (authoritative build reference)

**Hackathon:** Inside the Game: Developer Hackathon (Microsoft × Premier League) · Submissions close **27 Oct 2026** · Judging 27 Oct – 10 Nov 2026.
**Supersedes:** `RTMI_SPECS.md`, `RTMI_MASTER_PLAN.md` and **all `starter-pack/.github/prompts/01–10`** (those are the pre-trim, code-defined-agents scope — do not follow them).

---

## 1. Judging criteria (5 × 20%) and what we build against them

| Criterion | Our evidence |
|---|---|
| Technological Implementation | Microsoft Foundry **portal-defined agents** consumed by .NET 8 API; Copilot-assisted build; clean, documented repo |
| Agentic Design & Innovation | 6 specialized Foundry agents; code orchestrator: parallel desk → verdict handoff; validator + fallback = failure recovery |
| Real-World Impact | VAR/assistant-referee video verdicts + broadcast analyst deck — a real broadcast problem |
| UX & Presentation | "Broadcast control room" UI; live <2 min demo video; balanced frontend/backend |
| Category Adherence | Required pipeline **all five stages**: Ingest → Interpret → Explain → Render → Personalize |

**Target prizes:** Grand 1st/2nd, **Best Multi-Agent System**, **Best Use of Microsoft Foundry**.

## 2. Scope

**In (8 features):**
1. Match Simulator — synthetic events, scripted demo scenario, Start/Pause/Reset, speed control
2. **Video Referee** — stream a 5–15 s clip → multimodal Foundry agent verdict (Assistant Referee / Broadcast Analyst modes)
3. Stats + Momentum engine
4. Live Narrator — persona-styled commentary on key events
5. Analyst Desk — Tactics/Stats/Fan agents in parallel → Verdict agent
6. Explainability — every insight: `why`, evidence IDs, confidence
7. Persona switcher — Casual · Stats Nerd · Kid · Tactician (global, live)
8. React live dashboard

**Out (never build):** Ask-the-Match chat, Digest agent, TTS, multilingual, replay, databases, user accounts, mobile.

## 3. Architecture

```
┌──────────────────────── React SPA (web/rtmi-web) ─────────────────────────┐
│ ControlBar │ PersonaSwitcher │ Scoreboard │ Timeline │ Momentum │          │
│ NarratorFeed │ AnalystDesk │ RefereePanel │ Explainability (Why? overlay) │
└───────▲────────────────────────▲─────────────────────▲────────────────────┘
        │ SignalR /hubs/match    │ REST                │ REST
        │ (events, insights,     │ /api/referee/check  │ /api/health, /api/match/*
        │  desk, verdicts)       │ (frames base64)     │ /api/insights, /api/analyst-desk/*
┌───────┴────────────────────────┴─────────────────────┴────────────────────┐
│ .NET 8 API — src/Rtmi.Api (Azure App Service)                             │
│  Controllers  Match · Insights · AnalystDesk · Referee · Health           │
│  Hubs         MatchHub (per-persona SignalR groups)                       │
│  Simulation   MatchSimulator : BackgroundService + ScenarioScript.json     │
│  Services     EventStore · StatsEngine · MomentumCalculator                │
│               EventBus (Channel<MatchEvent>) · InsightStore                │
│               InsightOrchestrator · InsightValidator · FallbackTemplates   │
│  Agents       AgentInvoker (Foundry SDK adapter) · NarratorAgent ·         │
│               DeskAgents (tactics/stats/fan/verdict) · VideoRefereeAgent   │
└───────────────────────────────────┬───────────────────────────────────────┘
                                    │ DefaultAzureCredential / managed identity
                     ┌──────────────▼──────────────────────┐
                     │ Microsoft Foundry — project `rtmi`   │
                     │ Model: gpt-4.1-mini (vision-capable) │
                     │ Portal-defined agents:               │
                     │  rtmi-narrator · rtmi-tactics        │
                     │  rtmi-stats · rtmi-fan · rtmi-verdict│
                     │  rtmi-video-referee                  │
                     └──────────────────────────────────────┘
```

**Agent split:** agent prompts/instructions live **in the Foundry portal** (6 prompt agents, temp 0.4, model `gpt-4.1-mini`); orchestration, validation, fallbacks, stores, streaming live **in code**. Prompt sources are versioned in `/foundry-prompts` in the repo.

**Config (`appsettings.json`):**
```json
{
  "Foundry": { "Endpoint": "https://<resource>.services.ai.azure.com/api/projects/<project>",
               "UseFallbackOnly": false, "TimeoutSeconds": 8 },
  "Agents": { "Narrator": "rtmi-narrator", "Tactics": "rtmi-tactics", "Stats": "rtmi-stats",
              "Fan": "rtmi-fan", "Verdict": "rtmi-verdict", "VideoReferee": "rtmi-video-referee" },
  "Simulation": { "DefaultSpeed": 30, "Scenario": "Simulation/ScenarioScript.json" },
  "Cors": { "Origins": ["http://localhost:5173", "https://<swa>.azurestaticapps.net"] }
}
```
`UseFallbackOnly=true` short-circuits ALL model calls (templates only). Agent calls use the Azure AI Foundry project Responses endpoint and `DefaultAzureCredential`; keep endpoint overrides in user-secrets/environment and never commit credentials.

## 4. Synthetic data

**Match:** Harare Lions (HOME) vs Bulawayo Eagles (AWAY); 90 match-minutes ≈ 3 real minutes at speed 30.

**MatchEvent** (every downstream artifact cites these IDs):
```json
{ "id": "evt_0042", "matchId": "m1", "minute": 37, "second": 12, "type": "Shot",
  "team": "HOME", "player": "J. Moyo", "x": 82, "y": 40, "outcome": "Saved",
  "xg": 0.21, "isKey": true }
```
- Event types: `KickOff, Pass, Shot, Goal, Foul, YellowCard, RedCard, Corner, Save, Substitution, Offside, HalfTime, FullTime, MomentumShift`
- `MomentumShift` is **derived** (never scripted) and carries `relatedEventIds`.
- `isKey = true` for Goal, Shot on target, Cards, MomentumShift → these trigger agents.

**ScenarioScript.json:** ordered `keyBeats` (guarantees: goal 33', yellow 40', red 58', away equaliser 88') + deterministic filler between beats (seeded RNG; **fillers never score goals**; kickoff/half-time/full-time auto-generated by the simulator).

**Derived state:** score, shots, xG, possession (pass share %), fouls; momentum 0–1 per team from weighted 5-minute rolling window (Shot 1, Corner 0.5, Goal 3, Save 0.5); swing > 0.25 → emit `MomentumShift`.

## 5. Dataflows

### A. Live match loop
```
UI Start → POST /api/match/start?speed=30
→ MatchSimulator releases next MatchEvent
→ EventStore.Add + StatsEngine.Apply + MomentumCalculator.Apply (+ derived shift)
→ EventBus.Publish(evt); MatchHub: EventReceived + StateUpdated  → UI paints (<1 s, no AI)
→ if evt.isKey → InsightOrchestrator (Channel reader):
     3 s debounce → ×4 personas in parallel → AgentInvoker("rtmi-narrator", payload)
     → InsightValidator (text/why non-empty, evidence ∈ EventStore, confidence 0–1)
     ✗ timeout/invalid → FallbackTemplates (source:"Fallback", confidence 0.5)
     → InsightStore → MatchHub InsightCreated → persona group → NarratorFeed card
     → "Why?" → evidenceEventIds pulse gold in timeline
```

### B. Analyst Desk (multi-agent handoff)
```
Trigger: Goal | RedCard | MomentumShift (auto) or desk button
→ parallel: rtmi-tactics, rtmi-stats, rtmi-fan (payload {persona, event, recentEvents[10], state})
→ all validated takes → shared state → rtmi-verdict → AnalystDeskResult {takes[3], verdict}
→ MatchHub DeskReady → UI: 3 cards + verdict banner
Failure recovery: any agent fails → that take = fallback; verdict still runs.
```

### C. Video Referee
```
UI: pick/trim 5–15 s clip → canvas samples ~8 frames (base64 JPEG)
POST /api/referee/check { persona, mode: "Referee"|"Analyst", frames[], linkedEventId? }
→ + match context (last 5 events + state) → AgentInvoker("rtmi-video-referee", text + images)
→ RefereeVerdict { decision: GoalConfirmed|Offside|Penalty|Foul|NoAction, why,
                   evidenceFrameIndexes[], evidenceEventIds[], confidence, personaText }
→ validate → MatchHub RefereeReady → verdict card: frame strip + VAR stamp animation
```
Demo clips must be **royalty-free/synthetic** (no copyrighted footage in the submission video).

### D. Explainability contract (every rendered insight)
`{ id, kind, persona, source, text, why, evidenceEventIds[] | evidenceFrameIndexes[], confidence, createdAt }` — validator enforces; UI never renders an insight without receipts.

## 6. Design language (all phases)

- **Theme:** "broadcast control room" — near-black pitch `#0B0F0D`, floodlight lime `#C8FF4D`, team colors, amber cards, red for red-card/VAR.
- **Scorebug:** fixed TV-style strip `HAR 1 – 0 BUL` + pulsing live clock dot.
- **Cards:** dark glass, 14px radius, lime hairline; live elements glow.
- **Motion:** score flip on goal; animated momentum area chart; evidence events pulse gold; **VAR stamp animation** for referee decisions.
- **Persona chips:** 4 icon chips with accent tints that recolor feed highlights live.
- **Type:** Inter + mono numerals (score, xG, confidence). Skeletons while agents think; `template` badge on fallbacks.
- **Stack:** Vite + React + TS, Tailwind, lucide-react, recharts, framer-motion.
- **Responsive:** 3 columns → stacked; scorebug always visible.
- **Targets:** event on screen <1 s; agent insight <5 s.

## 7. Phases

### Phase 1 — MVP end-to-end (workstreams A–D)
| WS | Deliverable | Checkpoint |
|---|---|---|
| **A** | Solution, models, EventStore/StatsEngine/Momentum/InsightStore/EventBus, MatchSimulator + ScenarioScript, MatchHub, Match/Insights/Health controllers, Program.cs, Swagger, tests | `dotnet build` + `dotnet test` green; curl smoke: start → events stream → state updates |
| **B** | `/foundry-prompts` ×6, AgentInvoker (Foundry SDK), InsightValidator, FallbackTemplates, InsightOrchestrator, NarratorAgent, DeskAgents (parallel→verdict), Referee controller + VideoRefereeAgent, health `foundry` status | Portal agents created (manual ~30 min); narrator insights flow incl. `UseFallbackOnly`; desk renders; referee verdict from sample clip |
| **C** | React app per design language: scorebug, controls, persona chips, scoreboard, timeline, momentum chart, narrator feed + Why?, desk, referee panel, explainability overlay | `npm run dev`: full loop visible and styled |
| **D** | Resilience pass: fallback tags, latency check, unit tests for validator/invoker, README, `/foundry-prompts` sync | All spec acceptance criteria pass locally |

### Phase 2 — Seal
1. **Experience:** motion polish, empty/error states, mobile, demo mode (one-button identical run).
2. **Ops:** health dashboard, structured agent logs, README final.
3. **Deploy:** App Service B1 (WebSockets, Always On, managed identity, `ASPNETCORE_URLS=http://+:80` app setting) + Static Web Apps + GitHub Actions; CORS; budget alert.
4. **Submit:** <2 min demo video (pitch → live match → persona switch → why-receipts → desk verdict → VAR stamp → architecture), public repo, `Project-desc.md` copy + Video Referee line, URL checks.

## 8. Repo layout
```
Rtmi.sln
├── foundry-prompts/            # exact instructions pasted into Foundry portal
├── copilot-prompts/            # workstream prompt files (.prompt.md)
├── src/Rtmi.Api/               # Models Simulation Services Agents Hubs Controllers
├── src/Rtmi.Api.Tests/         # xUnit
└── web/rtmi-web/               # Vite + React + TS
```

## 9. Submission checklist
- [ ] Public GitHub repo · [ ] Working public URL · [ ] Demo video <2 min (public link)
- [ ] Tagline / keywords / description (`Project-desc.md`, updated with Video Referee)
- [ ] No secrets committed · [ ] Budget alert on · [ ] Azure deleted after 10 Nov
