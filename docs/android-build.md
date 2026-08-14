# Android App Bundle

Build settings, signing credentials and analytics are stored in repository YAML files:

- `Build.yaml` defines game name, version, Android icon, Google Play version code, bundle version and keystore credentials;
- `Analytics.yaml` contains the AppMetrica API key.

Use one script for both build flavours:

```bash
./scripts/build-android.sh dev
```

`dev` builds a Debug `.aab` with `GAME_DEV_BUILD`. To make a signed development bundle, also provide `--keystore` and `--alias`.

Google Play release build:

```bash
./scripts/build-android.sh release
```

`release` builds with `GAME_RELEASE_BUILD`. `--build` overrides the Google Play version code and must increase for every upload. `--version` overrides the bundle version. Each invocation produces both formats in `artifacts/android/<mode>` unless `--output` is set:

- `.aab` is the signed package for Google Play;
- `.apk` is the signed package for testers.

The AppMetrica key is embedded in the Android application and is required by the SDK for activation. Leaving `appmetrica.api_key` empty builds the application without analytics activation.

Generated artifacts are ignored by Git. `Build.yaml`, `Analytics.yaml` and the keystore are intentionally repository files; restrict repository access accordingly.
