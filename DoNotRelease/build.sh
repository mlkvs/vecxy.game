#!/usr/bin/env bash
set -euo pipefail
DNR_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
DNR_SDK="$DNR_ROOT/../.agents/local/dotnet/dotnet"
if [[ -x "$DNR_SDK" ]]; then export PATH="$(dirname "$DNR_SDK"):$PATH"; fi
cd "$DNR_ROOT/.."
DNR_USD_PYTHON="${VECXY_USD_PYTHON:-$DNR_ROOT/../.agents/local/usd-env/bin/python}"
if [[ ! -x "$DNR_USD_PYTHON" ]]; then DNR_USD_PYTHON=python3; fi
"$DNR_USD_PYTHON" Engine/Vecxy/Code/Vecxy.Usd/Tools/cook_usd.py \
  "$DNR_ROOT/Assets/Scenes/Cell.usda" --assets "$DNR_ROOT/Assets"
dotnet run --project Engine/Vecxy/tools/Vecxy.Cli -- --project "$DNR_ROOT" build release --platform linux --output "$DNR_ROOT/Build/Linux-x64"

"$DNR_ROOT/Tools/package.sh"
