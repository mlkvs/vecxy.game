#!/usr/bin/env bash
# Build signed Android packages: an App Bundle for Google Play and an APK for testers.
# Secrets are intentionally read only from environment variables, never CLI arguments.
set -Eeuo pipefail

readonly ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly PROJECT="$ROOT_DIR/HardCore.Cultivation/HardCore.Cultivation.csproj"

usage() {
    cat <<'EOF'
Usage:
  ./scripts/build-android.sh <dev|release> --build <number> [options]

Options:
  -b, --build NUMBER       Google Play version code (required, positive integer).
  -v, --version VERSION    Display version. Default: version in the game project.
  -k, --keystore PATH      Keystore for a signed bundle. Required for release.
  -a, --alias NAME         Keystore alias. Required for release.
  -o, --output PATH        Output directory. Default: artifacts/android/<mode>.
  -h, --help               Show this help.

Environment for signed builds:
  ANDROID_KEYSTORE_PASSWORD  Keystore password (required for release).
  ANDROID_KEY_PASSWORD       Key password. Defaults to ANDROID_KEYSTORE_PASSWORD.
  APPMETRICA_API_KEY         Optional AppMetrica application API key.

Examples:
  ./scripts/build-android.sh dev --build 45
  ANDROID_KEYSTORE_PASSWORD='...' ANDROID_KEY_PASSWORD='...' \
    ./scripts/build-android.sh release --build 45 \
      --keystore ~/.keys/google-play.jks --alias google-play
EOF
}

fail() { printf 'Error: %s\n' "$*" >&2; exit 1; }

[[ $# -gt 0 ]] || { usage; exit 1; }
if [[ "$1" == -h || "$1" == --help ]]; then
    usage
    exit 0
fi
mode="$1"
shift

case "$mode" in
    dev) configuration=Debug; flavor=Dev ;;
    release) configuration=Release; flavor=Release ;;
    *) fail "mode must be dev or release" ;;
esac

build_number=""
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1)"
keystore="${ANDROID_KEYSTORE_PATH:-}"
alias_name="${ANDROID_KEY_ALIAS:-}"
output_dir=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        -b|--build) build_number="${2:-}"; shift 2 ;;
        -v|--version) version="${2:-}"; shift 2 ;;
        -k|--keystore) keystore="${2:-}"; shift 2 ;;
        -a|--alias) alias_name="${2:-}"; shift 2 ;;
        -o|--output) output_dir="${2:-}"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) fail "unknown option: $1" ;;
    esac
done

[[ "$build_number" =~ ^[1-9][0-9]*$ ]] || fail "--build must be a positive integer"
[[ -n "$version" ]] || fail "could not determine version; provide --version"
output_dir="${output_dir:-$ROOT_DIR/artifacts/android/$mode}"
temp_dir="$output_dir/.tmp"

signing_args=()
if [[ "$mode" == release || -n "$keystore" || -n "$alias_name" ]]; then
    [[ -n "$keystore" && -f "$keystore" ]] || fail "a readable --keystore is required"
    [[ -n "$alias_name" ]] || fail "--alias is required for a signed build"
    [[ -n "${ANDROID_KEYSTORE_PASSWORD:-}" ]] || fail "ANDROID_KEYSTORE_PASSWORD is required"
    keystore="$(cd "$(dirname "$keystore")" && pwd)/$(basename "$keystore")"
    key_password="${ANDROID_KEY_PASSWORD:-$ANDROID_KEYSTORE_PASSWORD}"
    signing_args=(
        -p:AndroidKeyStore=True
        "-p:AndroidSigningKeyStore=$keystore"
        "-p:AndroidSigningKeyAlias=$alias_name"
        "-p:AndroidSigningStorePass=$ANDROID_KEYSTORE_PASSWORD"
        "-p:AndroidSigningKeyPass=$key_password"
    )
fi

analytics_args=()
if [[ -n "${APPMETRICA_API_KEY:-}" ]]; then
    analytics_args=("-p:AppMetricaApiKey=$APPMETRICA_API_KEY")
fi

rm -rf "$output_dir"
mkdir -p "$output_dir"
mkdir -p "$temp_dir"

publish_dir="$output_dir/.publish"
package_dir="$ROOT_DIR/HardCore.Cultivation/bin/$configuration/net10.0-android/android-arm64"

TMPDIR="$temp_dir" dotnet publish "$PROJECT" \
    --configuration "$configuration" \
    --framework net10.0-android \
    --runtime android-arm64 \
    --output "$publish_dir" \
    -p:VecxyPlatform=Android \
    -p:GameBuildFlavor="$flavor" \
    -p:AndroidPackageFormat=aab \
    '-p:AndroidPackageFormats=aab%3Bapk' \
    "-p:JavaOptions=-Djava.io.tmpdir=$temp_dir" \
    -p:ApplicationVersion="$build_number" \
    -p:ApplicationDisplayVersion="$version" \
    "${analytics_args[@]}" \
    "${signing_args[@]}"

bundle="$(find "$package_dir" -maxdepth 1 -type f -name '*-Signed.aab' -print -quit)"
bundle="${bundle:-$(find "$package_dir" -maxdepth 1 -type f -name '*.aab' -print -quit)}"
apk="$(find "$package_dir" -maxdepth 1 -type f -name '*-Signed.apk' -print -quit)"
apk="${apk:-$(find "$package_dir" -maxdepth 1 -type f -name '*.apk' -print -quit)}"
[[ -n "$bundle" ]] || fail "publish completed without an .aab file"
[[ -n "$apk" ]] || fail "publish completed without an .apk file"
bundle_artifact="$output_dir/$(basename "$bundle")"
apk_artifact="$output_dir/$(basename "$apk")"
cp "$bundle" "$bundle_artifact"
cp "$apk" "$apk_artifact"
rm -rf "$publish_dir"
rm -rf "$temp_dir"

printf 'Google Play bundle: %s\nTest APK: %s\n' "$bundle_artifact" "$apk_artifact"
