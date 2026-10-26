# Optional Microsoft Foundry setup

The dashboard and match simulator can be evaluated locally without Azure in fallback-only mode. This guide is only needed to run the six live AI agents. No Foundry resource or hosted web deployment is created by the repository.

## 1. Prepare a Foundry project

Use a Microsoft Foundry project with a model deployment that supports the project Responses API. Create these six agents in the project, using the exact names and versioned system instructions below:

| Agent name | Instructions |
|---|---|
| `rtmi-narrator` | [rtmi-narrator.md](./foundry-prompts/rtmi-narrator.md) |
| `rtmi-tactics` | [rtmi-tactics.md](./foundry-prompts/rtmi-tactics.md) |
| `rtmi-stats` | [rtmi-stats.md](./foundry-prompts/rtmi-stats.md) |
| `rtmi-fan` | [rtmi-fan.md](./foundry-prompts/rtmi-fan.md) |
| `rtmi-verdict` | [rtmi-verdict.md](./foundry-prompts/rtmi-verdict.md) |
| `rtmi-video-referee` | [rtmi-video-referee.md](./foundry-prompts/rtmi-video-referee.md) |

The agents must be available through the project endpoint. The model deployment is selected/configured in Foundry; the API addresses agents by name. Use the project's endpoint, in this form:

```text
https://<resource>.services.ai.azure.com/api/projects/<project>
```

## 2. Grant access and configure local credentials

Grant the Azure identity you will use a role with permission to invoke agents in the Foundry project. Then sign in with Azure CLI and verify that the intended subscription/account is active:

```powershell
az login
az account show
```

The API project already has a `UserSecretsId`; do not run `dotnet user-secrets init`. From the repository root, save the project endpoint and enable live calls in user-secrets:

```powershell
dotnet user-secrets set "Foundry:Endpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project .\src\Rtmi.Api\Rtmi.Api.csproj
dotnet user-secrets set "Foundry:UseFallbackOnly" "false" --project .\src\Rtmi.Api\Rtmi.Api.csproj
```

In Development, the API uses `AzureCliCredential`, so run `az login` as the same user before starting it. Do not place keys or credentials in appsettings files or commit them to the repository. The endpoint is not a credential, but user-secrets keeps local configuration out of tracked settings.

## 3. Start and verify

If the API is already running in fallback-only mode, stop it. Start a new PowerShell session from the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
Remove-Item Env:Foundry__UseFallbackOnly -ErrorAction SilentlyContinue
dotnet run --project .\src\Rtmi.Api\Rtmi.Api.csproj
```

Check `http://localhost:5080/api/health`. A `foundry` status of `Configured` means only that an endpoint is present and fallback-only mode is disabled. It does not prove authentication, agent availability, or successful inference. Test the features in the dashboard and check that each result is labeled as Foundry-backed. Failed, timed-out, malformed, or invalidly grounded agent responses are logged and use explicitly labeled fallback output. The default Foundry request timeout is 45 seconds.

Use [foundry-sample-test-prompts.md](./foundry-sample-test-prompts.md) for direct agent test cases. The video-referee fallback does not inspect frames and returns `NoAction`; only a live Foundry response represents frame analysis.
