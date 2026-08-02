#!/usr/bin/env bash

set -euo pipefail

readonly action="${1:-build}"
readonly architecture="${2:-arm64}"
readonly project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly project="$project_dir/HardCore.Cultivation.Android.csproj"
readonly sdk_root="${ANDROID_SDK_ROOT:-$HOME/.local/share/android-sdk}"
readonly adb="$sdk_root/platform-tools/adb"
readonly package="game.vecxy.hardcorecultivation"

case "$architecture" in
  arm64) readonly runtime="android-arm64" ;;
  x64|emulator) readonly runtime="android-x64" ;;
  *) echo "Architecture must be arm64, x64, or emulator." >&2; exit 2 ;;
esac

readonly output="$project_dir/bin/Release/net10.0-android/$runtime/publish"
readonly apk="$output/$package-Signed.apk"

build()
{
  dotnet publish "$project" -c Release -r "$runtime" --nologo --tl:off -v:minimal -m:1
  test -f "$apk"
  echo "APK: $apk"
}

case "$action" in
  build)
    build
    ;;
  install|run)
    build
    test -x "$adb"
    "$adb" install -r "$apk"
    if [[ "$action" == "run" ]]; then
      "$adb" shell am force-stop "$package"
      "$adb" shell monkey -p "$package" -c android.intent.category.LAUNCHER 1 >/dev/null
      echo "HardCore Cultivation started on the connected Android device."
    fi
    ;;
  logs)
    test -x "$adb"
    "$adb" logcat 'Vecxy.HardCore:V' 'DOTNET:V' 'AndroidRuntime:E' '*:S'
    ;;
  *)
    echo "Usage: $0 {build|install|run|logs} [arm64|x64|emulator]" >&2
    exit 2
    ;;
esac
