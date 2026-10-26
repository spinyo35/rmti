---
mode: agent
description: "WS-A — RTMI backend spine: solution, models, stores, simulator, SignalR hub, controllers, tests (single-shot, no questions)"
---

# WS-A — Backend foundation (build completely in this one session)

## 0. Operating rules (follow exactly — these exist to avoid back-and-forth)

1. **Never ask questions.** Everything you need is in this document (it is self-contained; the spec files are optional context). If something is ambiguous, pick the option this document implies and continue.
2. **Do not wait for confirmation.** Start implementing immediately. Do not "show a plan first".
3. **Implement in the order of sections 1→8.** Do not stop between sections to report progress.
4. **Do not modify** `FINAL_PLAN.md`, `copilot-prompts/*`, `RTMI_SPECS.md`, or anything under `starter-pack/`. The old prompts `01–10` are outdated — do not read or follow them.
5. **Stay in scope:** create only the files listed in section 2. No agents, no Foundry/Azure SDK calls, no React, no database, no auth, no Digest, no chat. The Foundry config keys exist for later — write them as placeholders, never call the model.
6. **After sections 1–8, run section 9 verification yourself.** If the build or tests fail, fix them and re-run until green. Do not finish while anything fails.
7. **Finish with a single summary:** files created (tree), verification results (exact command outputs' pass/fail), and a bullet list of any deviations from this document.

## 1. Environment

- OS: Windows, shell: cmd.exe, working directory = repo root (current workspace).
- .NET 8 SDK and Git available. Do not install anything else.
- Solution at repo root: `Rtmi.sln`. Projects: `src/Rtmi.Api`, `src/Rtmi.Api.Tests`.
- Local API URL: `http://localhost:5080`. Swagger: `http://localhost:5080/swagger`.

Scaffold with the CLI (if a flag is unsupported, create the project manually with the same shape and continue):
```bat
dotnet new sln -n Rtmi
dotnet new webapi -n Rtmi.Api -o src/Rtmi.Api --use-controllers
dotnet new xunit -n Rtmi.Api.Tests -o src/Rtmi.Api.Tests
dotnet sln add src/Rtmi.Api src/Rtmi.Api.Tests
dotnet add src/Rtmi.Api.Tests reference src/Rtmi.Api
dotnet add src/Rtmi.Api package Swashbuckle.AspNetCore
```
Delete template boilerplate (`WeatherForecast*`, `Program` minimal-API sample) so only our code remains.
Add `.gitignore` at root (standard VisualStudio + Node: `bin/`, `obj/`, `node_modules/`, `.vs/`, `*.user`, `appsettings.*.local.json`).

## 2. File tree to create (only these)

```
Rtmi.sln
.gitignore
src/Rtmi.Api/
  Program.cs
  appsettings.json
  appsettings.Development.json
  Properties/launchSettings.json
  Models/Enums.cs
  Models/MatchEvent.cs
  Models/MatchState.cs
  Models/Insight.cs
  Models/AnalystDeskResult.cs
  Models/RefereeVerdict.cs
  Models/ApiContracts.cs            (StartRequest none; EventsQuery/HealthReport/ApiResult)
  Services/EventStore.cs
  Services/EventBus.cs
  Services/StatsEngine.cs
  Services/MomentumCalculator.cs
  Services/InsightStore.cs
  IMatchControl.cs                  (interface, root or Services/)
  Simulation/MatchSimulator.cs
  Simulation/ScenarioScript.json    (CopyToOutputDirectory: PreserveNewest)
  Hubs/MatchHub.cs
  Hubs/IMatchBroadcaster.cs
  Controllers/MatchController.cs
  Controllers/InsightsController.cs
  Controllers/HealthController.cs
src/Rtmi.Api.Tests/
  StatsEngineTests.cs
  EventStoreTests.cs
  MomentumCalculatorTests.cs
  MatchSimulatorScriptTests.cs
```

Namespace root: `Rtmi.Api`. JSON: camelCase + `JsonStringEnumConverter` everywhere (section 7).

## 3. Models (exact contracts — serialize to the JSON shown)

**Enums.cs**
```csharp
public enum Team { HOME, AWAY }
public enum EventType { KickOff, Pass, Shot, Goal, Foul, YellowCard, RedCard, Corner, Save, Substitution, Offside, HalfTime, FullTime, MomentumShift }
public enum Persona { Casual, StatsNerd, Kid, Tactician }
public enum InsightKind { Narration, Analysis, Verdict, Answer }
public enum MatchStatus { NotStarted, Live, Paused, HalfTime, FullTime }
public enum RefereeMode { Referee, Analyst }
public enum RefereeDecision { GoalConfirmed, Offside, Penalty, Foul, NoAction }
```

**MatchEvent** — class with: `string Id, string MatchId, int Minute, int Second, EventType Type, Team Team, string Player, int X, int Y, string? Outcome, double? Xg, bool IsKey, List<string>? RelatedEventIds`.
Serializes to:
```json
{ "id": "evt_0042", "matchId": "m1", "minute": 37, "second": 12, "type": "Shot",
  "team": "HOME", "player": "J. Moyo", "x": 82, "y": 40, "outcome": "Saved",
  "xg": 0.21, "isKey": true }
```
(`relatedEventIds` omitted when null — `DefaultIgnoreCondition = WhenWritingNull` for it: use `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` on that property only.)

**MatchState** — `string MatchId, int Minute, MatchStatus Status, TeamState Home, TeamState Away, Momentum Momentum`.
**TeamState** — `string Name, int Score, int Shots, double Xg, int Possession, int Fouls`.
**Momentum** — `double Home, Away` (two doubles summing ≈ 1.0; both 0.5 before any events).

**Insight** — `string Id, InsightKind Kind, Persona Persona, string Source, string Text, string Why, List<string> EvidenceEventIds, double Confidence, DateTime CreatedAt`.

**AnalystDeskResult** — `string EventId, Persona Persona, List<DeskTake> Takes, Insight Verdict`.
**DeskTake** — `string Role ("Tactics"|"Stats"|"Fan"), string Text, string Why, List<string> EvidenceEventIds, double Confidence`.

**RefereeVerdict** — `string Id, RefereeMode Mode, RefereeDecision Decision, string Text, string Why, List<int> EvidenceFrameIndexes, List<string> EvidenceEventIds, double Confidence, string PersonaText, DateTime CreatedAt`.

**ApiContracts:** `EventsQuery(string? Since, string? Type, int? FromMinute, int? ToMinute, int Take = 500)`; `HealthReport(string Status, string Api, string Simulator, string Foundry)` — Foundry returns `"NotConfigured"` in this workstream; `ApiResult<T>(bool Ok, T? Data, string? Error)`.

## 4. Services (thread-safe; singletons registered in section 7)

**EventStore** — lock-protected `List<MatchEvent>`: `Add(MatchEvent)`, `GetAll()`, `GetSince(string? id)` (all after that id, empty list if unknown/none), `Query(EventType? type, int? fromMinute, int? toMinute, int take)`, `Exists(string id)`, `GetRecent(int n)`, `Count`, `Clear()`. Event IDs are assigned here if empty: format `evt_0001` zero-padded 4, monotonically increasing (keep counter; Reset resets counter).

**EventBus** — wraps `System.Threading.Channels.Channel<MatchEvent>` (unbounded, single reader): `void Publish(MatchEvent e)`, `ChannelReader<MatchEvent> Reader`, `void Reset()` (complete old channel and create a new one only when no readers depend on it — simplest: create in ctor, never reset; orchestrator in a later workstream tolerates restart by draining). Register as singleton.

**StatsEngine** — owns `MatchState State` (thread-safe via lock): `void Apply(MatchEvent e)`:
- `Goal`: score++ for scoring team; also counts as Shot (shots++, add xG).
- `Shot`: shots++, `Xg += e.Xg ?? 0`.
- `Foul`/`YellowCard`/`RedCard`: fouls++ for committing team.
- `Pass`: possession = running pass counts → `Home.Possession = round(100.0 * homePasses / totalPasses)` (0 if none); same for Away.
- `Minute`: `State.Minute = max(current, e.Minute)`; `HalfTime` → `Status = HalfTime`; `FullTime` → `Status = FullTime`; `KickOff` → `Status = Live` (first time).
- `Snapshot()` returns a **deep copy** of MatchState (new TeamState/Momentum instances) so callers never mutate shared state.
- `void Reset()` → fresh state with team names from a const pair (Harare Lions / Bulawayo Eagles), minute 0, NotStarted, possession 50/50 after kickoff only — initial `Possession` 0/0 until first Pass, then normalized.
Expose `MatchState Snapshot()`; mutate internally.

**MomentumCalculator** — `MomentumOutcome Apply(MatchEvent e, MatchState state)` applied **after** StatsEngine:
- Keep per-team weighted sums of events within the rolling **5 match-minutes** window (drop events older than `e.Minute - 5`): weights `Goal 3, Shot 1, Corner 0.5, Save 0.5`, others 0.
- `state.Momentum.Home = homeWeight / max(homeWeight + awayWeight, 0.0001)` (start 0.5/0.5).
- Track `previousSwing = lastEmittedHomeMomentum` (init 0.5). If `abs(newHome - lastEmittedHomeMomentum) > 0.25` → return the shift: caller (simulator) creates a derived event (section 5) and you set `lastEmittedHomeMomentum = newHome`. Returns `bool EmittedShift` (or null) — keep the type simple: `bool Apply(MatchEvent e, MatchState state)` returning `true` when a shift threshold crossed.
- `void Reset()`.

**InsightStore** — lock-protected list: `Add(Insight)`, `GetAll()`, `GetByPersona(Persona p)`, `Clear()`. No id assignment here (orchestrator assigns later).

**IMatchControl** — `string Status { get; }`, `void Start(int speed)`, `void Pause()`, `void Reset()`, `void Demo()`.

## 5. Simulation

**ScenarioScript.json** — place EXACTLY this file (set `CopyToOutputDirectory: PreserveNewest`):
```json
{
  "matchId": "m1",
  "home": "Harare Lions",
  "away": "Bulawayo Eagles",
  "homeRoster": ["J. Moyo", "T. Zvobgo", "K. Sibanda", "D. Chikafu", "P. Mangwiro"],
  "awayRoster": ["T. Ncube", "S. Dube", "M. Khumalo", "L. Ndlovu", "R. Moyo"],
  "fillerSeed": 42,
  "fillerEventsPerMinute": 2,
  "keyBeats": [
    { "minute": 12, "second": 30, "type": "Shot",   "team": "AWAY", "player": "T. Ncube", "x": 78, "y": 44, "outcome": "Saved", "xg": 0.08, "isKey": true },
    { "minute": 27, "second": 5,  "type": "Corner", "team": "HOME", "player": "K. Sibanda", "x": 95, "y": 12, "isKey": false },
    { "minute": 33, "second": 44, "type": "Goal",   "team": "HOME", "player": "J. Moyo", "x": 88, "y": 38, "outcome": "Scored", "xg": 0.42, "isKey": true },
    { "minute": 40, "second": 12, "type": "Foul",   "team": "HOME", "player": "D. Chikafu", "x": 44, "y": 60, "outcome": "Yellow card", "isKey": true },
    { "minute": 58, "second": 3,  "type": "RedCard","team": "AWAY", "player": "S. Dube", "x": 55, "y": 40, "outcome": "Sent off", "isKey": true },
    { "minute": 70, "second": 21, "type": "Shot",   "team": "HOME", "player": "T. Zvobgo", "x": 84, "y": 33, "outcome": "Saved", "xg": 0.14, "isKey": true },
    { "minute": 88, "second": 57, "type": "Goal",   "team": "AWAY", "player": "T. Ncube", "x": 81, "y": 42, "outcome": "Scored", "xg": 0.31, "isKey": true }
  ]
}
```
Note: type `Foul` at 40' with `outcome: "Yellow card"` — additionally emit a **separate** `YellowCard` event at the same minute/second +1s with the same player (do it in the simulator when a Foul beat has `"outcome": "Yellow card"`), `isKey: true`.

**MatchSimulator : BackgroundService, IMatchControl** (singleton, registered as both):
- **Schedule build** (`BuildSchedule(bool demoOnly)`): produce ordered `List<ScheduledEvent>` of `(int DelayMs, MatchEvent Evt)`:
  1. Auto beats: `KickOff` m0 s0 (HOME), `HalfTime` m45 s0, `FullTime` m90 s0.
  2. Key beats from the script (+ the derived YellowCard above).
  3. Filler: for each match minute 1..89 not covered by an auto/beat minute, generate `fillerEventsPerMinute` events using `Random(fillerSeed)` in **Demo mode**, else `Random(Environment.TickCount)`; minutes get `second = rng.Next(0, 60)`. Filler rules: type distribution Pass 65%, Shot 10% (outcome never `Scored` — choose from Saved/Blocked/Off target; `isKey` only when outcome == "Saved" → actually set `isKey: false` for all filler Shots except set `isKey=true` when `outcome=="Saved"`, keep simple: filler Shot → outcome "Saved", `IsKey=true`, `Xg = rng 0.03–0.12`), Foul 8% (`IsKey=false`), Corner 5%, Offside 4%, Substitution 4% (auto at minutes 46 and 63: two scripted substitutions instead — cleaner: add substitution auto-beats at 46' HOME and 63' AWAY with roster players, remove Substitution from filler). Team chosen 52% HOME / 48% AWAY; player from that team's roster (rng); x,y random in 5..95 (for Pass keep x arbitrary, y arbitrary).
  4. Sort all by `(minute, second)` ascending. Compute `DelayMs = max(50, 30000 / speed)` for every event (speed captured at Start; default from config `Simulation:DefaultSpeed` = 30 → 1 s/event; ~170 events ≈ 3 minutes).
- **Run loop** (`ExecuteAsync` with an internal `CancellationTokenSource`): while `Start` called: iterate schedule; between events `await Task.Delay(DelayMs, ct)`; honor `Pause` by `while (_paused && !ct.IsCancellationRequested) await Task.Delay(100, ct)`.
- **Per event:** `eventStore.Add` → `statsEngine.Apply` → if `momentumCalculator.Apply` returns true, ALSO create derived event `{ Type: MomentumShift, Id: next id, Minute/Second = current event's, Team = team with higher momentum now, IsKey = true, RelatedEventIds = ids of the up-to-3 last non-Pass events in window, Outcome: "Momentum shift", Player: "" }` → add to store, publish it and use it for the shift. Then `broadcaster.EventReceived(evt)` + `broadcaster.StateUpdated(state.Snapshot())`, then `eventBus.Publish(evt)` (publish MomentumShift too). After publishing `FullTime`: set `Status=FullTime`, stop loop.
- **Start(speed):** if running → ignore; build schedule, `Reset()` all stores first ONLY when status NotStarted (Start after Reset continues clean); set `IsRunning`. **Pause:** toggle `_paused`; status `Paused`/`Live` + broadcast StateUpdated. **Reset:** cancel loop, clear EventStore/InsightStore/StatsEngine/MomentumCalculator, status NotStarted, broadcast StateUpdated. **Demo:** `Reset()` then `Start(speed: config default)` with `demoOnly=true` schedule.
- `Status` string property: `"NotStarted" | "Running" | "Paused" | "Finished"`.

## 6. Hub, broadcaster, controllers

**MatchHub** (`/hubs/match`): client method `Task SetPersona(string persona)` → validate against `Enum.TryParse<Persona>`; move caller into group `persona:{name}` (also leave other `persona:*` groups via `Groups.RemoveFromGroupAsync`). Server methods the client listens for (declared as strongly-typed or documented names): `EventReceived(MatchEvent)`, `StateUpdated(MatchState)`, `InsightCreated(Insight)`, `DeskReady(AnalystDeskResult)`, `RefereeReady(RefereeVerdict)`.

**IMatchBroadcaster / MatchBroadcaster** (singleton, wraps `IHubContext<MatchHub>`): methods `EventReceived(MatchEvent e)` — send to group `all` PLUS every persona group (`Clients.Groups(...)` for the 4 groups; simplest: `Clients.All`), `StateUpdated(MatchState s)` → `Clients.All`, and stubs `InsightCreated(Insight i)` (send to `persona:{i.Persona}` only), `DeskReady(AnalystDeskResult)`, `RefereeReady(RefereeVerdict)` → `Clients.All`. This interface is the only SignalR dependency the simulator has (inject into MatchSimulator).

**Controllers:**
| Route | Behavior |
|---|---|
| `POST /api/match/start?speed=30` | `IMatchControl.Start(speed)`; clamp speed 1..120; return `ApiResult<string>(true, control.Status)` |
| `POST /api/match/pause` | Pause() |
| `POST /api/match/reset` | Reset() |
| `POST /api/match/demo` | Demo() |
| `GET /api/match/state` | `statsEngine.Snapshot()` |
| `GET /api/match/events?since=&type=&fromMinute=&toMinute=&take=` | EventStore.Query; `type` binds to EventType (ignore invalid → null) |
| `GET /api/insights?persona=Casual` | InsightStore.GetByPersona (no persona → all) |
| `GET /api/health` | HealthReport("Healthy", "Healthy", control.Status, "NotConfigured") |

No AnalystDesk/Referee controllers in this workstream (added in WS-B).

## 7. Program.cs + config

- `builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5080");`
- `AddControllers().AddJsonOptions(o => { o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping; o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });`
- Swagger: `AddEndpointsApiExplorer(); AddSwaggerGen();` (no auth).
- CORS from `Cors:Origins` array, `AllowAnyHeader/AllowAnyMethod/AllowCredentials()` (credentials required by SignalR). Applied before `MapControllers()`/`MapHub`.
- `AddSignalR();` (default JSON hub protocol already camelCase + respects converters — set hub protocol options only if needed).
- DI: singletons `EventStore, EventBus, StatsEngine, MomentumCalculator, InsightStore, IMatchBroadcaster→MatchBroadcaster, IMatchControl→MatchSimulator`; hosted service `MatchSimulator`.
- `MapControllers(); MapHub<MatchHub>("/hubs/match");` Swagger in **all** environments (demo convenience).
- appsettings.json — EXACTLY (no keys):
```json
{
  "Foundry": { "Endpoint": "", "Deployment": "gpt-4.1-mini", "UseFallbackOnly": false, "TimeoutSeconds": 8, "ApiKey": "" },
  "Agents": { "Narrator": "rtmi-narrator", "Tactics": "rtmi-tactics", "Stats": "rtmi-stats", "Fan": "rtmi-fan", "Verdict": "rtmi-verdict", "VideoReferee": "rtmi-video-referee" },
  "Simulation": { "DefaultSpeed": 30, "Scenario": "Simulation/ScenarioScript.json" },
  "Cors": { "Origins": [ "http://localhost:5173" ] },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } }
}
```
appsettings.Development.json: `{ "Foundry": { "Endpoint": "", "UseFallbackOnly": true } }` (fallback-first while building UI — saves credits).
launchSettings: http profile on port 5080.

## 8. Tests (xUnit — must compile and pass)

1. **StatsEngineTests:** Goal increments score; Shot increments shots and adds `Xg`; Foul increments fouls; Pass updates possession to a plausible 0–100 pair; `Snapshot()` returns independent copy (mutating snapshot doesn't change engine state).
2. **EventStoreTests:** `GetSince` returns only later ids; `Query` filters by type and minute range; `Exists` true/false; ids auto-assigned `evt_0001`-style; `GetRecent(n)` ordering.
3. **MomentumCalculatorTests:** scripted sequence (3 Shots + 1 Goal for HOME within 5 min vs no AWAY events) → `Apply` returns `true` at some point and `state.Momentum.Home > state.Momentum.Away`; Reset restores 0.5/0.5.
4. **MatchSimulatorScriptTests:** load `Simulation/ScenarioScript.json` (via `File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Simulation", "ScenarioScript.json"))` — file must be copied to test output too; add the copy item to the test project or read from the source path relative to the test assembly's directory: simplest — add `<None Include="..\Rtmi.Api\Simulation\ScenarioScript.json" Link="Simulation\ScenarioScript.json" CopyToOutputDirectory="PreserveNewest" />` to the test csproj): assert it contains a Goal with `IsKey`, a RedCard, equaliser minute ≥ 80, and no key beat is a `MomentumShift`.

## 9. Verification (run these yourself; fix until all green)

```bat
dotnet build Rtmi.sln --nologo        :: 0 errors
dotnet test  Rtmi.sln --nologo        :: all tests pass
```
Then an HTTP smoke test (start the API in the background, wait ~5 s, then):
```bat
curl.exe -s http://localhost:5080/api/health
curl.exe -s -X POST "http://localhost:5080/api/match/start?speed=120"
curl.exe -s "http://localhost:5080/api/match/state"
:: wait ~6 s, then:
curl.exe -s "http://localhost:5080/api/match/events?take=5"     :: non-empty array of events
curl.exe -s "http://localhost:5080/api/insights"                 :: [] is correct in WS-A
curl.exe -s http://localhost:5080/swagger/index.html             :: swagger UI HTML
curl.exe -s -X POST http://localhost:5080/api/match/pause
curl.exe -s -X POST http://localhost:5080/api/match/reset
```
Stop the background process afterwards. Requirements: health returns `healthy`, state shows `status: "Live"` with a minute advancing, events list is non-empty and includes scripted beats as minutes advance, swagger loads.

## 10. Done definition

- [ ] File tree of section 2 exists, nothing extra in scope-violating areas
- [ ] `dotnet build` + `dotnet test` green
- [ ] Smoke curl results all pass (paste them in your summary)
- [ ] No Foundry/model calls anywhere; no secrets; no changes to plan/spec files
- [ ] Final summary: tree, verification, deviations
