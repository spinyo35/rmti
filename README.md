# RTMI — Real-Time Match Intelligence

RTMI is a live football match control room: a deterministic match simulator streams events to a React dashboard, with explainable persona-specific commentary, analyst desk perspectives, and a video-referee review panel.

## Local development

Requirements: .NET 8 SDK, Node.js 20 or newer, and npm.

Start the API from the repository root:

```powershell
dotnet run --project src\Rtmi.Api
```

In another terminal, install the web dependencies once and start Vite:

```powershell
cd web\rtmi-web
npm install
npm run dev
```

Open the URL printed by Vite (normally `http://localhost:5173`). The dev server proxies `/api` and `/hubs` to the API at `http://localhost:5080`.

Use **Start Match** for a normal run, **Demo Run** for the scripted scenario, and the speed selector to change simulation pace. Switch personas to change commentary. Click an insight's **Why this?** row to inspect its cited event receipts.

## Foundry agents

The versioned portal instructions are in [foundry-prompts](./foundry-prompts/README.md). Create the agents manually in Microsoft Foundry before enabling live invocation. Until a configured endpoint and credentials are available, the API uses explicitly labeled `Fallback` output; video fallback does not claim to have analyzed frames.

The API uses `Azure.AI.Projects` with the project Responses API and the six agent names in `appsettings.json`. In Development it uses `AzureCliCredential` directly; in other environments it uses `DefaultAzureCredential` (including managed identity when hosted). The Foundry request timeout defaults to 45 seconds and can be overridden with `Foundry:TimeoutSeconds`. For local model-backed runs:

```powershell
az login
dotnet user-secrets init --project src\Rtmi.Api
dotnet user-secrets set "Foundry:Endpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project src\Rtmi.Api
dotnet user-secrets set "Foundry:UseFallbackOnly" "false" --project src\Rtmi.Api
dotnet run --project src\Rtmi.Api
```

Use the project endpoint shown in Foundry and grant the signed-in identity the required Foundry/OpenAI data-plane role. Run `az login` before starting the API locally; do not put API keys or credentials in the repository. Set `Foundry:UseFallbackOnly=true` to test without model calls. Failed, timed-out, malformed, or ungrounded agent responses are logged and replaced with explicitly tagged `Fallback` output. The referee fallback does not inspect frames and returns `NoAction`, rather than implying a real review took place.

For Foundry setup and deployment prerequisites, see [AZURE_FOUNDRY_SETUP.md](./AZURE_FOUNDRY_SETUP.md).

## Verification

```powershell
dotnet build Rtmi.sln --nologo
dotnet test Rtmi.sln --nologo
cd web\rtmi-web
npm run build
```
