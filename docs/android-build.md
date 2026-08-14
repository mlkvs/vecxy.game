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

`release` builds with `GAME_RELEASE_BUILD`. `--build` is the Google Play version code and must increase for every upload. `--version` changes the display version; it defaults to the version declared in `HardCore.Cultivation.csproj`. Each invocation produces both formats in `artifacts/android/<mode>` unless `--output` is set:

- `.aab` is the signed package for Google Play;
- `.apk` is the signed package for testers.

To enable AppMetrica analytics, pass the application API key from AppMetrica as an environment variable:

```bash
APPMETRICA_API_KEY='your-appmetrica-key' \
  ./scripts/build-android.sh release --build 45 \
    --keystore ~/.keys/google-play.jks --alias google-play
```

The key is embedded in the Android application and is required by the AppMetrica SDK for activation. Leaving it unset builds the application without analytics activation.

Keystores and generated artifacts are ignored by Git. Do not put passwords into shell history, scripts, or project files.
