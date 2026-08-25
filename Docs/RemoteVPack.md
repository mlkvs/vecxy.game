# Remote VPack

Remote VPack — слой получения пакетов поверх обычного VPack. После скачивания файл
проверяется и передаётся существующему `AssetPackage`/`VPackReader`; HTTP не является
частью бинарного формата или asset importer API.

## Descriptor

```yaml
name: DLC
version: 1.2.0
load: on-demand
compression: balanced

remote:
  manifest: https://cdn.example.com/game/packages.json
  cache: persistent
  update: check
  integrity: sha256
```

`version` — версия содержимого package в формате `major.minor.patch`. Она не связана
с `VPackFormat.Version`. В `remote` необходимо выбрать ровно один источник:

```yaml
remote:
  manifest: https://cdn.example.com/game/packages.json
```

или прямой URL:

```yaml
remote:
  url: https://cdn.example.com/{platform}/{name}-{version}.vpack
  size: 1928123712
  sha256: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
```

Placeholders: `{name}`, `{version}`, `{platform}`, `{architecture}`. Неизвестные
placeholder’ы и не-HTTP(S) URI являются build error. Для прямого URL поля `size` и
`sha256` нужны для полной integrity-проверки; primary manifest mode получает их из
`packages.json`.

Cache modes:

- `persistent` — сохраняется между запусками;
- `session` — удаляется при завершении `AssetsModule`;
- `none` — использует временный файл только для текущего runtime-сеанса.

Update policies:

- `manual` — сеть используется только явными `CheckForUpdatesAsync`/`DownloadAsync`;
- `check` — status/явная проверка видит обновление, но существующий локальный пакет
  не скачивается автоматически;
- `always` — `EnsureLoadedAsync` проверяет и получает более новую версию.

## Typed API

```csharp
var status = await Assets.DLC.CheckForUpdatesAsync();

if (status.IsUpdateAvailable)
    await Assets.DLC.DownloadUpdateAsync(progress, cancellationToken);

await using var dlc = await Assets.DLC.EnsureLoadedAsync(progress, cancellationToken);
renderer.Model = Assets.DLC.Models.Car;
```

`PackageDownloadProgress` отдаёт raw bytes, `Fraction`, bytes/sec, ETA и количество
уже имевшихся resume bytes. UI и форматирование размеров остаются в игре.

Дополнительные операции:

```csharp
var status = await Assets.DLC.GetRemoteStatusAsync();
await Assets.DLC.DownloadAsync(progress, cancellationToken);
var cache = await Assets.DLC.GetCacheInfoAsync();
await Assets.DLC.RemoveCachedAsync();
```

`LoadAsync` открывает уже доступный local VPack. `EnsureLoadedAsync` сначала
обеспечивает наличие пакета и его зависимостей, затем вызывает тот же package loader.

## Resolution и offline

Приоритет: более новая проверенная cached version → bundled version → remote по
явному запросу/policy. Старый cache не перекрывает более новый bundled package.
Ошибка update check не мешает запуску из валидного bundled/cache пакета. Если local
копии нет, network/manifest/platform error возвращается игре различимым exception.

Скачивание идёт в `package.vpack.download`. HTTP Range используется для resume;
сервер, проигнорировавший Range, вызывает безопасный restart. До activation
проверяются размер, SHA-256, VPack magic/version, PackageId и platform. Только после
этого выполняется atomic move и переключение active metadata. Открытая старая версия
остаётся рабочей до освобождения lease, после чего superseded version удаляется.

Одновременные запросы одного PackageId используют одну download operation. Отмена
одного waiter не отменяет I/O, пока операция нужна другому waiter; при отсутствии
waiter network/storage operation отменяется.

## Cache paths

Cache namespaced application identity и PackageId. Windows/Android используют
`LocalApplicationData`, Linux — `$XDG_CACHE_HOME` или `~/.cache`. Идентификатор и
пути нормализуются; remote metadata не участвует в построении filesystem path.
Путь можно переопределить через `AssetsModule.Options.PackageCacheDirectory`, а
application namespace — через `ApplicationId`.

## Build и upload

Каждый VPack build автоматически создаёт рядом с пакетами:

```text
Build/Windows/
├── game.vpack
├── packages.manifest   # bundled runtime discovery
├── packages.json       # remote distribution manifest
└── Remote/
    └── dlc.vpack       # upload artifact, never copied into the application
```

`packages.json` содержит фактические version, PackageId, platform, architecture,
размер, SHA-256, VPack format version и resolved URL. Отдельная команда:

```bash
./vecxy.sh --project HardCore.Cultivation packages manifest --platform windows
```

CLI работает из git submodule; глобальная установка не нужна. Файлы текущего
platform output можно загрузить на любой HTTP/CDN provider. Runtime не сканирует
каталоги и не парсит YAML.

## Remote manifest schema v1

```json
{
  "version": 1,
  "packages": {
    "DLC": {
      "id": "package-guid",
      "version": "1.2.0",
      "platforms": {
        "windows": {
          "url": "dlc.vpack",
          "size": 1928123712,
          "sha256": "...",
          "vPackFormatVersion": 1,
          "architecture": "x64"
        }
      }
    }
  }
}
```

Relative URLs resolve against manifest URL. Неизвестная schema version отклоняется.

## HardCore Cultivation stage backgrounds

Progression backgrounds use one package per stage. Each package contains the
`Cultivation` and `Missions` variants, so changing the activity mode never starts
another download. `BodyTemperingBackgrounds` has no `remote` section and ships in
the application. Every later stage uses `on-demand`, `persistent` cache and the
shared remote manifest.

The stage descriptors currently use the production manifest at
`https://s3.eponesh.com/games/draft/21228/packages.json`. Build the target platform, then upload
`Build/<Platform>/packages.json` and the contents of `Build/<Platform>/Remote/`
while preserving the `Remote/` relative path. The CLI calculates package `size`
and `sha256`; those values must not be maintained manually in YAML.

At runtime `Background.SetStageAsync` calls the generated typed package API. On
the first visit to a later stage Vecxy downloads, verifies, atomically activates
and caches that stage package. If the network is unavailable, the previous stage
background remains visible and the game can retry on the next scene sync.
