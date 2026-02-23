#!/usr/bin/env bash
# set-env.sh – Set Azure OpenAI environment variables for the workshop

set -euo pipefail

echo "============================================="
echo " Azure Agent Framework Workshop – Set Env"
echo "============================================="
echo ""

REQUIRED_VARS=(
  "AZURE_OPENAI_ENDPOINT"
  "AZURE_OPENAI_API_KEY"
  "AZURE_OPENAI_DEPLOYMENT"
)

OPTIONAL_VARS=(
  "AZURE_OPENAI_API_VERSION"
)

MISSING=()

for var in "${REQUIRED_VARS[@]}"; do
  if [ -z "${!var:-}" ]; then
    MISSING+=("$var")
  fi
done

if [ ${#MISSING[@]} -eq 0 ]; then
  echo "✅ All required environment variables are set."
  echo ""
  echo "Current values:"
  echo "  AZURE_OPENAI_ENDPOINT    = $AZURE_OPENAI_ENDPOINT"
  echo "  AZURE_OPENAI_API_KEY     = (set, hidden)"
  echo "  AZURE_OPENAI_DEPLOYMENT  = $AZURE_OPENAI_DEPLOYMENT"
  if [ -n "${AZURE_OPENAI_API_VERSION:-}" ]; then
    echo "  AZURE_OPENAI_API_VERSION = $AZURE_OPENAI_API_VERSION"
  fi
  exit 0
fi

echo "❌ Missing required environment variables:"
for var in "${MISSING[@]}"; do
  echo "   - $var"
done
echo ""
echo "Please set them before running any module:"
echo ""
echo "  export AZURE_OPENAI_ENDPOINT='https://<your-resource>.openai.azure.com/'"
echo "  export AZURE_OPENAI_API_KEY='<your-api-key>'"
echo "  export AZURE_OPENAI_DEPLOYMENT='<your-deployment-name>'"
echo "  export AZURE_OPENAI_API_VERSION='2025-01-01-preview'  # optional"
echo ""
echo "Tip: Add these exports to ~/.bashrc or ~/.zshrc for persistence."
exit 1
