#!/usr/bin/env bash
set -euo pipefail
if [ -z "${VALHEIM_DIR:-}" ]; then
  echo "Set VALHEIM_DIR to your Valheim install folder." >&2
  exit 1
fi
dotnet build "$(dirname "$0")/../SkadiNet.csproj" -c Release
