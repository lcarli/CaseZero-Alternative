# Run the CaseGen.Functions host locally on Windows.
# Idempotent: installs Azurite + Functions Core Tools if missing.
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot = Resolve-Path (Join-Path $ScriptDir "..")
$FuncDir = Join-Path $RepoRoot "functions/CaseGen.Functions"

function Log($msg)  { Write-Host "[run-functions] $msg" -ForegroundColor Cyan }
function Warn($msg) { Write-Host "[run-functions] $msg" -ForegroundColor Yellow }
function Die($msg)  { Write-Host "[run-functions] $msg" -ForegroundColor Red; exit 1 }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  Die ".NET SDK not found — install .NET 9 SDK from https://dotnet.microsoft.com/download"
}
$majors = (dotnet --list-sdks | ForEach-Object { ($_ -split '\.')[0] } | Sort-Object -Unique -Descending)
if (-not $majors -or [int]$majors[0] -lt 9) {
  Warn ".NET 9 SDK not detected — Functions will likely fail to build."
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
  Die "Node.js not found — install Node 20+ first (Azurite + Functions Core Tools need it)."
}

if (-not (Get-Command func -ErrorAction SilentlyContinue)) {
  Log "Installing Azure Functions Core Tools v4 (npm -g)…"
  npm install -g azure-functions-core-tools@4 --unsafe-perm true
}

if (-not (Get-Command azurite -ErrorAction SilentlyContinue)) {
  Log "Installing Azurite (npm -g)…"
  npm install -g azurite
}

$AzuriteDir = Join-Path $RepoRoot "AzuriteConfig"
New-Item -ItemType Directory -Force -Path $AzuriteDir | Out-Null

$pidFile = Join-Path $AzuriteDir "azurite.pid"
if (Test-Path $pidFile) {
  $oldPid = Get-Content $pidFile -ErrorAction SilentlyContinue
  if ($oldPid -and (Get-Process -Id $oldPid -ErrorAction SilentlyContinue)) {
    Stop-Process -Id $oldPid -Force -ErrorAction SilentlyContinue
  }
}

Log "Starting Azurite in the background (logs: $AzuriteDir/azurite.log)…"
$azuriteProc = Start-Process azurite -ArgumentList "--silent","--location","$AzuriteDir","--debug","$AzuriteDir/azurite.log" -PassThru -WindowStyle Hidden
$azuriteProc.Id | Out-File -FilePath $pidFile -Encoding ascii
Start-Sleep -Seconds 2
Log ("Azurite up (pid {0})." -f $azuriteProc.Id)

# Make sure the v2 schema is in the output before func start
Copy-Item (Join-Path $RepoRoot "schemas/case.schema.json") (Join-Path $FuncDir "Schemas/case.v2.schema.json") -Force

Log "Restoring + building $FuncDir…"
Push-Location $FuncDir
dotnet build CaseGen.Functions.csproj --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Die "dotnet build failed" }

Log "Starting Functions host on http://localhost:7071 …"
func start --csharp
Pop-Location
