# Google Play Signing Key

> This file contains the private signing credentials. Commit it only to a private repository with restricted access.

| Field | Value |
| --- | --- |
| Keystore file | `hardcore-cultivation-google-play.jks` |
| Store type | `JKS` |
| Alias | `hardcore-cultivation-google-play` |
| Keystore password | `564c004166b02bb7eb51389decd3fa630d41dc9317bd8f11` |
| Key password | `564c004166b02bb7eb51389decd3fa630d41dc9317bd8f11` |
| Key algorithm | `RSA 4096` |
| Validity | `9125 days` |

Release build:

```bash
ANDROID_KEYSTORE_PASSWORD='564c004166b02bb7eb51389decd3fa630d41dc9317bd8f11' \
ANDROID_KEY_PASSWORD='564c004166b02bb7eb51389decd3fa630d41dc9317bd8f11' \
./scripts/build-android.sh release --build 45 \
  --keystore Signing/Keystores/hardcore-cultivation-google-play.jks \
  --alias hardcore-cultivation-google-play
```

## Create A New Keystore

Use a new alias and file name for every separate Google Play application. Keep the password outside the repository unless this repository is intentionally restricted to the signing team.

```bash
cd /mnt/projects/mlkvs/vecxy.game

# Generate a password and keep it in the password manager.
PASSWORD="$(openssl rand -hex 24)"
printf 'New keystore password: %s\n' "$PASSWORD"

# Generate a 4096-bit RSA signing key valid for 25 years.
keytool -genkeypair -v \
  -keystore Signing/Keystores/new-google-play.jks \
  -storetype JKS \
  -storepass "$PASSWORD" \
  -keypass "$PASSWORD" \
  -alias new-google-play \
  -keyalg RSA \
  -keysize 4096 \
  -validity 9125 \
  -dname 'CN=Game Name, OU=Studio, O=Studio, L=Omsk, ST=Omsk Oblast, C=RU'

# Verify that the alias can be opened.
keytool -list \
  -keystore Signing/Keystores/new-google-play.jks \
  -storepass "$PASSWORD" \
  -alias new-google-play
```

Build with the new key:

```bash
ANDROID_KEYSTORE_PASSWORD="$PASSWORD" \
ANDROID_KEY_PASSWORD="$PASSWORD" \
./scripts/build-android.sh release --build 46 \
  --keystore Signing/Keystores/new-google-play.jks \
  --alias new-google-play
```

The command creates both a signed `.aab` for Google Play and a signed `.apk` for testers in `artifacts/android/release`.

## Key Rotation

Google Play requires the same app-signing key for updates unless Play App Signing is configured for the application. Do not replace the current key for an existing app without first completing the key-upgrade procedure in Google Play Console. Back up the `.jks` file and credentials before publishing the first release.
