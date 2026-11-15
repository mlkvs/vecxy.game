#!/usr/bin/env bash
set -euo pipefail
DNR_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
DNR_OUTPUT="$DNR_ROOT/Build/Linux-x64"
[[ -x "$DNR_OUTPUT/DoNotRelease" ]] || { echo 'Сначала соберите игру: ./DoNotRelease/build.sh' >&2; exit 1; }
cp "$DNR_ROOT/README.md" "$DNR_OUTPUT/README.md"
cp "$DNR_ROOT/Docs/TEST_REPORT.md" "$DNR_OUTPUT/TEST_REPORT.md"
cp "$DNR_ROOT/Docs/CREDITS.md" "$DNR_OUTPUT/CREDITS.md"
mkdir -p "$DNR_OUTPUT/ThirdParty"
cp "$DNR_ROOT/Assets/Fonts/NotoSans.OFL.txt" "$DNR_OUTPUT/ThirdParty/"
cp "$DNR_ROOT/../Engine/Vecxy/FMODEngine/doc/LICENSE.TXT" "$DNR_OUTPUT/ThirdParty/FMOD-LICENSE.txt"
cp "$DNR_ROOT/Assets/Audio/Voice/denis.MODEL_CARD.txt" "$DNR_ROOT/Assets/Audio/Voice/dmitri.MODEL_CARD.txt" "$DNR_OUTPUT/ThirdParty/"
cat > "$DNR_OUTPUT/start.sh" <<'LAUNCH'
#!/usr/bin/env bash
set -euo pipefail
DNR_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
if [[ -n "${DISPLAY:-}" ]]; then
    export SDL_VIDEODRIVER=x11 XDG_SESSION_TYPE=x11
    unset WAYLAND_DISPLAY
fi
exec "$DNR_DIR/DoNotRelease" "$@"
LAUNCH
chmod +x "$DNR_OUTPUT/start.sh"
tar -C "$DNR_OUTPUT" -czf "$DNR_ROOT/Build/NeVypuskat-linux-x64.tar.gz" .
(cd "$DNR_ROOT/Build" && sha256sum NeVypuskat-linux-x64.tar.gz > NeVypuskat-linux-x64.tar.gz.sha256)
echo "Архив: $DNR_ROOT/Build/NeVypuskat-linux-x64.tar.gz"
