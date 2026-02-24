#!/usr/bin/env bash
# run.sh - Run a workshop module by number or name
#
# Usage:
#   ./scripts/run.sh 00              # runs 00_ConnectivityCheck
#   ./scripts/run.sh 02              # runs 02_Tools_FunctionCalling
#   ./scripts/run.sh 00_ConnectivityCheck  # also works

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MODULES_DIR="$REPO_ROOT/modules"

if [ -z "${1:-}" ]; then
  echo "Usage: $0 <module-number-or-name>"
  echo ""
  echo "Available modules:"
  for d in "$MODULES_DIR"/*/; do
    echo "  $(basename "$d")"
  done
  exit 1
fi

MODULE_ARG="$1"

# Find the matching module directory
MATCH=""
for d in "$MODULES_DIR"/*/; do
  name="$(basename "$d")"
  prefix="${name%%_*}"
  if [ "$name" = "$MODULE_ARG" ] || [ "$prefix" = "$MODULE_ARG" ]; then
    MATCH="$d"
    break
  fi
done

if [ -z "$MATCH" ]; then
  echo "[ERROR] No module found matching: $MODULE_ARG"
  echo ""
  echo "Available modules:"
  for d in "$MODULES_DIR"/*/; do
    echo "  $(basename "$d")"
  done
  exit 1
fi

MOD_NAME="$(basename "$MATCH")"
CSPROJ="$MATCH$MOD_NAME.csproj"

echo ">> Running module: $MOD_NAME"
echo "   Project: $CSPROJ"
echo "-------------------------------------------"

cd "$REPO_ROOT"
dotnet run --project "$CSPROJ" "${@:2}"
