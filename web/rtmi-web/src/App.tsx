import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Activity, ArrowDown, ArrowUpRight, BadgeCheck, BarChart3, ChevronDown, ChevronLeft, ChevronRight,
  CircleHelp, Clock3, Flame, Goal, LoaderCircle, Moon, Pause, Play, Radio, RefreshCw, Sun,
  RotateCcw, ShieldAlert, Sparkles, Target, Users, Video, Wifi, WifiOff,
  X,
} from "lucide-react";
import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { AnimatePresence, motion, MotionConfig, useReducedMotion } from "framer-motion";
import {
  Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";
import type {
  AnalystDeskResult, EventType, HealthReport, Insight, MatchEvent, MatchState,
  Persona, RefereeVerdict,
} from "./types";

const personas: { value: Persona; label: string; short: string; icon: string }[] = [
  { value: "Casual", label: "Casual", short: "CASUAL", icon: "◉" },
  { value: "StatsNerd", label: "Stats Nerd", short: "STATS", icon: "⌁" },
  { value: "Kid", label: "Kid", short: "KID", icon: "✳" },
  { value: "Tactician", label: "Tactician", short: "TACTICIAN", icon: "⌖" },
];

const syntheticScenarios = [
  {
    path: "away-attack",
    title: "Synthetic sample · away attack",
    description: "Blue away attackers move left toward the lime home goal. Home defenders track back and the gold-shirted goalkeeper blocks the final shot.",
  },
  {
    path: "home-counterattack",
    title: "Synthetic sample · home counterattack",
    description: "Lime home attackers break right toward the blue away goal. Away defenders retreat as their gold-shirted goalkeeper dives to block the shot.",
  },
  {
    path: "away-corner",
    title: "Synthetic sample · away corner",
    description: "Blue away players deliver a corner toward the lime home goal. Both teams converge in the box as the gold-shirted home goalkeeper claims the cross.",
  },
  {
    path: "away-offside-goal",
    title: "Synthetic sample · away offside goal",
    description: "A blue away attacker is beyond the second-last defender when the through-ball is played, then scores past the gold-shirted home goalkeeper.",
  },
] as const;

const initialState: MatchState = {
  matchId: "m1",
  minute: 0,
  status: "NotStarted",
  home: { name: "Harare Lions", score: 0, shots: 0, xg: 0, possession: 0, fouls: 0 },
  away: { name: "Bulawayo Eagles", score: 0, shots: 0, xg: 0, possession: 0, fouls: 0 },
  momentum: { home: 0.5, away: 0.5 },
};

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/+$/, "") ?? "";
type Theme = "dark" | "light" | "live";

const fmtTime = (minute: number, second = 0) =>
  `${String(minute).padStart(2, "0")}:${String(second).padStart(2, "0")}`;

const readableLabel = (value: string) => value.replace(/([a-z])([A-Z])/g, "$1 $2");
const readableType = (type: EventType) => readableLabel(type);
const iconFor = (type: EventType) => {
  if (type === "Goal") return <Goal size={14} />;
  if (type === "Shot") return <Target size={14} />;
  if (type === "RedCard" || type === "YellowCard") return <ShieldAlert size={14} />;
  if (type === "MomentumShift") return <Activity size={14} />;
  return <span className="event-dot" />;
};

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });
  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed (${response.status})`);
  }
  return response.json() as Promise<T>;
}

function App() {
  const [state, setState] = useState(initialState);
  const [events, setEvents] = useState<MatchEvent[]>([]);
  const [insights, setInsights] = useState<Insight[]>([]);
  const [persona, setPersona] = useState<Persona>("Casual");
  const [theme, setTheme] = useState<Theme>("dark");
  const [activeModal, setActiveModal] = useState<"desk" | "review" | null>(null);
  const [connection, setConnection] = useState<"connecting" | "connected" | "disconnected">("connecting");
  const [health, setHealth] = useState<HealthReport | null>(null);
  const [healthError, setHealthError] = useState("");
  const [healthUpdatedAt, setHealthUpdatedAt] = useState<Date | null>(null);
  const [healthRefreshing, setHealthRefreshing] = useState(false);
  const [scorePulse, setScorePulse] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState("");
  const [speed, setSpeed] = useState(30);
  const [desk, setDesk] = useState<AnalystDeskResult | null>(null);
  const [referee, setReferee] = useState<RefereeVerdict | null>(null);
  const [selectedEvidence, setSelectedEvidence] = useState<string[]>([]);
  const [eventFilter, setEventFilter] = useState<"all" | "key">("all");
  const [clipFrames, setClipFrames] = useState<string[]>([]);
  const [clipName, setClipName] = useState("");
  const [syntheticDescription, setSyntheticDescription] = useState("");
  const [frameIndex, setFrameIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [sampleLoading, setSampleLoading] = useState(false);
  const [refereeMode, setRefereeMode] = useState<"Referee" | "Analyst">("Referee");
  const hubRef = useRef<HubConnection | null>(null);
  const personaRef = useRef(persona);
  const videoInputRef = useRef<HTMLInputElement>(null);
  const playbackTimer = useRef<number | null>(null);
  const lastSampleScenario = useRef<string | null>(null);
  const chartRef = useRef<{ minute: number; home: number; away: number }[]>([]);
  const previousScore = useRef({ home: initialState.home.score, away: initialState.away.score });
  const prefersReducedMotion = useReducedMotion();

  useEffect(() => {
    if (!activeModal) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setActiveModal(null);
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [activeModal]);

  const refreshHealth = useCallback(async () => {
    setHealthRefreshing(true);
    try {
      setHealth(await api<HealthReport>("/api/health"));
      setHealthUpdatedAt(new Date());
      setHealthError("");
    } catch (reason) {
      setHealthError(reason instanceof Error ? reason.message : "Could not refresh service health.");
    } finally {
      setHealthRefreshing(false);
    }
  }, []);

  const addEvent = useCallback((incoming: MatchEvent) => {
    setEvents((previous) => previous.some((event) => event.id === incoming.id)
      ? previous
      : [...previous, incoming].sort((a, b) => a.minute * 60 + a.second - (b.minute * 60 + b.second)));
  }, []);

  const addInsight = useCallback((incoming: Insight) => {
    setInsights((previous) => previous.some((item) => item.id === incoming.id)
      ? previous
      : [incoming, ...previous].slice(0, 30));
  }, []);

  const loadSnapshot = useCallback(async () => {
    const [nextState, nextEvents, nextInsights, nextHealth] = await Promise.all([
      api<MatchState>("/api/match/state"),
      api<MatchEvent[]>("/api/match/events?take=5000"),
      api<Insight[]>("/api/insights"),
      api<HealthReport>("/api/health"),
    ]);
    setState(nextState);
    setEvents(nextEvents);
    setInsights(nextInsights.reverse());
    setHealth(nextHealth);
    setHealthUpdatedAt(new Date());
    setHealthError("");
    chartRef.current = [{ minute: nextState.minute, home: nextState.momentum.home, away: nextState.momentum.away }];
  }, []);

  useEffect(() => {
    const timer = window.setInterval(() => void refreshHealth(), 30_000);
    return () => window.clearInterval(timer);
  }, [refreshHealth]);

  useEffect(() => {
    const scored = state.home.score > previousScore.current.home
      || state.away.score > previousScore.current.away;
    previousScore.current = { home: state.home.score, away: state.away.score };
    if (!scored || prefersReducedMotion) return;

    setScorePulse(true);
    const timer = window.setTimeout(() => setScorePulse(false), 700);
    return () => window.clearTimeout(timer);
  }, [state.home.score, state.away.score, prefersReducedMotion]);

  useEffect(() => {
    let cancelled = false;
    const startHub = async () => {
      const hub = new HubConnectionBuilder()
        .withUrl(`${apiBaseUrl}/hubs/match`)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();
      hubRef.current = hub;
      hub.on("EventReceived", (event: MatchEvent) => addEvent(event));
      hub.on("StateUpdated", (next: MatchState) => {
        setState(next);
        const points = chartRef.current;
        const point = { minute: next.minute, home: next.momentum.home, away: next.momentum.away };
        if (!points.length || points.at(-1)?.minute !== point.minute ||
          points.at(-1)?.home !== point.home || points.at(-1)?.away !== point.away) {
          chartRef.current = [...points, point].slice(-50);
        }
      });
      hub.on("InsightCreated", (insight: Insight) => addInsight(insight));
      hub.on("DeskReady", (result: AnalystDeskResult) => setDesk(result));
      hub.on("RefereeReady", (result: RefereeVerdict) => setReferee(result));
      hub.onreconnecting(() => setConnection("connecting"));
      hub.onreconnected(async () => {
        setConnection("connected");
        try {
          await hub.invoke("SetPersona", personaRef.current);
        } catch (reason) {
          setError(reason instanceof Error ? reason.message : "Could not restore the selected broadcast persona.");
        }
      });
      hub.onclose(() => setConnection("disconnected"));
      try {
        await hub.start();
        if (cancelled) {
          await hub.stop();
          return;
        }
        await hub.invoke("SetPersona", personaRef.current);
        setConnection("connected");
        await loadSnapshot();
      } catch (reason) {
        if (cancelled) return;
        setConnection("disconnected");
        setError(reason instanceof Error ? reason.message : "Could not connect to the match service.");
      }
    };
    const startupTimer = window.setTimeout(() => void startHub(), 0);
    return () => {
      cancelled = true;
      window.clearTimeout(startupTimer);
      const hub = hubRef.current;
      hubRef.current = null;
      if (hub) void hub.stop();
    };
  }, [addEvent, addInsight, loadSnapshot]);

  useEffect(() => {
    if (!isPlaying || clipFrames.length < 2) return;
    playbackTimer.current = window.setInterval(() => {
      setFrameIndex((current) => (current + 1) % clipFrames.length);
    }, 500);
    return () => {
      if (playbackTimer.current !== null) window.clearInterval(playbackTimer.current);
      playbackTimer.current = null;
    };
  }, [isPlaying, clipFrames.length]);

  const chartData = chartRef.current;
  const filteredEvents = useMemo(
    () => [...events].reverse().filter((event) => eventFilter === "all" || event.isKey).slice(0, 18),
    [events, eventFilter],
  );
  const visibleInsights = useMemo(() => insights.filter((insight) => insight.persona === persona), [insights, persona]);
  const statusLabel = state.status === "Live" ? "LIVE" : state.status === "FullTime" ? "FULL TIME" : state.status.toUpperCase();
  const foundryStatus = health?.foundry ?? "Checking";
  const apiStatus = healthError
    ? "Unverified"
    : health?.api ?? (connection === "disconnected" ? "Unavailable" : "Checking");
  const simulatorStatus = health?.simulator ?? "Checking";
  const isRunning = state.status === "Live";

  const command = async (name: string, path: string, method = "POST", body?: unknown) => {
    setBusy(name);
    setError("");
    try {
      await api(path, { method, body: body === undefined ? undefined : JSON.stringify(body) });
      await loadSnapshot();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "The request could not be completed.");
    } finally {
      setBusy("");
    }
  };

  const selectPersona = async (next: Persona) => {
    personaRef.current = next;
    setPersona(next);
    setSelectedEvidence([]);
    setError("");
    try {
      const nextInsights = await api<Insight[]>(`/api/insights?persona=${next}`);
      setInsights(nextInsights.reverse());
      await hubRef.current?.invoke("SetPersona", next);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not change the broadcast persona.");
    }
  };

  const requestDesk = async () => {
    setBusy("desk");
    setError("");
    try {
      const latestKey = [...events].reverse().find((event) => event.isKey);
      const result = await api<AnalystDeskResult>("/api/analyst-desk", {
        method: "POST",
        body: JSON.stringify({ eventId: latestKey?.id ?? null, persona }),
      });
      setDesk(result);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "The analyst desk request failed.");
    } finally {
      setBusy("");
    }
  };

  const handleClip = async (file?: File) => {
    if (!file) return;
    setError("");
    setReferee(null);
    setIsPlaying(false);
    setClipFrames([]);
    setFrameIndex(0);
    setSyntheticDescription("");
    setClipName(file.name);
    try {
      const url = URL.createObjectURL(file);
      let frames: string[];
      try {
        frames = await extractFrames(url);
      } finally {
        URL.revokeObjectURL(url);
      }
      setClipFrames(frames);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not read the selected clip.");
      setClipName("");
    }
  };

  const loadSampleFrames = async () => {
    setSampleLoading(true);
    setError("");
    setReferee(null);
    setIsPlaying(false);
    try {
      const candidates = syntheticScenarios.filter((scenario) => scenario.path !== lastSampleScenario.current);
      const scenario = candidates[Math.floor(Math.random() * candidates.length)] ?? syntheticScenarios[0];
      const sampleFrames = await Promise.all(
        Array.from({ length: 8 }, (_, index) =>
          imageFileToJpegDataUrl(`/samples/${scenario.path}/frame-${String(index + 1).padStart(2, "0")}.png`)),
      );
      setClipFrames(sampleFrames);
      setClipName(scenario.title);
      setSyntheticDescription(scenario.description);
      lastSampleScenario.current = scenario.path;
      setFrameIndex(0);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load the synthetic sample frames.");
    } finally {
      setSampleLoading(false);
    }
  };

  const requestReferee = async () => {
    if (!clipFrames.length) return;
    setBusy("referee");
    setError("");
    try {
      const verdict = await api<RefereeVerdict>("/api/referee/check", {
        method: "POST",
        body: JSON.stringify({
          persona,
          mode: refereeMode,
          frames: clipFrames,
          linkedEventId: [...events].reverse().find((event) => event.isKey)?.id ?? null,
        }),
      });
      setReferee(verdict);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "The video review request failed.");
    } finally {
      setBusy("");
    }
  };

  const toggleEvidence = (ids: string[]) => setSelectedEvidence((current) =>
    ids.every((id) => current.includes(id)) ? current.filter((id) => !ids.includes(id)) : [...new Set([...current, ...ids])]);

  return (
    <MotionConfig reducedMotion="user">
    <main className={`app-shell persona-${persona.toLowerCase()}`} data-theme={theme}>
      <header className="topbar">
        <a className="brand" href="#" aria-label="RTMI home">
          <span className="brand-mark"><Activity size={19} strokeWidth={2.6} /></span>
          <span className="brand-word">RTMI<span className="brand-period">.</span></span>
        </a>
        <div className="topbar-center">
          <span className="product-label">REAL-TIME MATCH INTELLIGENCE</span>
          <span className="topbar-divider" />
          <span className="match-label">PREMIER LEAGUE · DEMO MATCH</span>
        </div>
        <div className="topbar-status">
          <span className={`connection-indicator ${connection}`} />
          <span>{connection === "connected" ? "CONNECTED" : connection === "connecting" ? "CONNECTING" : "OFFLINE"}</span>
          <span className="topbar-divider" />
          <span className={`foundry-indicator ${foundryStatus.toLowerCase()}`} />
          <span>AI {foundryStatus.toUpperCase()}</span>
        </div>
      </header>

      <div className="page-wrap">
        <details className="health-dashboard">
          <summary>
            <Activity size={14} />
            <span>SYSTEM HEALTH</span>
            <HealthStatus service="API" value={apiStatus} />
            <HealthStatus service="SIM" value={simulatorStatus} />
            <HealthStatus service="FOUNDRY" value={foundryStatus} />
          </summary>
          <div className="health-details">
            <div className="health-services">
              <HealthRow service="API" value={apiStatus} detail="HTTP endpoint" />
              <HealthRow service="Simulator" value={simulatorStatus} detail="Match process" />
              <HealthRow
                service="Foundry"
                value={foundryStatus}
                detail={foundryStatus === "Configured"
                  ? "Endpoint configured; authentication and agent availability are not checked"
                  : foundryStatus === "FallbackOnly"
                    ? "Model calls disabled; labeled templates are active"
                    : "Endpoint not configured"}
              />
            </div>
            <div className="health-refresh">
              <span>{healthUpdatedAt ? `Updated ${healthUpdatedAt.toLocaleTimeString()}` : "Waiting for health status"}</span>
              <button className="icon-button" onClick={() => void refreshHealth()} disabled={healthRefreshing} aria-label="Refresh service health">
                <RefreshCw size={14} className={healthRefreshing ? "spin" : ""} />
              </button>
            </div>
            {healthError && <p className="health-error" role="status">{healthError}</p>}
          </div>
        </details>
        {error && (
          <div className="error-banner" role="alert">
            <ShieldAlert size={17} /><span>{error}</span>
            <button className="retry-button" onClick={() => window.location.reload()}>RETRY</button>
            <button className="icon-button" onClick={() => setError("")} aria-label="Dismiss error"><X size={16} /></button>
          </div>
        )}
        <section className="match-strip">
          <div className="match-meta"><span>RTMI STADIUM</span><span className="meta-dot">·</span><span>DEMO MATCH · 90 MIN</span></div>
          <div className="scorebug">
            <div className="score-team home-team"><span className="crest lions-crest">HL</span><span className="team-abbr">HAR</span><span className="team-name-full">Harare Lions</span></div>
            <div className="score-center">
              <div className={`score-numbers ${scorePulse ? "score-pulse" : ""}`}><span>{state.home.score}</span><i>:</i><span>{state.away.score}</span></div>
              <div className="match-clock"><span className={`live-pulse ${isRunning ? "pulsing" : ""}`} />{fmtTime(state.minute)}<span className={`match-state ${isRunning ? "active" : ""}`}>{statusLabel}</span></div>
            </div>
            <div className="score-team away-team"><span className="team-name-full">Bulawayo Eagles</span><span className="team-abbr">BUL</span><span className="crest eagles-crest">BE</span></div>
          </div>
          <div className="match-controls">
            <div className="speed-select"><Clock3 size={13} /><select value={speed} onChange={(event) => setSpeed(Number(event.target.value))} aria-label="Simulation speed">
              <option value={15}>15×</option><option value={30}>30×</option><option value={60}>60×</option><option value={120}>120×</option>
            </select><ChevronDown size={12} /></div>
            <button className={`control-button primary ${isRunning ? "pause-button" : ""}`} onClick={() => void command("match", isRunning ? "/api/match/pause" : `/api/match/start?speed=${speed}`)} disabled={Boolean(busy)}>
              {busy === "match" ? <LoaderCircle size={15} className="spin" /> : isRunning ? <Pause size={15} fill="currentColor" /> : <Play size={15} fill="currentColor" />}
              {isRunning ? "PAUSE" : "START MATCH"}
            </button>
            <button className="control-button secondary" onClick={() => void command("demo", "/api/match/demo")} disabled={Boolean(busy)}><Sparkles size={14} /> DEMO RUN</button>
            <button className="icon-button reset-button" onClick={() => void command("reset", "/api/match/reset")} disabled={Boolean(busy)} aria-label="Reset match"><RotateCcw size={15} /></button>
          </div>
        </section>

        <section className="persona-row">
          <div className="persona-copy"><span className="eyebrow">YOUR BROADCAST</span><span className="persona-heading">Choose your lens</span></div>
          <div className="persona-list" role="tablist" aria-label="Broadcast persona">
            {personas.map((item) => (
              <button key={item.value} role="tab" aria-selected={persona === item.value} className={`persona-chip ${persona === item.value ? "selected" : ""}`} onClick={() => void selectPersona(item.value)}>
                <span className="persona-icon">{item.icon}</span>{item.label}
              </button>
            ))}
          </div>
          <div className="persona-actions">
            <div className="theme-selector" role="group" aria-label="Color theme">
              <button className={theme === "dark" ? "active" : ""} onClick={() => setTheme("dark")} aria-pressed={theme === "dark"} title="Dark mode"><Moon size={15} /><span>DARK</span></button>
              <button className={theme === "light" ? "active" : ""} onClick={() => setTheme("light")} aria-pressed={theme === "light"} title="Light mode"><Sun size={15} /><span>LIGHT</span></button>
              <button className={theme === "live" ? "active" : ""} onClick={() => setTheme("live")} aria-pressed={theme === "live"} title="Live stadium mode"><Radio size={15} /><span>LIVE</span></button>
            </div>
            <button className="modal-launch desk-launch" onClick={() => { setActiveModal("desk"); setDesk(null); void requestDesk(); }} disabled={Boolean(busy)}>
              {busy === "desk" ? <LoaderCircle size={16} className="spin" /> : <BarChart3 size={16} />} ANALYST DESK
            </button>
            <button className="modal-launch review-launch" onClick={() => setActiveModal("review")}>
              <ShieldAlert size={16} /> REVIEW ROOM VAR
            </button>
          </div>
        </section>

        <div className="dashboard-grid">
          <div className="dashboard-column main-column">
            <section className="panel match-stats-panel">
              <div className="panel-heading">
                <div><span className="eyebrow">THE NUMBERS</span><h2>Match centre</h2></div>
                <span className="panel-live"><span className="mini-live-dot" />LIVE DATA</span>
              </div>
              <div className="stats-teams">
                <span className="team-tag home-tag"><span />HAR</span><span className="stats-heading">MATCH STATS</span><span className="team-tag away-tag">BUL<span /></span>
              </div>
              <StatRow label="Possession" home={state.home.possession} away={state.away.possession} format={(value) => `${value}%`} />
              <StatRow label="Shots" home={state.home.shots} away={state.away.shots} />
              <StatRow label="Expected goals" home={state.home.xg} away={state.away.xg} format={(value) => Number(value).toFixed(2)} />
              <StatRow label="Fouls" home={state.home.fouls} away={state.away.fouls} />
              <div className="momentum-block">
                <div className="momentum-head"><span><Activity size={14} /> MOMENTUM</span><span className="momentum-readout"><b>{Math.round(state.momentum.home * 100)}</b><i>—</i><b>{Math.round(state.momentum.away * 100)}</b></span></div>
                <div className="momentum-chart" aria-label="Live momentum chart">
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={chartData} margin={{ top: 7, right: 0, bottom: 0, left: 0 }}>
                      <defs>
                        <linearGradient id="homeMomentum" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#c8ff4d" stopOpacity={0.24} /><stop offset="100%" stopColor="#c8ff4d" stopOpacity={0} /></linearGradient>
                        <linearGradient id="awayMomentum" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#5c8bff" stopOpacity={0.22} /><stop offset="100%" stopColor="#5c8bff" stopOpacity={0} /></linearGradient>
                      </defs>
                      <CartesianGrid stroke="#ffffff0d" vertical={false} />
                      <XAxis dataKey="minute" tickLine={false} axisLine={false} tick={{ fill: "#686e69", fontSize: 10 }} tickFormatter={(value) => `${value}'`} />
                      <YAxis domain={[0, 1]} hide />
                      <Tooltip content={<MomentumTooltip />} />
                      <Area type="monotone" dataKey="home" stroke="#c8ff4d" strokeWidth={2} fill="url(#homeMomentum)" isAnimationActive={!prefersReducedMotion} animationDuration={350} />
                      <Area type="monotone" dataKey="away" stroke="#5c8bff" strokeWidth={2} fill="url(#awayMomentum)" isAnimationActive={!prefersReducedMotion} animationDuration={350} />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>
                <div className="chart-legend"><span><i className="legend-home" />HARARE LIONS</span><span><i className="legend-away" />BULAWAYO EAGLES</span></div>
              </div>
            </section>

          </div>

          <div className="dashboard-column middle-column">
            <section className="panel narrator-panel">
              <div className="panel-heading">
                <div><span className="eyebrow">YOUR PERSONAL COMMENTARY</span><h2>From the booth</h2></div>
                <span className="panel-icon"><Radio size={16} /></span>
              </div>
              <div className="narrator-feed">
                {!visibleInsights.length && <EmptyState icon={<Sparkles size={18} />} title="The booth is warming up" body={isRunning ? "Your personalized commentary will appear as key moments unfold." : "Kick things off and your commentator will be on the call."} />}
                <AnimatePresence initial={false}>
                  {visibleInsights.map((insight) => (
                    <motion.article key={insight.id} layout initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="insight-card">
                      <div className="insight-meta"><span className="insight-live"><span />{insight.kind === "Narration" ? "LIVE CALL" : insight.kind.toUpperCase()}</span><span className="insight-time">{new Date(insight.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</span></div>
                      <p>{insight.text}</p>
                      <button className="why-toggle" onClick={() => toggleEvidence(insight.evidenceEventIds)} aria-expanded={insight.evidenceEventIds.some((id) => selectedEvidence.includes(id))}>
                        <CircleHelp size={14} /> WHY THIS? <span className="receipt-count">{insight.evidenceEventIds.length} RECEIPT{insight.evidenceEventIds.length === 1 ? "" : "S"}</span><ArrowDown size={13} />
                      </button>
                      {insight.evidenceEventIds.some((id) => selectedEvidence.includes(id)) && <div className="why-details"><b>{insight.why}</b><div className="evidence-links">{insight.evidenceEventIds.map((id) => <button key={id} onClick={() => toggleEvidence([id])}>{id}<ArrowUpRight size={11} /></button>)}</div></div>}
                      <div className="insight-footer"><span className={`source-badge ${insight.source.toLowerCase() === "fallback" ? "fallback" : ""}`}>{insight.source.toLowerCase() === "fallback" ? "TEMPLATE" : insight.source}</span><span className="confidence">CONFIDENCE <b>{Math.round(insight.confidence * 100)}%</b></span></div>
                    </motion.article>
                  ))}
                </AnimatePresence>
              </div>
            </section>

          </div>

          <div className="dashboard-column side-column">
            <section className="panel timeline-panel">
              <div className="panel-heading">
                <div><span className="eyebrow">THE STORY SO FAR</span><h2>Match timeline</h2></div>
                <div className="filter-tabs"><button className={eventFilter === "all" ? "active" : ""} onClick={() => setEventFilter("all")}>ALL</button><button className={eventFilter === "key" ? "active" : ""} onClick={() => setEventFilter("key")}>KEY MOMENTS</button></div>
              </div>
              <div className="timeline-list">
                {!filteredEvents.length && <EmptyState icon={<Radio size={18} />} title="Waiting for kick-off" body="Start the match to see every moment appear here." />}
                {filteredEvents.map((event) => (
                  <button key={event.id} className={`timeline-item ${event.isKey ? "key-event" : ""} ${selectedEvidence.includes(event.id) ? "evidence-active" : ""}`} onClick={() => toggleEvidence([event.id])}>
                    <span className="timeline-time">{fmtTime(event.minute, event.second)}</span>
                    <span className={`timeline-icon ${event.team === "HOME" ? "home-event" : "away-event"} type-${event.type.toLowerCase()}`}>{iconFor(event.type)}</span>
                    <span className="timeline-content"><b>{event.type === "MomentumShift" ? "Momentum swing" : `${readableType(event.type)}${event.outcome ? ` · ${event.outcome}` : ""}`}</b><small>{event.player || (event.team === "HOME" ? "Harare Lions" : "Bulawayo Eagles")}</small></span>
                    <span className={`event-team-pill ${event.team === "HOME" ? "home-pill" : "away-pill"}`}>{event.team === "HOME" ? "HAR" : "BUL"}</span>
                    <span className="event-id">{event.id}</span>
                  </button>
                ))}
              </div>
              {events.length > 18 && <div className="timeline-foot">Showing the latest {filteredEvents.length} of {events.length} events</div>}
            </section>

            <section className="panel match-insight-panel">
              <div className="panel-heading"><div><span className="eyebrow">PULSE CHECK</span><h2>At a glance</h2></div><Flame size={16} className="subtle-icon" /></div>
              <div className="quick-stat"><span className="quick-stat-icon"><Target size={15} /></span><span>Shots</span><b>{state.home.shots} <i>—</i> {state.away.shots}</b></div>
              <div className="quick-stat"><span className="quick-stat-icon"><Users size={15} /></span><span>Possession leader</span><b>{state.home.possession > state.away.possession ? "HAR" : state.away.possession > state.home.possession ? "BUL" : "EVEN"}</b></div>
              <div className="quick-stat"><span className="quick-stat-icon"><Activity size={15} /></span><span>Pressure</span><b>{state.momentum.home > 0.58 ? "HAR ↑" : state.momentum.away > 0.58 ? "BUL ↑" : "BALANCED"}</b></div>
            </section>
            <section className="receipt-note"><BadgeCheck size={17} /><div><b>Every insight has receipts.</b><span>Tap “Why this?” to see the match events behind each claim.</span></div></section>
          </div>
        </div>

        {activeModal && (
          <div className="modal-backdrop" onMouseDown={(event) => {
            if (event.target === event.currentTarget) setActiveModal(null);
          }}>
            <section className="fullscreen-modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
              <header className="modal-header">
                <div>
                  <span className="eyebrow">{activeModal === "desk" ? "THREE VOICES. ONE VERDICT." : "VIDEO ASSISTANT REFEREE"}</span>
                  <h2 id="modal-title">{activeModal === "desk" ? "Analyst desk" : "Review room VAR"}</h2>
                </div>
                <button className="icon-button modal-close" onClick={() => setActiveModal(null)} aria-label="Close dialog"><X size={20} /></button>
              </header>
              <div className="modal-body">
                {activeModal === "desk" && (
                  desk ? (
                    <>
                      <div className="desk-takes">{desk.takes.map((take) => <article key={take.role} className="take-card"><div className="take-heading"><span className={`take-role role-${take.role.toLowerCase()}`}>{take.role}</span><span className={`source-badge ${take.source.toLowerCase() === "fallback" ? "fallback" : ""}`}>{take.source.toLowerCase() === "fallback" ? "TEMPLATE" : take.source}</span></div><p>{take.text}</p><button className="why-link" onClick={() => toggleEvidence(take.evidenceEventIds)}>{take.why} <span>WHY ↗</span></button></article>)}</div>
                      <div className="verdict-card"><div><BadgeCheck size={18} /><span>FINAL VERDICT</span><span className={`source-badge ${desk.verdict.source.toLowerCase() === "fallback" ? "fallback" : ""}`}>{desk.verdict.source.toLowerCase() === "fallback" ? "TEMPLATE" : desk.verdict.source}</span></div><p>{desk.verdict.text}</p><button className="why-link" onClick={() => toggleEvidence(desk.verdict.evidenceEventIds)}>{desk.verdict.why} <span>{Math.round(desk.verdict.confidence * 100)}% CONFIDENT</span></button></div>
                    </>
                  ) : (
                    <EmptyState icon={<LoaderCircle size={22} className={busy === "desk" ? "spin" : ""} />} title={busy === "desk" ? "The analysts are conferring" : "No analyst report yet"} body={busy === "desk" ? "Tactics, stats and fan perspective are being brought together." : "Close this window and open the analyst desk to request a fresh report."} />
                  )
                )}
                {activeModal === "review" && (
                  <section className="panel referee-panel" aria-label="VAR review controls">
                    <div className="panel-heading"><div><span className="eyebrow">ASSISTED VIDEO REVIEW</span><h2>Submit a match clip</h2></div><span className="var-mark">VAR</span></div>
                    <div className="review-upload-column">
                      <div className="referee-upload" onClick={() => videoInputRef.current?.click()} onKeyDown={(event) => {
                        if (event.key === "Enter" || event.key === " ") {
                          event.preventDefault();
                          videoInputRef.current?.click();
                        }
                      }} role="button" tabIndex={0}>
                        <input ref={videoInputRef} type="file" accept="video/*" hidden onChange={(event) => void handleClip(event.target.files?.[0])} />
                        <span className="upload-icon"><Video size={20} /></span>
                        <b>{clipName || "Drop a match clip"}</b>
                        <small>{clipName ? `${clipFrames.length} frames sampled · click to change` : "MP4, MOV · 5–15 seconds"}</small>
                        <span className="upload-action">{clipName ? "CHANGE CLIP" : "BROWSE FILES"} <ArrowUpRight size={12} /></span>
                      </div>
                      <button className="sample-load-button" onClick={() => void loadSampleFrames()} disabled={sampleLoading || Boolean(busy)}>
                        {sampleLoading ? <LoaderCircle size={13} className="spin" /> : <Sparkles size={13} />}
                        {sampleLoading ? "ROLLING SCENARIO…" : "ROLL RANDOM SCENARIO"}
                      </button>
                    </div>
                    <div className="review-clip-column">
                      {clipFrames.length > 0 ? <>
                      <div className="sample-frame-preview">
                        <img src={clipFrames[frameIndex]} alt={`Synthetic match frame ${frameIndex + 1} of ${clipFrames.length}`} />
                        <span className="sample-frame-counter">FRAME {String(frameIndex + 1).padStart(2, "0")} / {String(clipFrames.length).padStart(2, "0")}</span>
                      </div>
                      <div className="playback-controls">
                        <button className="playback-step" onClick={() => { setIsPlaying(false); setFrameIndex((frameIndex - 1 + clipFrames.length) % clipFrames.length); }} aria-label="Previous frame"><ChevronLeft size={15} /></button>
                        <button className="playback-toggle" onClick={() => setIsPlaying((playing) => !playing)} aria-label={isPlaying ? "Pause frame animation" : "Play frame animation"}>
                          {isPlaying ? <Pause size={14} /> : <Play size={14} fill="currentColor" />}
                          {isPlaying ? "PAUSE" : "PLAY ANIMATION"}
                        </button>
                        <button className="playback-step" onClick={() => { setIsPlaying(false); setFrameIndex((frameIndex + 1) % clipFrames.length); }} aria-label="Next frame"><ChevronRight size={15} /></button>
                      </div>
                      <div className="frame-strip">{clipFrames.map((frame, index) => <button className={index === frameIndex ? "active" : ""} onClick={() => { setIsPlaying(false); setFrameIndex(index); }} aria-label={`Show frame ${index + 1}`} key={index}><img src={frame} alt="" /></button>)}</div>
                      {syntheticDescription && <p className="sample-description">{syntheticDescription}</p>}
                      </> : <EmptyState icon={<Video size={20} />} title="No clip loaded" body="Choose a match clip or roll a synthetic scenario to preview the evidence here." />}
                    </div>
                    <div className="review-action-column">
                    <div className="review-mode"><span>REVIEW MODE</span><div className="mode-switch"><button className={refereeMode === "Referee" ? "active" : ""} onClick={() => setRefereeMode("Referee")}>REFEREE</button><button className={refereeMode === "Analyst" ? "active" : ""} onClick={() => setRefereeMode("Analyst")}>ANALYST</button></div></div>
                    <button className="review-button" onClick={() => void requestReferee()} disabled={!clipFrames.length || Boolean(busy)}>{busy === "referee" ? <LoaderCircle size={15} className="spin" /> : <ShieldAlert size={15} />} SEND FOR REVIEW</button>
                    {!referee && <div className="review-note"><ShieldAlert size={14} /><span>AI-assisted review. A human referee makes the final call.</span></div>}
                    </div>
                    {referee && <div className="review-result-column">
                    <div className="referee-result">
                      <div className="var-stamp">VAR <b>{readableLabel(referee.decision)}</b><span className={`source-badge ${referee.source.toLowerCase() === "fallback" ? "fallback" : ""}`}>{referee.source.toLowerCase() === "fallback" ? "TEMPLATE" : referee.source}</span></div>
                      <p>{referee.personaText || referee.text}</p>
                      <small>{referee.why}</small>
                      {referee.evidenceFrameIndexes.length > 0 && <div className="referee-evidence"><span>EVIDENCE FRAMES</span>{referee.evidenceFrameIndexes.map((index) => <button key={index} onClick={() => { setIsPlaying(false); setFrameIndex(index); }}>FRAME {String(index + 1).padStart(2, "0")}</button>)}</div>}
                      {referee.evidenceEventIds.length > 0 && <div className="referee-evidence"><span>EVENT RECEIPTS</span>{referee.evidenceEventIds.map((id) => <button key={id} onClick={() => toggleEvidence([id])}>{id}</button>)}</div>}
                      <span className="review-confidence">CONFIDENCE {Math.round(referee.confidence * 100)}%</span>
                    </div>
                    </div>}
                  </section>
                )}
              </div>
            </section>
          </div>
        )}
        <footer className="app-footer"><span>RTMI <b>·</b> REAL-TIME MATCH INTELLIGENCE</span><span><Wifi size={13} /> {connection === "connected" ? "SIGNALR STREAM ACTIVE" : <><WifiOff size={13} /> SIGNALR DISCONNECTED</>}</span><span>BUILT FOR THE BEAUTIFUL GAME <span className="footer-ball">●</span></span></footer>
      </div>
      {selectedEvidence.length > 0 && <div className="evidence-toast"><span><BadgeCheck size={14} /> {selectedEvidence.length} EVENT RECEIPT{selectedEvidence.length === 1 ? "" : "S"} HIGHLIGHTED</span><button onClick={() => setSelectedEvidence([])}>CLEAR <X size={12} /></button></div>}
    </main>
    </MotionConfig>
  );
}

function HealthStatus({ service, value }: { service: string; value: string }) {
  return <span className={`health-pill ${healthTone(service, value)}`}><i />{service} {value.toUpperCase()}</span>;
}

function HealthRow({ service, value, detail }: { service: string; value: string; detail: string }) {
  return <div className="health-row">
    <HealthStatus service={service} value={value} />
    <span>{detail}</span>
  </div>;
}

function healthTone(service: string, value: string) {
  if (service === "API") {
    if (value === "Healthy") return "healthy";
    if (value === "Unavailable") return "unhealthy";
    if (value === "Unverified") return "fallback";
  }
  if (service === "Simulator") return value === "Unavailable" ? "unhealthy" : "neutral";
  if (value === "Configured") return "configured";
  if (value === "FallbackOnly") return "fallback";
  if (value === "NotConfigured") return "unconfigured";
  return "neutral";
}

function StatRow({ label, home, away, format = (value: number) => String(value) }: { label: string; home: number; away: number; format?: (value: number) => string }) {
  const homeWins = home > away;
  const awayWins = away > home;
  return <div className="stat-row">
    <span className={homeWins ? "stat-value lead" : "stat-value"}>{format(home)}</span>
    <div className="stat-meter"><span>{label}</span><div className="meter-track"><i style={{ width: `${home + away === 0 ? 50 : home / (home + away) * 100}%` }} /></div></div>
    <span className={awayWins ? "stat-value lead away-value" : "stat-value away-value"}>{format(away)}</span>
  </div>;
}

function EmptyState({ icon, title, body }: { icon: React.ReactNode; title: string; body: string }) {
  return <div className="empty-state"><span className="empty-icon">{icon}</span><b>{title}</b><p>{body}</p></div>;
}

function MomentumTooltip({ active, payload, label }: { active?: boolean; payload?: { dataKey: string; value: number }[]; label?: number }) {
  if (!active || !payload?.length) return null;
  return <div className="chart-tooltip"><b>{label}'</b>{payload.map((item) => <span key={item.dataKey} className={item.dataKey}>{item.dataKey === "home" ? "HAR" : "BUL"} {Math.round(item.value * 100)}%</span>)}</div>;
}

async function extractFrames(url: string): Promise<string[]> {
  const video = document.createElement("video");
  video.preload = "metadata";
  video.muted = true;
  video.playsInline = true;
  video.src = url;
  await new Promise<void>((resolve, reject) => {
    video.onloadedmetadata = () => resolve();
    video.onerror = () => reject(new Error("The selected file could not be decoded as a video."));
  });
  if (!Number.isFinite(video.duration) || video.duration < 5 || video.duration > 15) {
    throw new Error("Choose a clip between 5 and 15 seconds long.");
  }
  const canvas = document.createElement("canvas");
  const scale = Math.min(1, 640 / video.videoWidth);
  canvas.width = Math.max(1, Math.round(video.videoWidth * scale));
  canvas.height = Math.max(1, Math.round(video.videoHeight * scale));
  const context = canvas.getContext("2d");
  if (!context) throw new Error("This browser cannot sample video frames.");
  const frames: string[] = [];
  for (let index = 0; index < 8; index++) {
    const time = video.duration * (index + 0.5) / 8;
    await new Promise<void>((resolve, reject) => {
      video.onseeked = () => resolve();
      video.onerror = () => reject(new Error("Could not sample the selected video."));
      video.currentTime = time;
    });
    context.drawImage(video, 0, 0, canvas.width, canvas.height);
    frames.push(canvas.toDataURL("image/jpeg", 0.72));
  }
  video.removeAttribute("src");
  video.load();
  return frames;
}

async function imageFileToJpegDataUrl(path: string): Promise<string> {
  const response = await fetch(path);
  if (!response.ok) throw new Error(`Could not load sample frame (${response.status}).`);
  const bitmap = await createImageBitmap(await response.blob());
  const scale = Math.min(1, 640 / bitmap.width);
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bitmap.width * scale));
  canvas.height = Math.max(1, Math.round(bitmap.height * scale));
  const context = canvas.getContext("2d");
  if (!context) {
    bitmap.close();
    throw new Error("This browser cannot prepare the sample frame for review.");
  }
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  return canvas.toDataURL("image/jpeg", 0.82);
}

export default App;
