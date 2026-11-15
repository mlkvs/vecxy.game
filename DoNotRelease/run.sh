#!/usr/bin/env bash
set -euo pipefail
DNR_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
if [[ -n "${DISPLAY:-}" ]]; then
    export SDL_VIDEODRIVER=x11 XDG_SESSION_TYPE=x11
    unset WAYLAND_DISPLAY
fi
if [[ -x "$DNR_ROOT/Build/Linux-x64/DoNotRelease" ]]; then
    exec "$DNR_ROOT/Build/Linux-x64/DoNotRelease" "$@"
fi
DNR_SDK="$DNR_ROOT/../.agents/local/dotnet/dotnet"
if [[ -x "$DNR_SDK" ]]; then
    export PATH="$(dirname "$DNR_SDK"):$PATH"
fi
exec dotnet run --project "$DNR_ROOT/DoNotRelease.csproj" "$@"
