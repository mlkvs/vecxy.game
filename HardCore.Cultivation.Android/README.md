# HardCore.Cultivation for Android

The Android application is a small platform host. Game code lives in
`HardCore.Cultivation.Game.csproj`, which is shared with the desktop executable.
There are no linked copies of the game's C# source files in this project.

Requirements:

- .NET SDK `10.0.300` and the Android workload;
- Android SDK in `$ANDROID_SDK_ROOT` or `~/.local/share/android-sdk`;
- JDK in `~/.local/share/android-jdk`.

Build an APK for a physical ARM64 device:

```bash
./HardCore.Cultivation.Android/android.sh build arm64
```

Build for an x86_64 emulator:

```bash
./HardCore.Cultivation.Android/android.sh build emulator
```

With a device already visible in `adb devices`, install or install and run:

```bash
./HardCore.Cultivation.Android/android.sh install arm64
./HardCore.Cultivation.Android/android.sh run arm64
```

The script prints the exact signed APK path. It does not start or stop an
emulator and does not depend on a generated Activity class name.

Packaged engine and game assets are extracted into the app's private files
directory. The extraction cache is invalidated automatically whenever a new APK
is installed.

Touch input is sent to Vecxy as real multi-touch data. The Activity consumes the
native event after forwarding it, preventing SDL from generating a duplicate
mouse click.
