# RTMI Foundry agent sample test prompts

Use these sample payloads to test the portal-defined agents. Paste the relevant JSON into each agent's test/playground input, and for `rtmi-video-referee` attach the indicated images as image inputs as well as including the text payload.

The event IDs below are sample IDs. When testing through the running API, replace them with IDs that exist in its EventStore so the API evidence validator accepts them. Check that each response is exactly one valid JSON object with only the keys specified by that agent's system instructions.

## 1. `rtmi-narrator` — goal, Stats Nerd persona

Tests concise commentary, use of a supplied xG figure, and event receipts.

```json
{
  "persona": "StatsNerd",
  "event": {
    "id": "evt_0033",
    "matchId": "m1",
    "minute": 33,
    "second": 44,
    "type": "Goal",
    "team": "HOME",
    "player": "J. Moyo",
    "x": 88,
    "y": 38,
    "outcome": "Scored",
    "xg": 0.42,
    "isKey": true
  },
  "recentEvents": [
    {
      "id": "evt_0032",
      "matchId": "m1",
      "minute": 33,
      "second": 12,
      "type": "Pass",
      "team": "HOME",
      "player": "T. Zvobgo",
      "x": 76,
      "y": 40,
      "isKey": false
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 33,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 1,
      "shots": 3,
      "xg": 0.81,
      "possession": 56,
      "fouls": 4
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 0,
      "shots": 2,
      "xg": 0.24,
      "possession": 44,
      "fouls": 5
    },
    "momentum": {
      "home": 0.68,
      "away": 0.32
    }
  }
}
```

Try again with `persona` set to `Casual`, `Kid`, and `Tactician`. Wording should adapt without changing the facts or evidence IDs.

## 2. `rtmi-tactics` — red card after a recent attacking sequence

Tests tactical restraint: the supplied sequence supports location and sequence, but does not establish formations, intent, or why the foul occurred.

```json
{
  "persona": "Tactician",
  "event": {
    "id": "evt_0058",
    "matchId": "m1",
    "minute": 58,
    "second": 3,
    "type": "RedCard",
    "team": "AWAY",
    "player": "S. Dube",
    "x": 55,
    "y": 40,
    "outcome": "Sent off",
    "isKey": true
  },
  "recentEvents": [
    {
      "id": "evt_0055",
      "matchId": "m1",
      "minute": 57,
      "second": 18,
      "type": "Pass",
      "team": "HOME",
      "player": "K. Sibanda",
      "x": 68,
      "y": 42,
      "isKey": false
    },
    {
      "id": "evt_0056",
      "matchId": "m1",
      "minute": 57,
      "second": 31,
      "type": "Shot",
      "team": "HOME",
      "player": "J. Moyo",
      "x": 82,
      "y": 39,
      "outcome": "Saved",
      "xg": 0.14,
      "isKey": true
    },
    {
      "id": "evt_0057",
      "matchId": "m1",
      "minute": 57,
      "second": 50,
      "type": "Foul",
      "team": "AWAY",
      "player": "S. Dube",
      "x": 55,
      "y": 40,
      "outcome": "Sent off",
      "isKey": true
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 58,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 1,
      "shots": 5,
      "xg": 1.14,
      "possession": 55,
      "fouls": 6
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 0,
      "shots": 3,
      "xg": 0.38,
      "possession": 45,
      "fouls": 8
    },
    "momentum": {
      "home": 0.71,
      "away": 0.29
    }
  }
}
```

## 3. `rtmi-stats` — late equaliser with a cautious statistical read

Tests the agent's ability to distinguish the scoreline from broader match statistics and avoid claiming that one team has dominated.

```json
{
  "persona": "StatsNerd",
  "event": {
    "id": "evt_0088",
    "matchId": "m1",
    "minute": 88,
    "second": 57,
    "type": "Goal",
    "team": "AWAY",
    "player": "T. Ncube",
    "x": 81,
    "y": 42,
    "outcome": "Scored",
    "xg": 0.31,
    "isKey": true
  },
  "recentEvents": [
    {
      "id": "evt_0086",
      "matchId": "m1",
      "minute": 87,
      "second": 40,
      "type": "Shot",
      "team": "AWAY",
      "player": "L. Ndlovu",
      "x": 75,
      "y": 48,
      "outcome": "Saved",
      "xg": 0.12,
      "isKey": true
    },
    {
      "id": "evt_0087",
      "matchId": "m1",
      "minute": 88,
      "second": 20,
      "type": "Corner",
      "team": "AWAY",
      "player": "M. Khumalo",
      "x": 94,
      "y": 8,
      "isKey": false
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 88,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 1,
      "shots": 9,
      "xg": 1.63,
      "possession": 53,
      "fouls": 10
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 1,
      "shots": 7,
      "xg": 1.21,
      "possession": 47,
      "fouls": 12
    },
    "momentum": {
      "home": 0.37,
      "away": 0.63
    }
  }
}
```

## 4. `rtmi-fan` — equaliser as a match-story moment

Tests supporter-focused framing grounded in the actual score and event, without inventing a crowd reaction or emotional backstory.

```json
{
  "persona": "Casual",
  "event": {
    "id": "evt_0088",
    "matchId": "m1",
    "minute": 88,
    "second": 57,
    "type": "Goal",
    "team": "AWAY",
    "player": "T. Ncube",
    "x": 81,
    "y": 42,
    "outcome": "Scored",
    "xg": 0.31,
    "isKey": true
  },
  "recentEvents": [
    {
      "id": "evt_0033",
      "matchId": "m1",
      "minute": 33,
      "second": 44,
      "type": "Goal",
      "team": "HOME",
      "player": "J. Moyo",
      "x": 88,
      "y": 38,
      "outcome": "Scored",
      "xg": 0.42,
      "isKey": true
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 88,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 1,
      "shots": 9,
      "xg": 1.63,
      "possession": 53,
      "fouls": 10
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 1,
      "shots": 7,
      "xg": 1.21,
      "possession": 47,
      "fouls": 12
    },
    "momentum": {
      "home": 0.37,
      "away": 0.63
    }
  }
}
```

## 5. `rtmi-verdict` — synthesize distinct desk perspectives

Tests synthesis of useful agreement while preserving disagreement and uncertainty. The Stats take explicitly notes the limits of possession and xG.

```json
{
  "persona": "Casual",
  "event": {
    "id": "evt_0058",
    "matchId": "m1",
    "minute": 58,
    "second": 3,
    "type": "RedCard",
    "team": "AWAY",
    "player": "S. Dube",
    "x": 55,
    "y": 40,
    "outcome": "Sent off",
    "isKey": true
  },
  "recentEvents": [
    {
      "id": "evt_0056",
      "matchId": "m1",
      "minute": 57,
      "second": 31,
      "type": "Shot",
      "team": "HOME",
      "player": "J. Moyo",
      "x": 82,
      "y": 39,
      "outcome": "Saved",
      "xg": 0.14,
      "isKey": true
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 58,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 1,
      "shots": 5,
      "xg": 1.14,
      "possession": 55,
      "fouls": 6
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 0,
      "shots": 3,
      "xg": 0.38,
      "possession": 45,
      "fouls": 8
    },
    "momentum": {
      "home": 0.71,
      "away": 0.29
    }
  },
  "takes": [
    {
      "role": "Tactics",
      "text": "The sending-off occurs around midfield, with the recent timeline also showing a home shot shortly beforehand.",
      "why": "The red-card event is at x=55, y=40; evt_0056 records a home shot before it.",
      "evidenceEventIds": ["evt_0058", "evt_0056"],
      "confidence": 0.72
    },
    {
      "role": "Stats",
      "text": "The Lions lead 1-0 and have higher supplied shot, xG, and possession totals; the sample alone does not prove why.",
      "why": "Current state lists home/away shots as 5/3, xG as 1.14/0.38, and possession as 55/45.",
      "evidenceEventIds": ["evt_0058"],
      "confidence": 0.82
    },
    {
      "role": "Fan",
      "text": "A red card adds a major turning point to a match the Lions currently lead.",
      "why": "The away player is recorded as sent off while the score is 1-0.",
      "evidenceEventIds": ["evt_0058"],
      "confidence": 0.76
    }
  ]
}
```

## 6. `rtmi-video-referee` — four synthetic referee scenarios

For each test, attach the eight PNGs listed for that scenario in numeric filename order as image inputs, then paste its JSON context as text. The app's **Roll Random Scenario** button can also load, preview, and submit each sequence. Goalkeepers wear gold shirts to distinguish them from the lime home and blue away outfield players. These are schematic stick figures, not match footage or a real-world VAR benchmark.

The referee should use the frames to assess the incident, not treat a recorded event outcome as the review decision. The saved shots and claimed corner below do not establish a goal, offside, penalty, or foul. For the final scenario, the event feed records a goal as scored in play; review the images for possible offside rather than assuming the goal stands.

### 6.1 Away attack — saved shot

Attach [`away-attack/frame-01.png` through `frame-08.png`](./web/rtmi-web/public/samples/away-attack/). Blue Bulawayo Eagles attack right-to-left; the gold-shirted home goalkeeper saves the shot.

```json
{
  "persona": "Kid",
  "mode": "Referee",
  "frames": [
    { "index": 0 },
    { "index": 1 },
    { "index": 2 },
    { "index": 3 },
    { "index": 4 },
    { "index": 5 },
    { "index": 6 },
    { "index": 7 }
  ],
  "linkedEventId": "evt_0012",
  "recentEvents": [
    {
      "id": "evt_0012",
      "matchId": "m1",
      "minute": 12,
      "second": 30,
      "type": "Shot",
      "team": "AWAY",
      "player": "T. Ncube",
      "x": 78,
      "y": 44,
      "outcome": "Saved",
      "xg": 0.08,
      "isKey": true
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 12,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 0,
      "shots": 0,
      "xg": 0.0,
      "possession": 50,
      "fouls": 0
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 0,
      "shots": 1,
      "xg": 0.08,
      "possession": 50,
      "fouls": 0
    },
    "momentum": {
      "home": 0.0,
      "away": 1.0
    }
  }
}
```

### 6.2 Home counterattack — saved shot

Attach [`home-counterattack/frame-01.png` through `frame-08.png`](./web/rtmi-web/public/samples/home-counterattack/). Lime Harare Lions attack left-to-right; the gold-shirted away goalkeeper dives to block the shot.

```json
{
  "persona": "Kid",
  "mode": "Referee",
  "frames": [
    { "index": 0 }, { "index": 1 }, { "index": 2 }, { "index": 3 },
    { "index": 4 }, { "index": 5 }, { "index": 6 }, { "index": 7 }
  ],
  "linkedEventId": "evt_0021",
  "recentEvents": [
    {
      "id": "evt_0021",
      "matchId": "m1",
      "minute": 21,
      "second": 14,
      "type": "Shot",
      "team": "HOME",
      "player": "J. Moyo",
      "x": 82,
      "y": 40,
      "outcome": "Saved",
      "xg": 0.19,
      "isKey": true
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 21,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 0,
      "shots": 1,
      "xg": 0.19,
      "possession": 50,
      "fouls": 0
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 0,
      "shots": 0,
      "xg": 0.0,
      "possession": 50,
      "fouls": 0
    },
    "momentum": { "home": 1.0, "away": 0.0 }
  }
}
```

### 6.3 Away corner — goalkeeper claims the cross

Attach [`away-corner/frame-01.png` through `frame-08.png`](./web/rtmi-web/public/samples/away-corner/). The blue away side takes a corner toward the home goal, and the goalkeeper claims it.

```json
{
  "persona": "Kid",
  "mode": "Referee",
  "frames": [
    { "index": 0 }, { "index": 1 }, { "index": 2 }, { "index": 3 },
    { "index": 4 }, { "index": 5 }, { "index": 6 }, { "index": 7 }
  ],
  "linkedEventId": "evt_0028",
  "recentEvents": [
    {
      "id": "evt_0028",
      "matchId": "m1",
      "minute": 28,
      "second": 9,
      "type": "Corner",
      "team": "AWAY",
      "player": "M. Khumalo",
      "x": 94,
      "y": 8,
      "outcome": "Claimed by goalkeeper",
      "isKey": false
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 28,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 0,
      "shots": 0,
      "xg": 0.0,
      "possession": 50,
      "fouls": 0
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 0,
      "shots": 0,
      "xg": 0.0,
      "possession": 50,
      "fouls": 0
    },
    "momentum": { "home": 0.5, "away": 0.5 }
  }
}
```

### 6.4 Away offside goal — scored in play, under review

Attach [`away-offside-goal/frame-01.png` through `frame-08.png`](./web/rtmi-web/public/samples/away-offside-goal/). The sequence shows the attacker beyond the second-last defender when the pass is played, followed by the ball entering the goal. The feed's `Scored` outcome is the provisional on-field event, not the referee's review decision.

```json
{
  "persona": "Kid",
  "mode": "Referee",
  "frames": [
    { "index": 0 }, { "index": 1 }, { "index": 2 }, { "index": 3 },
    { "index": 4 }, { "index": 5 }, { "index": 6 }, { "index": 7 }
  ],
  "linkedEventId": "evt_0042",
  "recentEvents": [
    {
      "id": "evt_0042",
      "matchId": "m1",
      "minute": 42,
      "second": 16,
      "type": "Goal",
      "team": "AWAY",
      "player": "T. Ncube",
      "x": 86,
      "y": 42,
      "outcome": "Scored",
      "xg": 0.29,
      "isKey": true
    }
  ],
  "state": {
    "matchId": "m1",
    "minute": 42,
    "status": "Live",
    "home": {
      "name": "Harare Lions",
      "score": 0,
      "shots": 4,
      "xg": 0.72,
      "possession": 52,
      "fouls": 3
    },
    "away": {
      "name": "Bulawayo Eagles",
      "score": 1,
      "shots": 3,
      "xg": 0.81,
      "possession": 48,
      "fouls": 4
    },
    "momentum": { "home": 0.46, "away": 0.54 }
  }
}
```

### Optional sparse-evidence video-referee test

To test uncertainty, use synthetic or licensed frames that are too wide or obstructed to establish a call. The agent should choose `NoAction` with appropriately low confidence and empty `evidenceFrameIndexes` when no frame supports a listed decision. Do not add text telling the model what decision to make. A single static frame is not enough to establish a moving offside line or the full context of a foul.
