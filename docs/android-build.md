# Android App Bundle

Build settings, signing credentials and analytics are stored in repository YAML files:

- `HardCore.Cultivation/Assets/Configs/Build.yaml` defines game name, application version, internal build version, Android icon, Google Play version code and keystore credentials;
- `HardCore.Cultivation/Assets/Configs/Analytics.yaml` contains the AppMetrica API key.

Use the root build command for both build flavours:

```bash
./build.sh dev
```

On Windows use the Git Bash-backed wrapper; WSL is not required:

```powershell
.\build.cmd dev
.\build.cmd release
```

`dev` builds a Debug `.aab` with `GAME_DEV_BUILD`. To make a signed development bundle, also provide `--keystore` and `--alias`.

Google Play release build:

```bash
./build.sh release
```

Перед Android publish автоматически выполняется Vecxy Asset Pipeline:

```text
scan -> generate -> analyze -> validate -> publish
```

Он обновляет game manifest вместе с ассетами `Vecxy.Engine`, генерирует typed handles,
проверяет missing assets и добавляет `Assets.manifest` в APK/AAB. Глобальная установка
CLI не нужна: MSBuild запускает `Engine/Vecxy/tools/Vecxy.Cli` из submodule.

`release` builds with `GAME_RELEASE_BUILD`. `game.version` is used both in the game and as the Android/Google Play version name. `--build` overrides the Google Play version code and must increase for every upload. `--version` overrides the application version. `--build-version` overrides the internal build number. After a successful desktop or Android build, the build command writes the next internal build number back to `build.buildVersion`; it can also be edited manually. `Build.yaml` selects the target platform and supports comma-separated `definesCommon` plus `definesAndroid` or `definesDesktop`. Android builds produce both formats in `artifacts/android/<mode>` unless `--output` is set.

Set `build.platform` to `desktop` and choose `desktop.runtimeIdentifier` (for example `linux-x64`, `win-x64`, or `osx-arm64`) to publish a self-contained desktop build in `artifacts/desktop/<mode>`. The desktop package includes the YAML files, so its in-game settings use the same version fields as an IDE launch.

- `.aab` is the signed package for Google Play;
- `.apk` is the signed package for testers.

The AppMetrica key is embedded in the Android application and is required by the SDK for activation. Leaving `appmetrica.apiKey` empty builds the application without analytics activation.

Generated artifacts are ignored by Git. `Build.yaml`, `Analytics.yaml` and the keystore are intentionally repository files; restrict repository access accordingly.
