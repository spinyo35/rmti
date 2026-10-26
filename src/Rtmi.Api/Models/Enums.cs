namespace Rtmi.Api.Models;

public enum Team { HOME, AWAY }
public enum EventType { KickOff, Pass, Shot, Goal, Foul, YellowCard, RedCard, Corner, Save, Substitution, Offside, HalfTime, FullTime, MomentumShift }
public enum Persona { Casual, StatsNerd, Kid, Tactician }
public enum InsightKind { Narration, Analysis, Verdict, Answer }
public enum MatchStatus { NotStarted, Live, Paused, HalfTime, FullTime }
public enum RefereeMode { Referee, Analyst }
public enum RefereeDecision { GoalConfirmed, Offside, Penalty, Foul, NoAction }
