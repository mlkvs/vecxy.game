# VPack в Vecxy

VPack — бинарный пакет ассетов с независимой загрузкой и временем жизни. Папки задают границы пакетов, а игровой код по-прежнему использует типизированные handles по стабильному `AssetId`.

Удалённая доставка, versioning, resume, cache и typed download API описаны отдельно
в [RemoteVPack.md](RemoteVPack.md).

## Быстрый старт

Создайте descriptor внутри каталога ассетов:

```text
Assets/
├── Textures/player.png       # неявный пакет Game
└── DLC/
    ├── dlc.vpack
    ├── Models/car.glb
    └── Textures/car.png
```

Минимальный `Assets/DLC/dlc.vpack`:

```yaml
name: DLC
```

Главный пакет также можно настроить корневым `Assets/game.vpack`:

```yaml
name: Game
load: startup
compression: balanced

platforms:
  android:
    compression: fast
```

Корневой descriptor заменяет настройки неявного `Game`, а не создаёт второй пакет. Для него имя `Game` и режим `startup` обязательны; compression, platform overrides и зависимости используют тот же формат, что и остальные packages.

Ресурсы самого движка настраиваются отдельно в
`Engine/Vecxy/Code/Vecxy.Engine/Assets/engine.vpack`. Корневая папка engine assets
образует обязательный startup-пакет `Engine`; он получает стабильный `PackageId` и
собирается в `engine.vpack`. В коде типизированный API остаётся
`Assets.Engine.Shaders.*`, `Assets.Engine.Textures.*` и так далее.

Не каждый проект обязан упаковывать весь встроенный контент движка. Исключения
задаются в `.csproj` игры и применяются только к её `Assets.manifest` и
`engine.vpack`; исходные файлы submodule не меняются:

```xml
<PropertyGroup>
  <VecxyDisabledEngineFeatures>Skybox</VecxyDisabledEngineFeatures>
  <VecxyDisabledEngineContent>DefaultSkybox</VecxyDisabledEngineContent>
</PropertyGroup>
```

`Skybox` исключает renderer asset `Shaders/Skybox.glsl`, а `DefaultSkybox`
исключает встроенный конфиг и cubemap `SkyBox/**`. Проект со своим skybox должен
отключить только встроенный контент: shader останется в `engine.vpack`, а его
собственные шесть текстур попадут в `game.vpack` или выбранный project package.
Если свойства отсутствуют, сохраняется полная обратная совместимость и весь
стандартный контент движка включается. Несколько значений разделяются `;`.

После scan/generate основной ассет доступен как `Assets.Textures.Player`, а DLC — как `Assets.DLC.Models.Car` и `Assets.DLC.Textures.Car`.

```csharp
renderer.Texture = Assets.Textures.Player;

await using var dlc = await Assets.DLC.LoadAsync();
using var car = assets.Load<ModelAsset>(Assets.DLC.Models.Car);
```

Handle можно получить до загрузки пакета. Попытка материализовать asset из незагруженного on-demand пакета приводит к `AssetPackageNotLoadedException`; скрытой загрузки большого DLC нет.

## Границы пакетов

Каталог с `.vpack` является корнем пакета. Сам descriptor в пакет как asset не входит. Вложенный descriptor начинает новую границу:

```text
Assets/player.png                 -> Game
Assets/DLC/common.png             -> DLC
Assets/DLC/Cars/sedan.png         -> Cars
Assets/DLC/Cars/cars.vpack        -> descriptor Cars
```

Все файлы вне явных границ относятся к startup-пакету `Game`. `PackageId` детерминирован логическим именем и не зависит от output path или машины. Имя должно быть допустимым C# identifier и уникальным без учёта регистра.

## Формат descriptor

```yaml
name: Harbor
load: on-demand
compression: balanced
dependencies:
  - Shared

platforms:
  windows:
    compression: maximum
  linux:
    compression: balanced
  android:
    compression:
      algorithm: lz4
      block-size: 256kb
```

`load` принимает `startup` или `on-demand`. Presets: `none`, `fast`, `balanced`, `maximum`. Расширенная форма:

```yaml
compression:
  algorithm: zstd
  level: 5
  block-size: 512kb
```

Алгоритмы: `none`, `lz4`, `zstd`. Приоритет настроек: platform defaults → package config → platform override.

Профили находятся в одном `VPackPlatformProfiles`: desktop использует блоки 512 KB и Zstd 3/12 для balanced/maximum; Android — 256 KB и Zstd 1/6. `fast` использует LZ4. Уже сжатые payload’ы и блоки без выигрыша сохраняются raw.

## Зависимости

Если asset пакета ссылается на asset другого пакета, зависимость должна быть объявлена:

```yaml
name: Harbor
dependencies:
  - Shared
```

Validation проверяет отсутствующие и циклические зависимости, undeclared cross-package references и запрещает зависимость startup `Game` от on-demand content. Загрузка Harbor рекурсивно удерживает Shared. `AssetPackageLease.Dispose` уменьшает ref count; физическая выгрузка происходит только после освобождения зависимых leases и загруженных runtime assets.

## CLI и build

Глобальная установка не нужна:

```powershell
.\vecxy.cmd --project HardCore.Cultivation assets packages
.\vecxy.cmd --project HardCore.Cultivation assets validate
.\vecxy.cmd --project HardCore.Cultivation build --platform windows
.\vecxy.cmd --project HardCore.Cultivation build --platform linux
.\vecxy.cmd --project HardCore.Cultivation build --platform android
```

Прямой вызов из submodule:

```bash
dotnet run --project Engine/Vecxy/tools/Vecxy.Cli -- \
  --project HardCore.Cultivation build --platform android
```

`assets packages` показывает membership, load mode, зависимости и resolved compression. Внутренняя команда `assets pack --platform ...` выполняет prepare и создаёт только packages; её использует `build.sh`/`build.cmd`.

IDE использует конфигурацию сборки автоматически:

- `Debug` выполняет scan/generate/analyze/validate и работает с исходным `Assets/` для быстрого запуска и hot reload;
- `Release` перед компиляцией собирает VPack для выбранной `VecxyPlatform`, добавляет packages в output и на Android восстанавливает platform graph после desktop asset tools;
- поведение можно диагностически отключить свойством `-p:VecxyBuildVPack=false`, но для production Release делать этого не следует.

Таким образом, обычные **Build/Run в Release из IDE**, `dotnet build -c Release` и project-local `build.cmd`/`build.sh` используют бинарные packages. Wrapper дополнительно выполняет publish и release signing.

Output изолирован по проекту и платформе:

```text
HardCore.Cultivation/Build/Windows/game.vpack
HardCore.Cultivation/Build/Windows/engine.vpack
HardCore.Cultivation/Build/Windows/dlc.vpack
HardCore.Cultivation/Build/Windows/packages.manifest
```

`packages.manifest` является единственным runtime-каталогом packages; runtime не сканирует директории. В production output файлы `.vpack` и manifest лежат рядом с executable, без каталогов `Assets/Packages` и `Packages`. На Android они лежат в корне packaged assets и извлекаются в единый runtime-каталог. Исходная папка `Assets/` используется только при разработке и не копируется в Release, когда включена VPack-упаковка.

## Binary format

VPack не является ZIP. Version 1 содержит фиксированный header (`VXPK`, version, `PackageId`, platform, offsets/sizes), asset index по `AssetId`, таблицу зависимостей, таблицу blocks и данные. Каждый block независимо raw/LZ4/Zstd. Reader загружает immutable index, seek’ается к нужному block и декодирует только его. Неизвестная версия, повреждённые offsets/header и неизвестный codec отклоняются явно.

Путь файла не является runtime key: `AssetId → registry → PackageId → VPack index → block → importer`.

## Import pipeline

VPack writer принимает `VPackAssetSource`, то есть payload после importer/compiler stage, и не связывает binary format с исходными файлами. Сейчас у Vecxy ещё нет общего platform import cache для всех форматов, поэтому стандартный provider передаёт source bytes как вход существующим runtime importers. Texture transcoding в BCn/ASTC намеренно не реализовано в VPack; после появления platform import cache меняется provider, но format, IDs и runtime API остаются прежними.
