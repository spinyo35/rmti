# RTMI — Hackathon Submission Details

## Tagline

> RTMI: A live football match control room with persona-based commentary, an AI analyst desk, and video-referee review—grounded in match evidence and powered by Microsoft Foundry agents.

## Keywords

Agentic AI, real-time analytics, Microsoft Foundry, explainable AI, multi-agent systems, personalization, SignalR, football, video review, match simulation

## Description

RTMI (Real-Time Match Intelligence) turns a deterministic football match simulation into a live control-room experience. A React dashboard receives match events over SignalR and presents persona-specific commentary, an analyst desk with tactics, statistics, and fan perspectives, and a synthesized verdict. Viewers can inspect the event evidence behind insights and submit synthetic match clips to the video-referee workflow.

The API invokes six portal-defined Microsoft Foundry agents for live commentary, desk analysis, and frame review. Responses are validated and grounded against the supplied match evidence; failures are explicitly labeled and use fallback output. The referee fallback does not analyze video. Four original referee scenarios cover two saved attacks, a claimed corner, and an offside goal scored in play.

The application is designed for local demonstration. Live Foundry responses require a configured Foundry project, the six agents, and Azure authentication; the simulator and dashboard can also run in fallback-only mode without Azure credentials.
