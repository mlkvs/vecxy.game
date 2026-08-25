# Asset Pipeline Vecxy

Этот документ описывает полный рабочий процесс с ассетами Vecxy: как добавить файл,
сгенерировать типизированный C# API, загрузить ресурс, найти его использования,
проверить проект и понять сообщения об ошибках.

## 1. Зачем нужен Asset Pipeline

Без pipeline код зависит от расположения файла:

```csharp
using var texture = assets.Load<TextureAsset>("Textures/Character.png");
```

Строка может содержать опечатку, не проверяется компилятором и незаметно ломается при
переименовании файла. С pipeline используется сгенерированный handle:

```csharp
using var texture = assets.Load<TextureAsset>(Assets.Textures.Character);
```

`Assets.Textures.Character` не содержит путь. Он содержит стабильный `AssetId`.
Реальный путь находится через `Assets.manifest` во время выполнения игры.

Общий поток данных:

```text
Assets/Textures/Character.png
              │
              ▼ assets scan
       Assets.manifest
              │
              ▼ assets generate
     Generated/Assets.g.cs
              │
              ▼ C# compiler
       Assets.Textures.Character
              │
              ▼ IAssetsManager.Load
          TextureAsset
```

## 2. Изоляция проектов

Pipeline всегда работает с одним игровым `.csproj`. Ассеты разных игр не смешиваются.

```text
vecxy.game/
├── HardCore.Cultivation/
│   ├── HardCore.Cultivation.csproj
│   ├── Assets/
│   ├── Assets.manifest
│   ├── Generated/Assets.g.cs
│   └── obj/vecxy.asset.references.json
├── Sponza/
│   ├── Sponza.csproj
│   ├── Assets/
│   ├── Assets.manifest
│   ├── Generated/Assets.g.cs
│   └── obj/vecxy.asset.references.json
└── Engine/Vecxy/
```

Из корня репозитория проект выбирается явно:

```powershell
.\vecxy.cmd --project HardCore.Cultivation assets generate
.\vecxy.cmd --project Sponza assets generate
```

Можно передать директорию или сам `.csproj`:

```powershell
.\vecxy.cmd --project Sponza\Sponza.csproj assets generate
```

Если текущая директория содержит ровно один `.csproj`, параметр можно опустить:

```powershell
cd HardCore.Cultivation
..\vecxy.cmd assets generate
```

На Linux/macOS используются те же аргументы:

```sh
./vecxy.sh --project Sponza assets generate
```

Глобально устанавливать `vecxy` не требуется. Wrapper запускает CLI из submodule:

```text
dotnet run --project Engine/Vecxy/tools/Vecxy.Cli -- ...
```

### Ассеты игры и движка

CLI читает `ProjectReference` игрового `.csproj` и автоматически находит
`Vecxy.Engine/Assets`. В manifest источники разделены полем `source`:

```json
{ "source": "Game", "path": "Textures/Character.png" }
{ "source": "Engine", "path": "Shaders/Sprite.glsl" }
```

Generated API также разделён:

```csharp
Assets.Textures.Character          // <Game>/Assets
Assets.Engine.Shaders.Sprite       // Vecxy.Engine/Assets
Assets.Engine.Inputs.Controls
```

Путь к submodule не зашит в CLI: источник определяется по `ProjectReference` на
`Vecxy.Engine.csproj`. При build game manifest копируется рядом с выходным `Assets/`,
а файлы игры и движка объединяются в runtime-каталоге. Если логические пути совпадают,
файл игры имеет приоритет.

## 3. Команды

### `assets scan`

```powershell
.\vecxy.cmd --project HardCore.Cultivation assets scan
```

Команда рекурсивно обходит `HardCore.Cultivation/Assets/` и обновляет
`HardCore.Cultivation/Assets.manifest`. Сканируются все файлы, включая неизвестные
пользовательские форматы.

### `assets generate`

```powershell
.\vecxy.cmd --project HardCore.Cultivation assets generate
```

Сначала выполняет scan, затем создаёт `Generated/Assets.g.cs`. Это обычная команда для
обновления C# API после добавления или переименования ассета.

### `assets analyze`

```powershell
.\vecxy.cmd --project HardCore.Cultivation assets analyze
```

Находит прямые использования сгенерированных свойств в C# и записывает результат в
`obj/vecxy.asset.references.json`.

### `assets validate`

```powershell
.\vecxy.cmd --project HardCore.Cultivation assets validate
```

Проверяет, что файлы из manifest существуют. При ошибке показывает ID, прежний путь и
известные места использования.

### `assets build` и `build`

```powershell
.\vecxy.cmd --project HardCore.Cultivation assets build
```

или:

```powershell
.\vecxy.cmd --project HardCore.Cultivation build
```

Обе формы выполняют одинаковый полный pipeline:

```text
scan → generate → analyze → validate → dotnet build
```

Для CI и обычной полной проверки проекта рекомендуется использовать именно эту команду.

### Обычный MSBuild и `./build release`

Игровые проекты импортируют `Vecxy.Platforms.props`. Его target `VecxyPrepareAssets`
автоматически запускает перед `PrepareForBuild`:

```text
scan → generate → analyze → validate
```

Поэтому pipeline работает не только через wrapper, но и через:

```bash
./build dev
./build release
dotnet build HardCore.Cultivation/HardCore.Cultivation.csproj
dotnet publish HardCore.Cultivation/HardCore.Cultivation.csproj
```

При missing asset Android/desktop packaging останавливается до создания артефакта.
После проверки исходный build flow продолжает собирать APK/AAB или desktop package.
Для служебной диагностики target можно отключить свойством
`-p:VecxySkipAssetPipeline=true`; в обычной разработке и CI отключать его не следует.

## 4. Manifest

Пример записи:

```json
{
  "id": "466008ff-5bba-4d13-a3fa-e3b0d01f3cf7",
  "path": "Textures/Character.png",
  "type": "Texture2D",
  "name": "Character",
  "hash": "27d8b8bb5c9ae3b7c8a8af7916dae94f5b55b127f69c978bad08ca04a357565b",
  "dependencies": []
}
```

Поля:

- `id` — постоянный GUID, который хранится в handle;
- `source` — владелец файла: `Game` или `Engine`;
- `path` — путь относительно `Assets/`, используемый только pipeline/runtime;
- `type` — распознанный вид ассета;
- `name` — стабильное имя сгенерированного C# свойства;
- `hash` — SHA-256 содержимого для распознавания rename;
- `dependencies` — ID других ассетов, которые читает этот ассет.

Manifest нужно хранить в Git. Не редактируйте GUID вручную без необходимости.

### Почему rename не ломает код

При первом scan файл получает GUID, hash и имя. Если файл перемещён или переименован,
следующий scan находит прежний hash и переносит запись на новый путь. Сохраняются:

- `id`;
- имя generated-свойства;
- ссылки из игрового кода.

Например, после `Character.png → Hero.png` свойство может остаться
`Assets.Textures.Character`. Это специально: rename файла не должен автоматически
ломать C# API. Если требуется переименовать и публичное свойство, измените `name` в
manifest осознанно и исправьте ошибки компиляции в местах использования.

Если существуют несколько файлов с полностью одинаковым содержимым, автоматическое
сопоставление rename может быть неоднозначным. После такого перемещения проверьте diff
manifest.

### Удалённые файлы

Запись удалённого файла остаётся как tombstone. Благодаря этому generated-свойство и
reference graph не исчезают до validation, и CLI может показать полезную ошибку:

```text
BUILD FAILED

Missing Asset:
466008ff-5bba-4d13-a3fa-e3b0d01f3cf7
Textures/Character.png

References:
Game/Character.cs:50
```

Если удаление было намеренным, сначала удалите все использования, затем удалите запись
из manifest вручную.

## 5. Generated API и handles

Файл `Generated/Assets.g.cs` не редактируется вручную. Пример:

```csharp
public static class Assets
{
    public static class Textures
    {
        [AssetReference("466008ff-5bba-4d13-a3fa-e3b0d01f3cf7")]
        public static TextureHandle Character =>
            new(new Guid("466008ff-5bba-4d13-a3fa-e3b0d01f3cf7"));
    }
}
```

Вложенный класс обычно берётся из первой директории пути:

```text
Assets/Textures/UI/Button.png → Assets.Textures.Button
Assets/Configs/GameBalance.yaml → Assets.Configs.GameBalance
Assets/Sounds/Click.wav → Assets.Sounds.Click
```

Поддерживаемые handles:

| Формат | Handle | Runtime-использование |
|---|---|---|
| изображения | `TextureHandle` | `Load<TextureAsset>` |
| `.glb` | `ModelHandle` | `Load<ModelAsset>` |
| `.material` | `MaterialHandle` | `Load<MaterialAsset>` |
| `.wav`, `.mp3`, `.ogg` | `SoundHandle` | `IAudioManager.Play` |
| `.yaml`, `.yml` | `ConfigHandle` | `LoadConfig<T>` |
| `.glsl` | `ShaderHandle` | `Load<ShaderAsset>` |
| `.input` | `InputHandle` | `Load<InputAsset>` |
| текстовые файлы | `TextHandle` | `Load<TextAsset>` |
| UI, font и custom-файлы | `AssetHandle` | соответствующий importer |

Handle — это лёгкая value-структура с `AssetId`. Он не загружает файл и не владеет
ресурсом.

## 6. Загрузка ассетов в runtime

### Текстура

```csharp
public sealed class Character(IAssetsManager assets) : IDisposable
{
    private AssetRef<TextureAsset>? _texture;

    public void Initialize()
    {
        _texture = assets.Load<TextureAsset>(Assets.Textures.Character);
        var pixels = _texture.Value.Pixels;
    }

    public void Dispose()
    {
        _texture?.Dispose();
    }
}
```

`AssetRef<T>` владеет ссылкой на кешированный ресурс. Его необходимо освобождать через
`Dispose`. Для короткой операции удобно использовать `using`:

```csharp
using var texture = assets.Load<TextureAsset>(Assets.Textures.Character);
Use(texture.Value);
```

Повторные `Load` одного ID и типа используют общую кешированную запись. Последний
`Dispose` освобождает ресурс.

### Модель и material

```csharp
using var model = assets.Load<ModelAsset>(Assets.Models.Sponza);
using var material = assets.Load<MaterialAsset>(Assets.Materials.Player);
```

### Звук

```csharp
audio.Play(Assets.Sounds.UiClick);
audio.Play(Assets.Musics.Main, loop: true, volume: 0.7f);
```

### Input

```csharp
using var inputAsset = assets.Load<InputAsset>(Assets.Inputs.Controls);
var gameplay = inputManager.Create(inputAsset, "Player");
```

Имена input maps/actions пока остаются частью содержимого `.input` и передаются строкой.
Asset Pipeline устраняет строковый путь к самому файлу, но не генерирует API содержимого.

### YAML-конфиг

```csharp
public sealed class GameBalance : IYamlConfig
{
    public float Speed { get; init; }
}

using var config = configProvider.LoadConfig<GameBalance>(
    Assets.Configs.GameBalance);

var speed = config.Value.Speed;
```

`ConfigHandle` выбирает файл по ID. Generic-параметр определяет C# тип, в который YAML
будет десериализован.

### Пользовательский формат

Неизвестный файл получает `AssetHandle`, поэтому путь всё равно не попадает в gameplay
код. Чтобы загрузить его, зарегистрируйте `IAssetImporter<T>`, поддерживающий расширение:

```csharp
assets.RegisterImporter<MyAsset>(new MyAssetImporter());
using var value = assets.Load<MyAsset>(Assets.Data.LevelOne);
```

## 7. Dependency tracking и references

Система хранит два разных вида связей.

### `dependencies` в manifest: ассет → ассет

Например material использует texture:

```text
Player.material
└── Textures/Character.png
```

Тогда ID текстуры находится в `dependencies` записи material. У самой текстуры список
может быть пустым — это нормально: текстура сама ничего не загружает.

Текущий scanner ищет asset-зависимости в `.material`, `.atlas`, `.xml` и `.css` по
относительным путям из manifest.

### Reference graph: C# → ассет

Использования из кода не записываются в `dependencies`. Они находятся в
`obj/vecxy.asset.references.json`:

```json
{
  "466008ff-5bba-4d13-a3fa-e3b0d01f3cf7": [
    {
      "file": "Game/Character.cs",
      "line": 50
    }
  ]
}
```

Итоговый граф можно читать так:

```text
Textures/Character.png
├── Player.material          asset dependency
└── Game/Character.cs:50     C# reference
```

Analyzer видит обращения вида `Assets.<Group>.<Name>`. Строка
`"Textures/Character.png"` не является типизированной ссылкой и в graph не попадёт.

## 8. Hot reload и FileSystemWatcher

В dev-режиме `AssetsModule` следит за `Assets/`, если `HotReloadEnabled` не отключён.

При изменении загруженного файла:

1. watcher получает событие;
2. события объединяются в течение `HotReloadDelay`;
3. вызывается `Reload(assetId)`;
4. importer создаёт новое значение;
5. существующие `AssetRef<T>` начинают видеть новое `Value` и увеличенный `Version`;
6. старое значение освобождается после успешной замены.

Если новый файл некорректен, рабочая предыдущая версия сохраняется, а ошибка попадает в
лог. При удалении выводится блок `[Vecxy Assets] Missing` с ID и путём.

Принудительный reload:

```csharp
assets.Reload(Assets.Textures.Character.Id);
```

## 9. Обычный рабочий процесс

### Добавление файла

1. Добавьте файл в `<Game>/Assets/`.
2. Выполните `assets generate`.
3. Используйте новое свойство из `Generated/Assets.g.cs`.
4. Перед commit выполните `assets build`.
5. Добавьте в Git ассет, `Assets.manifest` и, если принято в проекте, generated-файл.

### Переименование файла

1. Переименуйте или переместите файл, не меняя одновременно содержимое.
2. Выполните `assets generate`.
3. Проверьте diff manifest: GUID и `name` должны сохраниться, `path` — измениться.
4. Выполните `assets build`.

Если одновременно изменить содержимое и имя, hash уже не позволяет надёжно определить
rename. В таком случае сохраните прежние `id` и `name` в manifest вручную.

### Удаление файла

1. Найдите references через `assets analyze`.
2. Удалите или замените все `Assets.*` использования.
3. Удалите сам файл.
4. Выполните `assets validate`.
5. Если удаление намеренное, удалите tombstone-запись из manifest и снова запустите
   `assets generate`.

## 10. Что хранить в Git

Рекомендуется хранить:

- `Assets/**`;
- `Assets.manifest`;
- `Generated/Assets.g.cs` — чтобы IDE и сборка работали сразу после clone;
- wrapper `vecxy.cmd` и `vecxy.sh`.

Не нужно хранить:

- `obj/vecxy.asset.references.json`;
- `bin/` и остальные build artifacts.

Если команда generate запускается гарантированно до каждой сборки, generated-файл можно
не коммитить, но это должно быть единым правилом проекта.

## 11. Частые ошибки

### CLI пишет `No .csproj found`

Команда запущена из корня monorepo. Укажите игру:

```powershell
.\vecxy.cmd --project HardCore.Cultivation assets build
```

### Нового свойства нет

Запустите `assets generate`, затем откройте `Generated/Assets.g.cs`. Проверьте, что файл
физически находится внутри `Assets/` выбранного проекта.

### `dependencies` пустой, хотя ассет используется в C#

Это ожидаемо. `dependencies` содержит связи ассет → ассет. Выполните `assets analyze` и
смотрите `obj/vecxy.asset.references.json` для C# → ассет.

### Reference graph пустой

Проверьте, что код использует generated property:

```csharp
Assets.Textures.Character
```

Строковые пути analyzer не отслеживает. После исправления снова выполните
`assets analyze`.

### После rename появился новый ID

Обычно файл был одновременно переименован и изменён либо существует несколько файлов с
одинаковым hash. Верните прежний `id` и `name` нужной записи в manifest и удалите лишний
tombstone.

### `Unknown asset ID`

Убедитесь, что:

- `Assets.manifest` принадлежит запускаемому проекту;
- manifest находится рядом с `.csproj`;
- runtime `AssetsDirectory` указывает на `Assets/` этой игры;
- generate и запуск выполнялись для одного проекта.

### `No asset importer is registered`

Handle был создан, но runtime не знает, как преобразовать расширение в C# объект.
Зарегистрируйте подходящий `IAssetImporter<T>` или загружайте файл через модуль, которому
принадлежит этот формат.

## 12. Краткая памятка

```powershell
# Добавить/обновить generated API
.\vecxy.cmd --project HardCore.Cultivation assets generate

# Обновить C# reference graph
.\vecxy.cmd --project HardCore.Cultivation assets analyze

# Проверить отсутствующие файлы
.\vecxy.cmd --project HardCore.Cultivation assets validate

# Полная рекомендуемая проверка
.\vecxy.cmd --project HardCore.Cultivation assets build
```

В игровом коде:

```csharp
using var texture = assets.Load<TextureAsset>(Assets.Textures.Character);
using var config = configs.LoadConfig<GameBalance>(Assets.Configs.GameBalance);
audio.Play(Assets.Sounds.UiClick);
```

Главное правило: пути допустимы внутри asset-файлов и importers, но gameplay C# должен
использовать generated handles.
## VPack packages

Для independently loadable DLC, уровней и локализаций Asset Pipeline поддерживает folder-based `.vpack` descriptors, binary block containers и typed package API. Полный workflow, YAML format, CLI и runtime lifetime описаны в [VPack.md](VPack.md).

Ресурсы движка собираются в обязательный startup-пакет `engine.vpack`, настроенный
файлом `Engine/Vecxy/Code/Vecxy.Engine/Assets/engine.vpack`. Production packages и
`packages.manifest` размещаются прямо в корне platform output; отдельные папки
`Packages/` и runtime `Assets/` для них не создаются.
