#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(cd -- "$project_root/.." && pwd)"
usd_python="${VECXY_USD_PYTHON:-python3}"
if [[ -z "${VECXY_USD_PYTHON:-}" && -x "$repo_root/.agents/local/usd-env/bin/python" ]]; then
    usd_python="$repo_root/.agents/local/usd-env/bin/python"
fi
exec "$usd_python" "$repo_root/Engine/Vecxy/Code/Vecxy.Usd/Tools/cook_usd.py" \
    "$project_root/Assets/Scenes/Sandbox.usda" --assets "$project_root/Assets" "$@"
