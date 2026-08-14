# Android App Bundle

Build settings, signing credentials and analytics are stored in repository YAML files:

- `HardCore.Cultivation/Assets/Configs/Build.yaml` defines game name, version, Android icon, Google Play version code, bundle version and keystore credentials;
- `HardCore.Cultivation/Assets/Configs/Analytics.yaml` contains the AppMetrica API key.

Use the root build command for both build flavours:

```bash
./build dev
```

`dev` builds a Debug `.aab` with `GAME_DEV_BUILD`. To make a signed development bundle, also provide `--keystore` and `--alias`.

Google Play release build:

```bash
./build release
```

`release` builds with `GAME_RELEASE_BUILD`. `--build` overrides the Google Play version code and must increase for every upload. `--version` overrides the bundle version. `Build.yaml` selects the target platform and supports comma-separated `definesCommon` plus `definesAndroid` or `definesDesktop`. Android builds produce both formats in `artifacts/android/<mode>` unless `--output` is set.

Set `build.platform` to `desktop` and choose `desktop.runtimeIdentifier` (for example `linux-x64`, `win-x64`, or `osx-arm64`) to publish a self-contained desktop build in `artifacts/desktop/<mode>`. The desktop package includes the YAML files, so its in-game settings use the same version fields as an IDE launch.

- `.aab` is the signed package for Google Play;
- `.apk` is the signed package for testers.

The AppMetrica key is embedded in the Android application and is required by the SDK for activation. Leaving `appmetrica.apiKey` empty builds the application without analytics activation.

Generated artifacts are ignored by Git. `Build.yaml`, `Analytics.yaml` and the keystore are intentionally repository files; restrict repository access accordingly.
