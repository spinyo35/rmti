export type Persona = "Casual" | "StatsNerd" | "Kid" | "Tactician";
export type Team = "HOME" | "AWAY";
export type EventType =
  | "KickOff" | "Pass" | "Shot" | "Goal" | "Foul" | "YellowCard" | "RedCard"
  | "Corner" | "Save" | "Substitution" | "Offside" | "HalfTime" | "FullTime"
  | "MomentumShift";

export interface MatchEvent {
  id: string;
  matchId: string;
  minute: number;
  second: number;
  type: EventType;
  team: Team;
  player: string;
  x: number;
  y: number;
  outcome?: string | null;
  xg?: number | null;
  isKey: boolean;
  relatedEventIds?: string[] | null;
}

export interface TeamState {
  name: string;
  score: number;
  shots: number;
  xg: number;
  possession: number;
  fouls: number;
}

export interface MatchState {
  matchId: string;
  minute: number;
  status: "NotStarted" | "Live" | "Paused" | "HalfTime" | "FullTime";
  home: TeamState;
  away: TeamState;
  momentum: { home: number; away: number };
}

export interface Insight {
  id: string;
  kind: "Narration" | "Analysis" | "Verdict" | "Answer";
  persona: Persona;
  source: string;
  text: string;
  why: string;
  evidenceEventIds: string[];
  confidence: number;
  createdAt: string;
}

export interface DeskTake {
  role: "Tactics" | "Stats" | "Fan";
  text: string;
  why: string;
  evidenceEventIds: string[];
  confidence: number;
  source: string;
}

export interface AnalystDeskResult {
  eventId: string;
  persona: Persona;
  takes: DeskTake[];
  verdict: Insight;
}

export interface RefereeVerdict {
  id: string;
  mode: "Referee" | "Analyst";
  decision: "GoalConfirmed" | "Offside" | "Penalty" | "Foul" | "NoAction";
  text: string;
  why: string;
  evidenceFrameIndexes: number[];
  evidenceEventIds: string[];
  confidence: number;
  personaText: string;
  source: string;
  createdAt: string;
}

export interface HealthReport {
  status: string;
  api: string;
  simulator: string;
  foundry: string;
}
