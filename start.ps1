param(
    [string]$TenantId
)

$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$webProject = Join-Path $repositoryRoot "web\rtmi-web"

function Assert-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found. Install it and try again."
    }
}

Assert-Command "az"
$dotnetCommand = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnetCommand) {
    throw "Required command 'dotnet' was not found. Install the .NET SDK and try again."
}
$dotnetPath = $dotnetCommand.Source
$npmCommand = Get-Command "npm.cmd" -ErrorAction SilentlyContinue
if (-not $npmCommand) {
    throw "Required command 'npm.cmd' was not found. Install Node.js and npm, then try again."
}
$npmPath = $npmCommand.Source

$currentTenant = az account show --only-show-errors --query tenantId --output tsv 2>$null
$accountShowExitCode = $LASTEXITCODE

$isSignedIn = $accountShowExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($currentTenant)
$needsLogin = -not $isSignedIn -or ($TenantId -and $currentTenant -ne $TenantId)

if ($needsLogin) {
    if ($TenantId) {
        az login --tenant $TenantId
    }
    else {
        az login
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Azure login failed."
    }
}
elseif ($TenantId) {
    Write-Host "Already signed in to the requested Azure tenant."
}
else {
    Write-Host "Reusing the current Azure CLI sign-in."
}

Write-Host "Restoring the .NET solution..."
& $dotnetPath restore (Join-Path $repositoryRoot "Rtmi.sln")
if ($LASTEXITCODE -ne 0) {
    throw ".NET restore failed."
}

$viteExecutable = Join-Path $webProject "node_modules\.bin\vite.cmd"
if (-not (Test-Path $viteExecutable)) {
    Write-Host "Installing React app dependencies..."
    Push-Location $webProject
    try {
        & $npmPath ci
        if ($LASTEXITCODE -ne 0) {
            throw "React app dependency installation failed."
        }
    }
    finally {
        Pop-Location
    }
}

$shell = (Get-Process -Id $PID).Path
$escapedRoot = $repositoryRoot.Replace("'", "''")
$escapedDotnetPath = $dotnetPath.Replace("'", "''")
$escapedNpmPath = $npmPath.Replace("'", "''")
$apiCommand = @"
Set-Location -LiteralPath '$escapedRoot'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
& '$escapedDotnetPath' run --project '.\src\Rtmi.Api\Rtmi.Api.csproj'
"@
$webCommand = @"
Set-Location -LiteralPath '$escapedRoot\web\rtmi-web'
& '$escapedNpmPath' run dev
"@

function Start-Terminal {
    param([string]$Command)

    $encodedCommand = [Convert]::ToBase64String(
        [Text.Encoding]::Unicode.GetBytes($Command)
    )
    Start-Process -FilePath $shell -ArgumentList @(
        "-NoExit",
        "-EncodedCommand",
        $encodedCommand
    )
}

Start-Terminal $apiCommand
Start-Terminal $webCommand

Write-Host "Started the API and React app in separate PowerShell windows."
Write-Host "Dashboard: http://localhost:5173"
Write-Host "API health: http://localhost:5080/api/health"
Write-Host "Close either terminal window to stop its process."
