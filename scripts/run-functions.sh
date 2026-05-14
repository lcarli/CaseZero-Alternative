#!/usr/bin/env bash
# Run the CaseGen.Functions host locally on Mac/Linux.
# Idempotent: installs Azurite + Functions Core Tools if missing.
set -euo pipefail

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"
FUNC_DIR="$REPO_ROOT/functions/CaseGen.Functions"

log() { printf "\033[36m[run-functions]\033[0m %s\n" "$1"; }
warn() { printf "\033[33m[run-functions]\033[0m %s\n" "$1"; }
die() { printf "\033[31m[run-functions]\033[0m %s\n" "$1" >&2; exit 1; }

command -v dotnet >/dev/null || die "dotnet SDK not found — install .NET 9 SDK from https://dotnet.microsoft.com/download"
DOTNET_MAJOR=$(dotnet --list-sdks | awk -F. '{print $1}' | sort -nru | head -1)
if [ "${DOTNET_MAJOR:-0}" -lt 9 ]; then
  warn ".NET 9 SDK not detected (found $DOTNET_MAJOR). Functions will likely fail to build."
fi

command -v node >/dev/null || die "Node.js not found — install Node 20+ first (Azurite + Functions Core Tools need it)."

if ! command -v func >/dev/null; then
  log "Installing Azure Functions Core Tools v4 (npm -g)…"
  npm install -g azure-functions-core-tools@4 --unsafe-perm true
fi

if ! command -v azurite >/dev/null; then
  log "Installing Azurite (npm -g)…"
  npm install -g azurite
fi

AZURITE_DIR="$REPO_ROOT/AzuriteConfig"
mkdir -p "$AZURITE_DIR"

log "Starting Azurite in the background (logs: $AZURITE_DIR/azurite.log)…"
# Kill any prior azurite started by us.
if [ -f "$AZURITE_DIR/azurite.pid" ]; then
  OLD_PID="$(cat "$AZURITE_DIR/azurite.pid" 2>/dev/null || true)"
  if [ -n "$OLD_PID" ] && kill -0 "$OLD_PID" 2>/dev/null; then
    kill "$OLD_PID" || true
  fi
fi
nohup azurite --silent --location "$AZURITE_DIR" --debug "$AZURITE_DIR/azurite.log" >/dev/null 2>&1 &
echo $! > "$AZURITE_DIR/azurite.pid"
sleep 2
log "Azurite up (pid $(cat "$AZURITE_DIR/azurite.pid"))."

# Make sure the v2 schema is in the output before func start
cp "$REPO_ROOT/schemas/case.schema.json" "$FUNC_DIR/Schemas/case.v2.schema.json"

log "Restoring + building $FUNC_DIR…"
( cd "$FUNC_DIR" && dotnet build CaseGen.Functions.csproj --nologo >/dev/null )

log "Starting Functions host on http://localhost:7071 …"
cd "$FUNC_DIR"
exec func start --csharp
