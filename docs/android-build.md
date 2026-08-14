# Android App Bundle

Use one script for both build flavours:

```bash
./scripts/build-android.sh dev --build 45
```

`dev` builds a Debug `.aab` with `GAME_DEV_BUILD`. To make a signed development bundle, also provide `--keystore` and `--alias`.

Google Play release builds require a keystore and read all passwords from environment variables:

```bash
ANDROID_KEYSTORE_PASSWORD='...' ANDROID_KEY_PASSWORD='...' \
  ./scripts/build-android.sh release --build 45 \
    --keystore ~/.keys/google-play.jks --alias google-play
```

`release` builds with `GAME_RELEASE_BUILD`. `--build` is the Google Play version code and must increase for every upload. `--version` changes the display version; it defaults to the version declared in `HardCore.Cultivation.csproj`. Bundles are written to `artifacts/android/<mode>` unless `--output` is set.

Keystores and generated artifacts are ignored by Git. Do not put passwords into shell history, scripts, or project files.
