# RTMI — Real-Time Match Intelligence

RTMI is a local football match control room. A deterministic simulator streams match events to a React dashboard, where viewers can switch commentary personas, inspect an analyst desk, and submit synthetic video clips for referee review. Insights show the event evidence used to produce them.

The project runs locally; it does not currently include a hosted judge demo. Live AI responses require a Microsoft Foundry project and six manually created agents. The application can also be run in fallback-only mode without Azure credentials.

## What to review

- Match simulation, API, and SignalR updates: `src\Rtmi.Api`
- Dashboard: `web\rtmi-web`
- Foundry system instructions: [foundry-prompts](./foundry-prompts/README.md)
- Four referee test scenarios and sample requests: [foundry-sample-test-prompts.md](./foundry-sample-test-prompts.md)

## Run locally (fallback-only)

### Prerequisites

- .NET 8 SDK
- Node.js 20 or newer with npm
- PowerShell

Run these commands from the repository root.

### 1. Restore, build, and test

```powershell
dotnet restore .\Rtmi.sln
dotnet test .\Rtmi.sln --no-restore
dotnet build .\Rtmi.sln --no-restore
Push-Location .\web\rtmi-web
npm ci
npm run build
Pop-Location
```

### 2. Start the API

In the first PowerShell terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Foundry__UseFallbackOnly = "true"
dotnet run --project .\src\Rtmi.Api\Rtmi.Api.csproj
```

The environment variable makes this run fallback-only without changing any saved user secrets. The API listens on `http://localhost:5080`. In a second terminal, verify it:

```powershell
Invoke-RestMethod http://localhost:5080/api/health | ConvertTo-Json
```

`api` should say `Healthy`, `simulator` should say `NotStarted` before you launch a match, and `foundry` should say `FallbackOnly`. These are expected values.

### 3. Start the dashboard

In a second (or third) PowerShell terminal:

```powershell
Push-Location .\web\rtmi-web
npm run dev
```

Open the local URL printed by Vite, normally `http://localhost:5173`. Vite proxies API and SignalR traffic to port 5080.

### 4. Try the dashboard

1. Use **Demo Run** for the scripted match or **Start Match** for the regular simulation. The speed selector controls the simulation pace.
2. Switch the commentary persona to see how the same match is presented differently.
3. Open **Analyst Desk** to review the tactics, stats, and fan perspectives and the synthesized verdict.
4. Open **Review Room**, choose one of the four synthetic referee scenarios, and submit it for review. The set includes saved-shot and claimed-corner sequences plus an offside goal scored in play.
5. Open an insight’s **Why this?** evidence to inspect the cited match events.

In fallback-only mode, outputs are clearly labeled `Fallback`/`TEMPLATE`. The video-referee fallback does not analyze frames and returns `NoAction`; use the Foundry setup below for an actual model-backed frame review. A UI `Connected` indicator means the browser’s SignalR connection is up—it does not indicate Foundry connectivity.

## Optional: enable live Foundry agents

Live model calls require a Foundry project, the six agents configured from the versioned prompts, a project endpoint, and Azure CLI access with permission to invoke the agents. Follow [AZURE_FOUNDRY_SETUP.md](./AZURE_FOUNDRY_SETUP.md) and [foundry-prompts/README.md](./foundry-prompts/README.md).

For local Development, the API uses `AzureCliCredential`. After configuring the agents and access:

```powershell
az login
az account show
dotnet user-secrets set "Foundry:Endpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project .\src\Rtmi.Api\Rtmi.Api.csproj
dotnet user-secrets set "Foundry:UseFallbackOnly" "false" --project .\src\Rtmi.Api\Rtmi.Api.csproj
```

Stop the fallback API, then start it again without the `Foundry__UseFallbackOnly` environment variable:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
Remove-Item Env:Foundry__UseFallbackOnly -ErrorAction SilentlyContinue
dotnet run --project .\src\Rtmi.Api\Rtmi.Api.csproj
```

Check `/api/health`: `foundry: "Configured"` only confirms that an endpoint is set and fallback-only mode is off; it does **not** verify authentication, agent availability, or a successful model call. Confirm live results using the output source labels in the dashboard. The default request timeout is 45 seconds. Do not put credentials or keys in tracked files.

For direct agent testing, use the prompts and sample requests in [foundry-sample-test-prompts.md](./foundry-sample-test-prompts.md). For the video referee, run all four scenarios and inspect its cited frame evidence; the scenario label alone is not proof that a model reached the expected verdict.
