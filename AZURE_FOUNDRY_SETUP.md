# Personal Azure + Microsoft Foundry Setup Guide

Everything runs in **your own personal Azure subscription**. No BDO tenant, licences, data or billing.
Total time: about 45 minutes (Part A + B before Phase 5, Part C before Phase 11).

---

## Part A — Personal Azure account (15 min)
1. Go to **azure.microsoft.com/free** and sign up with a **personal** Microsoft account (e.g. Outlook.com / Hotmail), not your BDO account.
2. Complete phone and card verification. A free account includes starter credit (currently US$200 for 30 days). You are not charged unless you upgrade to pay-as-you-go.
   - If the free offer isn't available in your country, create a pay-as-you-go subscription and rely on the budget alert below.
3. **Budget alert (do this first):** Azure portal → **Cost Management → Budgets → Add** → scope: your subscription → amount **US$20 / month** → alerts at 50%, 80% and 100% to your personal email.
4. Create a resource group: **rg-rtmi** in a region where `gpt-4.1-mini` is available (e.g. **East US 2** or **Sweden Central**). Keep every RTMI resource in this group.
5. Install locally: **Azure CLI** (`winget install Microsoft.AzureCLI`), then `az login` with the personal account and `az account show` to confirm the right subscription.

## Part B — Microsoft Foundry project + model (15 min)
1. Go to **ai.azure.com** (Microsoft Foundry) → sign in with the personal account → **Create project** → name `rtmi`, resource group `rg-rtmi`, same region.
2. **Models + endpoints → Deploy model → `gpt-4.1-mini`** → deployment type **Global Standard** → deployment name `gpt-4.1-mini` → tokens-per-minute around **50K** (plenty for the demo).
3. Copy the **project endpoint** from the Foundry project overview (format: `https://<resource-name>.services.ai.azure.com/api/projects/<project-name>`). Keep it in local user-secrets rather than tracked settings:
   ```powershell
   dotnet user-secrets init --project src\Rtmi.Api
   dotnet user-secrets set "Foundry:Endpoint" "https://<resource-name>.services.ai.azure.com/api/projects/<project-name>" --project src\Rtmi.Api
   dotnet user-secrets set "Foundry:UseFallbackOnly" "false" --project src\Rtmi.Api
   ```
4. **Give yourself data access:** Azure portal → the Foundry resource → **Access control (IAM) → Add role assignment → Cognitive Services OpenAI User** → assign to your own user. (Owner alone doesn't grant model data access.) Wait ~5 minutes.
5. **Smoke test** in the Foundry **Playground**: paste the Narrator prompt from spec §8 and the sample input below; check you get pure JSON back.
```json
{"persona":"Casual","event":{"id":"evt_0042","minute":37,"type":"Goal","team":"HOME","player":"J. Moyo"},"recentEvents":[{"id":"evt_0040","minute":35,"type":"Shot","team":"HOME","outcome":"Saved"}],"state":{"home":{"name":"Harare Lions","score":1},"away":{"name":"Bulawayo Eagles","score":0}}}
```
6. Authenticate locally with `az login`; the API uses `DefaultAzureCredential`. No API key is required or read by the application. To avoid model calls while building the UI, set `Foundry:UseFallbackOnly` to `true`.

**Checkpoint:** `az login` done, endpoint in config, Playground returns JSON.

## Part C — Hosting (15 min, before Phase 11)
### API — Azure App Service
1. Portal → **Create → Web App** → resource group `rg-rtmi` → name e.g. `rtmi-api-<initials>` → Publish: **Code** → Runtime **.NET 8 (LTS)** → OS **Linux** → plan **Basic B1**.
   - Why B1 (about US$13/month, paid from the credit): the Free F1 tier has a daily CPU quota and no *Always On*, so the background match simulator would stop.
2. **Configuration → General settings:** **Web sockets = On**, **Always on = On**, **SCM Basic Auth Publishing = On** (needed for the publish profile).
3. **Identity → System assigned = On.** Then on the **Foundry resource → IAM → Add role assignment → Cognitive Services OpenAI User →** assign to the web app's managed identity.
4. **Environment variables:** `Foundry__Endpoint`, `Foundry__Deployment=gpt-4.1-mini`, `Cors__Origins__0=https://<your-static-web-app>.azurestaticapps.net`, `ASPNETCORE_ENVIRONMENT=Production`.
5. **Overview → Download publish profile** → GitHub repo → Settings → Secrets → `AZURE_WEBAPP_PUBLISH_PROFILE`. Add repo variable `AZURE_WEBAPP_NAME`.

### Web — Azure Static Web Apps
1. Portal → **Create → Static Web App** → resource group `rg-rtmi` → plan **Free** → deployment source **Other** (the workflow comes from Phase 11).
2. **Manage deployment token** → copy → GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.
3. GitHub repo variable `API_URL=https://rtmi-api-<initials>.azurewebsites.net`.

**Checkpoint:** both resources exist; GitHub secrets and variables are set.

## Part D — Cost and clean-up
| Resource | Tier | Expected cost |
|---|---|---|
| Microsoft Foundry `gpt-4.1-mini` | Global Standard, pay per token | A few US$ for the whole hackathon (key events only, caching on) |
| Azure App Service | Basic B1 | ~US$13 per month, from the free credit |
| Azure Static Web Apps | Free | US$0 |
| GitHub + GitHub Copilot | Personal | Your existing plan |

Check actual prices in the Azure pricing calculator. Keep the budget alert on, and use `Foundry:UseFallbackOnly=true` while building UI to avoid model calls.
**Clean-up:** after judging closes (10 Nov 2026), delete the `rg-rtmi` resource group. That removes everything in one step.

## Troubleshooting
| Problem | Fix |
|---|---|
| 401 / 403 from Foundry | Missing **Cognitive Services OpenAI User** role (for you locally, or for the web app identity); wait 5–10 minutes after assigning |
| `DefaultAzureCredential` picks the wrong account | `az logout` then `az login` with the personal account; or set `AZURE_TENANT_ID` |
| 404 DeploymentNotFound | `Foundry:Deployment` must match the deployment **name**, not the model name |
| 429 Too Many Requests | Raise tokens-per-minute on the deployment, or increase the debounce |
| SignalR fails in Azure | Web sockets off on App Service, or CORS origin missing the Static Web App URL |
| Simulator stops after a while | App Service on F1 / Always On off — use B1 with Always On |
