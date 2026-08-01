#!/usr/bin/env bash

set -euo pipefail

readonly sdk_root="${ANDROID_SDK_ROOT:-$HOME/.local/share/android-sdk}"
readonly adb="$sdk_root/platform-tools/adb"
readonly emulator="$sdk_root/emulator/emulator"
readonly serial="emulator-5554"
readonly project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
readonly android_project="$project_dir/Game.Elevator.Android/Game.Elevator.Android.csproj"
readonly apk="$project_dir/Game.Elevator.Android/bin/Release/net10.0-android/android-x64/publish/game.vecxy.elevator-Signed.apk"
readonly activity="game.vecxy.elevator/crc640ad4a9b34917663b.MainActivity"

started_emulator=0

cleanup()
{
    if [[ "$started_emulator" == 1 ]]; then
        "$adb" -s "$serial" emu kill >/dev/null 2>&1 || true
    fi
}

trap cleanup EXIT INT TERM

if [[ ! -x "$adb" || ! -x "$emulator" ]]; then
    echo "Android SDK tools were not found in: $sdk_root" >&2
    exit 1
fi

if ! "$adb" -s "$serial" get-state >/dev/null 2>&1; then
    echo "Starting vecxy_api35 with a visible emulator window..."
    "$emulator" \
        -avd vecxy_api35 \
        -port 5554 \
        -gpu host \
        -feature -Vulkan \
        -no-snapshot \
        >"${TMPDIR:-/tmp}/vecxy-emulator.log" 2>&1 &
    started_emulator=1
fi

echo "Waiting for Android to finish booting..."
boot_completed=0
for _ in $(seq 1 120); do
    boot_state=$("$adb" -s "$serial" shell getprop sys.boot_completed 2>/dev/null || true)
    boot_state=${boot_state//$'\r'/}
    if [[ "$boot_state" == 1 ]]; then
        boot_completed=1
        break
    fi

    sleep 2
done

if [[ "$boot_completed" != 1 ]]; then
    echo "Android emulator did not boot within four minutes." >&2
    exit 1
fi

echo "Building the x86_64 emulator APK..."
dotnet publish "$android_project" \
    -c Release \
    -r android-x64 \
    --nologo \
    --tl:off \
    -v:minimal

echo "Installing Elevator..."
"$adb" -s "$serial" install -r "$apk"
"$adb" -s "$serial" logcat -c
"$adb" -s "$serial" shell am force-stop game.vecxy.elevator
"$adb" -s "$serial" shell am start -W -n "$activity"

pid=""
for _ in $(seq 1 40); do
    pid=$("$adb" -s "$serial" shell pidof game.vecxy.elevator 2>/dev/null || true)
    pid=${pid//$'\r'/}
    [[ -n "$pid" ]] && break
    sleep 0.25
done

if [[ -z "$pid" ]]; then
    echo "Elevator did not start. Recent Android log:" >&2
    "$adb" -s "$serial" logcat -d -v color | tail -200
    exit 1
fi

echo
echo "Elevator is running (PID $pid)."
echo "Use Android Back to open or close the map. Stop this configuration to close the emulator."
echo

"$adb" -s "$serial" logcat \
    --pid="$pid" \
    -v color \
    'DOTNET:V' \
    'Vecxy.Elevator:V' \
    'AndroidRuntime:E' \
    'libc:E' \
    '*:S'
