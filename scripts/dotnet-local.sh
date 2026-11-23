#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_CLI_HOME="$PROJECT_ROOT/.local/dotnet-home"
export NUGET_PACKAGES="$PROJECT_ROOT/.local/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
if [ -x "$PROJECT_ROOT/.tools/dotnet/dotnet" ]; then
  export DOTNET_ROOT="$PROJECT_ROOT/.tools/dotnet"
  exec "$DOTNET_ROOT/dotnet" "$@"
fi
exec dotnet "$@"
