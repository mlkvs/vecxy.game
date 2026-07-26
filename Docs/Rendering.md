# Рендеринг в Vecxy: от asset-файла до пикселя на экране

Этот документ описывает текущую реализацию `Vecxy.Assets` и
`Vecxy.Rendering`, а затем объясняет необходимые для её понимания основы
OpenGL 3.3 Core.

Цель документа — не только показать, какой метод вызывается следующим, но и
объяснить:

- где находятся данные на CPU и GPU;
- кто владеет каждым объектом;
- чем asset отличается от runtime GPU-ресурса;
- какое состояние OpenGL необходимо активировать перед draw call;
- почему порядок вызовов важен;
- какие части текущей реализации являются начальным упрощением.

## 1. Картина целиком

В проекте существуют два разных мира.

### Мир assets — CPU

`Vecxy.Assets` читает файлы и превращает их в обычные объекты C#:

```text
textured.glsl ──> ShaderAsset
                    ├── TextAsset Vertex
                    └── TextAsset Fragment

checker.ppm ───> TextureAsset
                    ├── Width
                    ├── Height
                    └── Pixels (RGBA byte[])

checker-blue.material ──> MaterialAsset
                            ├── AssetRef<ShaderAsset>
                            └── параметры:
                                ├── AssetRef<TextureAsset>
                                └── Vector4 uTint
```

На этом уровне нет OpenGL handles. `TextureAsset.Pixels` — массив в памяти
процесса, а `ShaderAsset.Vertex.Content` — строка GLSL.

### Мир Rendering — GPU

`Vecxy.Rendering` превращает CPU-assets в объекты видеодрайвера:

```text
ShaderAsset  ──> Shader  ──> OpenGL program
TextureAsset ──> Texture ──> OpenGL texture
float[]      ──> Mesh    ──> VAO + VBO + EBO
```

`Shader`, `Texture` и `Mesh` владеют GPU-ресурсами. Поэтому они реализуют
`IDisposable` и должны уничтожаться, пока OpenGL context ещё существует.

Главный путь одного объекта:

```text
GameLayer
  │
  ├── загружает AssetRef<MaterialAsset>
  ├── создаёт Mesh
  └── добавляет RenderItem в GameView
          │
          ▼
RenderingModule.OnRender()
  │
  ├── активирует render target
  ├── очищает framebuffer
  ├── сортирует RenderItem по фазам
  ├── Material.Bind()
  │     ├── активирует Shader
  │     ├── активирует Texture
  │     └── отправляет material uniforms
  ├── отправляет object uniform uTransform
  ├── Mesh.Draw()
  └── Present() / SwapBuffers()
```

## 2. Инициализация движка и порядок модулей

### `Engine`

`Engine` владеет главным циклом приложения.

Сначала создаётся `Window`, затем Autofac-контейнер и app layers. Метод
`Run()` выполняет:

```text
Window.Initialize()
InitializeLayers()
RunLoop()
```

Это принципиально: OpenGL context появляется при инициализации окна. Создавать
GPU-ресурсы до этого момента нельзя.

Один кадр в `RunLoop()`:

```text
Window.PollEvents()
Update(deltaTime)
Render()
ограничение частоты кадров
```

`PollEvents()` обрабатывает события ОС: закрытие окна, resize, клавиатуру и
мышь. После этого обновляется логика, затем строится изображение.

### `EngineLayer`

`EngineLayer.Definition` больше не содержит регистрацию всех систем вручную.
Вместо этого он декларативно перечисляет дочерние definitions:

```csharp
public override IReadOnlyList<IDefinition> Children { get; } =
[
    new AssetsModule.Definition(),
    new RenderingModule.Definition(),
    new ScenesModule.Definition()
];
```

Каждый module definition сам знает, какие интерфейсы он экспортирует в scope
своего app layer:

- `AssetsModule` как `IAssetsManager`;
- `RenderingModule` как `IRenderer`;
- `ScenesModule` как `ISceneManager`.

Engine рекурсивно обходит definitions любой глубины. `RegisterGlobal()`
вызывается до построения root container, а `RegisterLocal()` — при построении
scope соответствующего app layer. Для module definition внутри этого scope
создаётся приватный дочерний scope. Там живут сам модуль и его внутренние
зависимости. В scope app layer проецируются только `IModule` и явно
перечисленные exports.

Например, `RenderingModule` видит свои `GraphicsDevice`, shader/texture/
material libraries и родительский `IAssetsManager`, но `EngineLayer` не может
запросить эти конкретные реализации. Он видит модуль только как `IModule`.
Следующий `GameLayer` видит экспорт `IRenderer`, потому что scopes app layers
образуют цепочку в порядке definitions:

```text
Root → EngineLayer → GameLayer
          ├─ AssetsModule private scope
          ├─ RenderingModule private scope
          └─ ScenesModule private scope
```

Дочерние definitions не заменяют app layers и не получают отдельный
lifecycle: их экземпляры по-прежнему попадают в `IEnumerable<IModule>`
родительского layer.

При обходе проверяются циклы, повторное использование одного definition
instance и `null` среди детей.

Module definitions добавляют свои экземпляры как `IModule`. Порядок
регистрации сейчас такой:

1. Assets;
2. Rendering;
3. Scenes.

Поэтому `AssetsModule.OnInitialize()` успевает зарегистрировать импортёры до
того, как `GameLayer` запросит материалы.

### `GameLayer`

`GameLayer` является пользовательской точкой сборки представления. Он не
вызывает OpenGL напрямую.

Во время `OnInitialize()` он:

1. загружает два `MaterialAsset`;
2. просит `IRenderer` создать quad mesh;
3. создаёт `GameView`;
4. добавляет в него два `RenderItem`;
5. задаёт каждому item материал и transform.

Во время `OnUnload()` он:

1. удаляет `GameView` из renderer;
2. уничтожает созданный `Mesh`.

Такой порядок важен: renderer больше не должен обращаться к mesh после его
удаления.

## 3. Asset-система по классам

### `AssetId`

`AssetId` — постоянная идентичность asset:

```csharp
public readonly record struct AssetId(Guid Value);
```

Сейчас `AssetId.FromPath()` вычисляет SHA-256 от нормализованного пути и берёт
первые 16 байт.

Следствие текущей реализации:

- одинаковый путь даёт одинаковый ID;
- переименование файла меняет ID;
- это ещё не полноценный editor registry со стабильным ID после rename.

Позже ID следует сохранять в registry или `.meta`-файле. Остальная система
уже работает через `AssetId`, поэтому способ его хранения можно будет
заменить.

### `AssetMetadata`

`AssetMetadata` описывает asset, не загружая его содержимое:

```text
Id         — идентификатор
AssetType  — ожидаемый C# Type
Path       — путь относительно Assets/
IsLoaded   — находится ли значение в кэше
```

Metadata и сам ресурс — разные вещи. Запись metadata может существовать, даже
когда текстура или материал не загружены.

### `AssetRegistry`

`AssetRegistry` содержит два индекса:

```text
AssetId -> AssetMetadata
Path    -> AssetId
```

Первый нужен для загрузки по ID, второй — для поиска по пути.

Метод `Add()` запрещает:

- повторный ID;
- повторный путь.

Сейчас registry наполняется лениво: запись создаётся при первом
`Find(path)`. На следующем этапе его можно загружать из YAML-файла проекта.

### `AssetRef<T>`

`AssetRef<T>` — владеющая ссылка на общую запись asset:

```text
Id        — идентичность
Metadata  — описание
Value     — текущее значение типа T
Version   — номер загруженной версии
IsLoaded  — доступно ли значение
```

Каждый `Load<T>()` и `Acquire()` увеличивает reference count общей записи.
`Dispose()` конкретной ссылки уменьшает его. Когда освобождена последняя
ссылка, `AssetsModule` автоматически удаляет запись из CPU-кэша и освобождает
её значение.

```csharp
using var material = assets.Load<MaterialAsset>("Materials/example.material");
using var anotherOwner = material.Acquire();
```

Обе ссылки указывают на один `MaterialAsset`, но освобождаются независимо.
После `Dispose()` обращаться к `Id`, `Metadata`, `Value` или `Version` этой
конкретной ссылки нельзя.

Допустим, код хранит:

```csharp
AssetRef<ShaderAsset> shaderAsset;
```

При reload общая запись остаётся той же, поэтому все живые `AssetRef`
увидят новое `Value` и увеличенный `Version`.

```text
до reload:
AssetRef A ─┐
            ├──> AssetRefEntry ──> ShaderAsset A, Version 1
AssetRef B ─┘

после reload:
AssetRef A ─┐
            ├──> AssetRefEntry ──> ShaderAsset B, Version 2
AssetRef B ─┘
```

Благодаря этому `MaterialAsset`, хранящий `AssetRef<ShaderAsset>`, увидит
новую версию без замены всех ссылок на материал.

`AssetRefEntry<T>` хранит значение, версию и reference count. Внутренний
`IAssetRefEntry` позволяет `AssetsModule` держать assets разных типов в одном
словаре, сохраняя типизированный публичный API.

### `IAssetImporter<T>`

Importer отвечает за преобразование файла в CPU-объект:

```csharp
T Import(AssetMetadata metadata, AssetImportContext context);
```

Он также сообщает поддерживаемые расширения.

Примеры:

```text
.glsl     -> ShaderAssetImporter -> ShaderAsset
.png      -> TextureAssetImporter -> TextureAsset
.material -> MaterialAssetImporter -> MaterialAsset
```

Importer не обязан создавать GPU-объекты. В текущей архитектуре это
запрещённая граница: Assets не зависит от OpenGL.

Внутренний `AssetImporter<T>` стирает generic type до `object`, чтобы все
importers можно было хранить в:

```csharp
Dictionary<Type, IAssetImporter>
```

### `AssetImportContext`

`AssetImportContext` даёт importer безопасный доступ к данным:

- `GetFullPath()` переводит asset path в абсолютный;
- `ReadAllText()` читает текст;
- `ReadAllBytes()` читает бинарные данные;
- `Load<T>()` загружает зависимый asset.

`GetFullPath()` проверяет, что путь не вышел за корень `Assets/`. Это не даёт
пути наподобие `../../secret.file` обратиться к произвольному файлу.

`Load<T>()` особенно важен для составных assets. Например,
`MaterialAssetImporter` во время импорта материала загружает shader и
texture.

### `AssetsModule`

`AssetsModule` — центральный менеджер CPU-assets.

Он хранит:

```text
_importers       Type -> importer
_extensionTypes  extension -> Type
_loaded          AssetId -> AssetRefEntry
Registry         metadata
```

Во время `OnInitialize()` регистрируются:

- `TextAssetImporter`;
- `ShaderAssetImporter`;
- `TextureAssetImporter`;
- `MaterialAssetImporter`.

#### Что делает `Load<T>(path)`

Полная последовательность:

1. `Find(path)` нормализует путь.
2. Registry проверяется на уже известный asset.
3. По расширению выбирается C# type.
4. Создаётся `AssetId` и `AssetMetadata`.
5. Отсутствующий файл обрабатывается importer как failed asset.
6. `Load<T>(id)` проверяет CPU-кэш.
7. Проверяется совпадение `metadata.AssetType` и `typeof(T)`.
8. Вызывается importer.
9. Результат помещается в общую `AssetRefEntry<T>`.
10. Reference count увеличивается и новая ссылка возвращается пользователю.

Повторный `Load<T>()` возвращает новый `AssetRef<T>` на ту же запись. Поэтому
каждый владелец обязан освободить именно свою ссылку.

#### Автоматическая выгрузка

Когда reference count достигает нуля:

1. запись удаляется из `_loaded`;
2. `Metadata.IsLoaded` становится `false`;
3. значение asset освобождается, если оно реализует `IDisposable`;
4. публикуется событие `IAssetsManager.Unloaded`.

`MaterialAsset` владеет ссылками на shader и textures и освобождает их в
`Dispose()`. Поэтому зависимости живут, пока существует хотя бы один
использующий их материал:

```text
RenderItem
  └── AssetRef<MaterialAsset>
        ├── AssetRef<ShaderAsset>
        └── AssetRef<TextureAsset>
```

`GameView.Submit()` вызывает `Acquire()` и получает собственную ссылку.
`Remove()`, `Clear()` и `DestroyGameView()` освобождают её. Передавший
материал код может сразу освободить свою исходную ссылку.

`Unload<T>()` остаётся принудительной массовой операцией. Она выгружает
assets даже при наличии ссылок; такие ссылки становятся недействительными.
Обычный runtime-код должен предпочитать `Dispose()`.

#### Что делает `Reload(id)`

1. Находит существующий `AssetRef`.
2. Повторно запускает importer.
3. Только после успешного импорта заменяет `Value`.
4. Увеличивает `Version`.
5. Освобождает старое значение, если оно реализует `IDisposable`.

GPU-кэши сравнивают сохранённую версию с `AssetRef.Version` и обновляют
ресурс при следующем использовании.

Если importer выбрасывает исключение, запись сохраняет прежнее значение,
помечается через `AssetRef.HasError` и также получает новую версию. Для
первой неудачной загрузки создаётся failed entry без `Value`. Благодаря
этому отсутствующий или изначально сломанный asset тоже может иметь
стабильный `AssetRef` и восстановиться после следующего сохранения.

#### Hot Reload файлов

`AssetFileWatcher` рекурсивно следит за каталогом `Assets/`. Он принимает
события изменения, создания, удаления и переименования файлов.

Callbacks `FileSystemWatcher` выполняются в фоновом потоке, поэтому watcher
не вызывает `Reload()` самостоятельно. Он только нормализует путь и
складывает его в потокобезопасную очередь:

```text
FileSystemWatcher thread
  └── ConcurrentQueue<string>
          ↓
AssetsModule.OnUpdate() на главном потоке
```

`AssetsModule` реализует `IModule.IUpdatable`. В `OnUpdate()` он переносит
события в таблицу pending paths и ждёт, пока файл не перестанет изменяться.
Это debounce: большинство редакторов создаёт несколько filesystem events
на одно сохранение.

По умолчанию задержка равна 150 мс. Настройка передаётся без изменения
структуры definitions:

```csharp
new EngineLayer.Definition(
    new AssetsModule.Options
    {
        AssetsDirectory = "/path/to/project/Assets",
        HotReloadEnabled = true,
        HotReloadDelay = TimeSpan.FromMilliseconds(200)
    })
```

По умолчанию используется `Assets/` рядом с executable. Для разработки
следует передать исходный каталог проекта, иначе watcher будет следить за
копией ресурсов в `bin/`. Игровой пример вычисляет путь до `Game/Assets`
относительно `AppContext.BaseDirectory`.

После debounce модуль:

1. ищет путь в `AssetRegistry`;
2. проверяет, загружен ли asset;
3. вызывает существующий `Reload(id)`;
4. при ошибке помечает ссылку ошибочной и логирует исключение.

Незагруженные assets не импортируются только из-за изменения файла. При
ошибке YAML, GLSL, чтения файла или зависимостей прежнее CPU-значение не
уничтожается, но renderer временно показывает розовый fallback. Следующее
сохранение создаст новую попытку и очистит error-state при успехе.

Для shader и texture успешный reload увеличивает `AssetRef.Version`.
Соответствующая GPU-библиотека замечает версию при следующем `Get()` и
сначала создаёт replacement. Старый OpenGL-ресурс удаляется только после
успешного создания нового.

Reload материала повторно читает YAML и получает новые владеющие ссылки на
shader и textures. После успешной замены старый `MaterialAsset.Dispose()`
освобождает прежние зависимости; reference counting удаляет только те из
них, которые больше нигде не используются.

### `TextAsset` и `TextAssetImporter`

`TextAsset` хранит строку `Content`.

Его importer поддерживает `.txt`, `.vert` и `.frag`. В текущем shader format
отдельные `.vert/.frag` не используются, но importer уже пригоден для других
текстовых assets.

### `ShaderAsset` и `ShaderAssetImporter`

`ShaderAsset` — CPU-представление одного `.glsl`:

```csharp
ShaderAsset
{
    TextAsset Vertex;
    TextAsset Fragment;
}
```

Файл разделён директивами:

```glsl
#type vertex
// vertex GLSL

#type fragment
// fragment GLSL
```

`ShaderAssetImporter` читает файл построчно и собирает две строки. Он
проверяет:

- наличие vertex stage;
- наличие fragment stage;
- отсутствие повторяющейся stage;
- корректность имени stage.

Он ничего не компилирует. Его результат остаётся обычными CPU-данными.

### `TextureAsset` и `TextureAssetImporter`

`TextureAsset` содержит:

```text
Width
Height
Pixels — RGBA, 4 байта на пиксель
```

Для PNG/JPG/BMP/TGA importer использует `StbImageSharp`. Независимо от
исходного количества каналов результат приводится к RGBA.

Для демонстрационной `.ppm` текстуры реализован небольшой P3 parser.

После импорта это всё ещё CPU-массив. OpenGL texture появится позднее в
`TextureLibrary`.

### `MaterialAsset` и параметры

`MaterialAsset` состоит из:

```text
AssetRef<ShaderAsset> Shader
Dictionary<string, MaterialParameter> Parameters
```

Поддерживаются параметры:

- `TextureMaterialParameter`;
- `VectorMaterialParameter`;
- `FloatMaterialParameter`.

`color` и `vector` сейчас оба становятся `Vector4`; разные YAML-имена нужны
для читаемости и будущего разделения семантики.

Пример:

```yaml
shader: Shaders/textured.glsl

parameters:
  uTexture:
    texture: Textures/checker.ppm
  uTint:
    color: [0.35, 0.70, 1.00, 1.00]
  uRoughness:
    float: 0.5
```

Имена `uTexture`, `uTint` и `uRoughness` должны совпадать с именами uniforms
в GLSL.

`MaterialAssetImporter`:

1. разбирает YAML через YamlDotNet;
2. загружает `ShaderAsset`;
3. для texture-параметров загружает `TextureAsset`;
4. проверяет, что у параметра указан ровно один тип значения;
5. создаёт неизменяемый словарь параметров.

## 4. Rendering-система по классам

### `Window`

`Window` оборачивает Silk.NET Windowing.

При создании запрашивается:

```text
API: OpenGL
Profile: Core
Version: 3.3
ForwardCompatible: true
ShouldSwapAutomatically: false
```

Core profile означает, что старые immediate-mode вызовы вроде `glBegin()` и
`glVertex()` недоступны. Геометрия должна поступать через buffers и vertex
attributes.

`ShouldSwapAutomatically = false` означает, что renderer сам решает, когда
показать готовый кадр.

Основные методы:

- `Initialize()` создаёт нативное окно и OpenGL context;
- `MakeCurrent()` делает context текущим для вызывающего потока;
- `GetProcAddress()` предоставляет адреса OpenGL-функций;
- `SwapBuffers()` показывает готовый backbuffer;
- `PollEvents()` обрабатывает события окна.

### `GraphicsDevice`

`GraphicsDevice` — низкоуровневая точка доступа к OpenGL:

```csharp
context.MakeCurrent();
GL = Silk.NET.OpenGL.GL.GetApi(context.GetProcAddress);
```

OpenGL-функции предоставляются драйвером, поэтому Silk.NET получает их адреса
из context.

Практическое правило: любой вызов `device.GL.*` должен происходить:

- после создания context;
- на потоке, где этот context сделан current;
- до уничтожения context.

### `ShaderCompiler`

`ShaderCompiler` превращает `ShaderAsset` в OpenGL program.

Для каждой stage:

```text
CreateShader
ShaderSource
CompileShader
GetShader(CompileStatus)
```

После успешной компиляции двух stages:

```text
CreateProgram
AttachShader(vertex)
AttachShader(fragment)
LinkProgram
GetProgram(LinkStatus)
```

После link отдельные shader objects больше не нужны и удаляются через
`DeleteShader`. Их машинный код уже включён в linked program.

Если compile или link завершается ошибкой, compiler получает info log и
выбрасывает исключение. Неуспешно созданные handles удаляются.

### `Shader`

`Shader` владеет одним linked OpenGL program.

`Bind()` вызывает:

```csharp
gl.UseProgram(program);
```

После этого program становится частью текущего OpenGL state. Следующие draw
calls используют именно его, пока другой код не вызовет `UseProgram`.

Методы `Set()`:

1. находят uniform через `GetUniformLocation`;
2. отправляют значение через `Uniform1`, `Uniform2`, `Uniform4` или
   `UniformMatrix4`.

Если location равен `-1`, uniform отсутствует или был удалён оптимизатором
GLSL. Текущая реализация просто пропускает такой параметр.

#### Почему матрица транспонируется

`System.Numerics.Matrix4x4` и GLSL используют разные практические соглашения
о размещении матрицы и умножении vector/matrix. Перед отправкой текущий код
делает:

```csharp
var transposed = Matrix4x4.Transpose(value);
gl.UniformMatrix4(location, 1, false, &transposed);
```

Это позволяет собирать transform привычными средствами `System.Numerics`, а
в GLSL использовать:

```glsl
gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
```

`Dispose()` вызывает `DeleteProgram`.

### `ShaderLibrary`

`ShaderLibrary` кэширует runtime shaders:

```text
AssetId -> Shader + версия ShaderAsset
```

При `Get(assetRef)` возможны три случая.

1. Shader ещё не создан — compile и добавить в кэш.
2. Версия не изменилась — вернуть существующий shader.
3. Версия изменилась — скомпилировать replacement.

При успешном reload старый program удаляется только после создания нового.
При ошибке используется встроенная розовая shader program.

Когда последний `AssetRef<ShaderAsset>` освобождён, `ShaderLibrary` получает
событие `Unloaded`, удаляет запись из кэша и вызывает `DeleteProgram`.

### Розовый fallback

`ShaderCompiler` содержит встроенные vertex и fragment sources, которые не
зависят от файлов в `Assets/`. Fragment stage всегда выводит:

```glsl
vec4(1.0, 0.0, 1.0, 1.0)
```

`Material.Bind()` выбирает этот shader, если:

- сам `MaterialAsset` имеет `HasError`;
- shader материала имеет `HasError`;
- хотя бы одна texture имеет `HasError`;
- обычный bind или создание GPU-ресурса выбросило исключение;
- пользовательский GLSL не скомпилировался или не слинковался.

Fallback поддерживает `uTransform`, поэтому объект сохраняет свою геометрию
и положение, но целиком становится розовым. После успешного hot reload
error-state очищается, версия увеличивается, и следующий bind снова
использует обычный материал.

### `Texture`

`Texture` владеет OpenGL texture handle.

Создание:

```text
GenTexture
BindTexture(Texture2D)
TexImage2D
TexParameter(...)
BindTexture(Texture2D, 0)
```

`TexImage2D` передаёт RGBA-массив из `TextureAsset` драйверу:

```text
internal format: RGBA8
width/height: размеры asset
input format: RGBA
input type: UnsignedByte
```

После вызова OpenGL скопировал данные. CPU-массив больше не нужен для
рисования, хотя asset продолжает хранить его для reload.

Параметры:

```text
MIN_FILTER = Nearest
MAG_FILTER = Nearest
WRAP_S = Repeat
WRAP_T = Repeat
```

`Nearest` выбран, чтобы маленький checker оставался резким. Для обычной
графики позже понадобятся linear filtering и mipmaps.

#### Что делает `Texture.Bind(slot)`

```text
ActiveTexture(Texture0 + slot)
BindTexture(Texture2D, handle)
```

У OpenGL есть набор texture units. Shader sampler хранит не texture handle, а
номер unit.

Для slot 0:

```text
Texture unit 0 содержит texture handle 17
uniform uTexture содержит integer 0
```

Тогда `texture(uTexture, uv)` читает texture 17.

### `TextureLibrary`

`TextureLibrary` работает аналогично `ShaderLibrary`:

```text
AssetId -> Texture + версия TextureAsset
```

Она лениво загружает пиксели на GPU и пересоздаёт texture после изменения
версии asset. При выгрузке последнего `AssetRef<TextureAsset>` библиотека
удаляет соответствующую OpenGL texture, поэтому CPU- и GPU-кэши не растут
бесконечно.

### `Material`

`Material` — runtime-мост между `MaterialAsset` и GPU state.

`Bind(assetRef)` выполняет:

1. берёт актуальный `MaterialAsset` из `AssetRef`;
2. получает runtime `Shader` из `ShaderLibrary`;
3. вызывает `shader.Bind()`;
4. проходит по параметрам материала;
5. для texture получает runtime `Texture`;
6. назначает следующую texture unit;
7. записывает номер unit в sampler uniform;
8. отправляет vector и float uniforms.

Метод возвращает активный `Shader`, чтобы renderer затем записал
per-object параметры, например `uTransform`.

Разделение параметров:

```text
Material parameters:
uTexture, uTint, roughness...

Object parameters:
uTransform, object ID...

View parameters:
view/projection, camera position...
```

Сейчас реализованы первые две группы частично.

### `MaterialLibrary`

`MaterialLibrary` кэширует runtime `Material` по `AssetId`.

Сам `Material` не владеет ни OpenGL handle, ни `AssetRef`. Ссылка передаётся
в каждый `Bind(assetRef)`, поэтому кэш не удерживает CPU-asset загруженным и
не образует цикл владения. При событии выгрузки `MaterialAsset` его лёгкая
runtime-обёртка также удаляется из кэша.

### `Mesh`

`Mesh` владеет:

```text
VAO — Vertex Array Object
VBO — Vertex Buffer Object
EBO — Element/Index Buffer Object
```

Quad содержит четыре вершины. Формат одной вершины:

```text
float position.x
float position.y
float uv.x
float uv.y
```

То есть 4 `float`, или 16 байт.

Индексы:

```text
0, 1, 2,
2, 3, 0
```

Они превращают четыре вершины в два треугольника.

#### Создание VBO

```text
GenBuffer
BindBuffer(ArrayBuffer, VBO)
BufferData(ArrayBuffer, vertices, StaticDraw)
```

`StaticDraw` — подсказка драйверу: данные будут редко изменяться и часто
использоваться для рисования.

#### Создание EBO

```text
GenBuffer
BindBuffer(ElementArrayBuffer, EBO)
BufferData(ElementArrayBuffer, indices, StaticDraw)
```

EBO содержит не вершины, а номера вершин.

#### Настройка vertex attributes

Для position:

```text
location = 0
components = 2
type = float
stride = 16 bytes
offset = 0 bytes
```

Для UV:

```text
location = 1
components = 2
type = float
stride = 16 bytes
offset = 8 bytes
```

`VertexAttribPointer` описывает, как читать VBO. Он не копирует вершины.

`EnableVertexAttribArray(location)` включает соответствующий attribute.

VAO запоминает:

- описание attributes;
- связанные с attributes buffers;
- текущий EBO.

Поэтому во время кадра достаточно вызвать `BindVertexArray(VAO)`.

`Mesh.Draw()`:

```text
BindVertexArray
DrawElements(Triangles, IndexCount, UnsignedInt, offset 0)
```

`Dispose()` удаляет EBO, VBO и VAO.

### `ERenderPhase`

Фазы:

```text
Background
Opaque
Transparent
Overlay
```

Сейчас фаза влияет только на порядок:

```csharp
OrderBy(item => item.Phase)
```

Текущая реализация пока не делает автоматически:

- включение depth test для Opaque;
- front-to-back sorting;
- включение blending для Transparent;
- back-to-front sorting;
- отключение depth test для Overlay.

Это следующий естественный шаг. Сейчас enum формирует архитектурную точку,
куда такое поведение будет добавлено.

### `RenderItem`

`RenderItem` описывает одну операцию рисования:

```text
Phase
Mesh
AssetRef<MaterialAsset>
Transform
Enabled
```

Он не содержит OpenGL-вызовов. Это декларативная команда: что нарисовать и
с какими данными.

Transform является per-object состоянием, поэтому находится в `RenderItem`,
а не в материале.

### `GameView`

`GameView` объединяет:

- render target;
- clear color;
- список render items;
- флаг `Enabled`.

Сейчас список persistent: `Submit()` добавляет item, который рисуется каждый
кадр, пока не будет вызван `Remove()`, `Clear()` или `DestroyGameView()`.

Это отличается от immediate command list, очищаемого каждый кадр.

`GameView` отвечает на вопрос: «какой набор объектов и куда рисовать?»

Позже сюда естественно добавить:

- camera;
- viewport rectangle;
- render layers/masks;
- post-processing;
- собственный framebuffer.

### `IRenderTarget`

Render target отвечает на вопрос: «в какой framebuffer рисовать?»

Контракт:

```text
Width
Height
Bind(GraphicsDevice)
Present()
```

Текущая реализация — `BackbufferRenderTarget`.

Будущая offscreen-реализация сможет:

1. создать framebuffer object;
2. прикрепить color/depth textures;
3. в `Bind()` вызвать `BindFramebuffer`;
4. предоставить color texture следующему pass;
5. не делать window swap в `Present()`.

### `BackbufferRenderTarget`

`Bind()`:

1. делает window context current;
2. устанавливает viewport размером framebuffer окна.

`Present()` вызывает `SwapBuffers()`.

Окно использует double buffering:

```text
frontbuffer — сейчас видит пользователь
backbuffer  — renderer строит следующий кадр
```

`SwapBuffers()` меняет их местами. Без него пользователь не увидит
нарисованный backbuffer.

### `IRenderer`

Публичный минимальный API:

```text
Statistics
CreateGameView()
DestroyGameView()
CreateQuad()
```

Он скрывает от Game-кода `GraphicsDevice`, OpenGL handles и runtime
libraries.

### `RenderingModule`

`RenderingModule` координирует весь rendering.

Приватный scope модуля создаёт:

1. `GraphicsDevice`;
2. `ShaderCompiler`;
3. `ShaderLibrary`;
4. `TextureLibrary`;
5. `MaterialLibrary`.
6. `RenderingStatistics`;
7. `ImGuiOverlay`.

В `OnInitialize()` overlay создаёт input context и официальный
Silk.NET-контроллер Dear ImGui.

В `OnUpdate()` начинается новый ImGui frame и обновляется сглаженное время
кадра.

В `OnRender()` для каждого включённого `GameView`:

1. активируется target;
2. задаётся clear color;
3. очищается color buffer;
4. рассчитывается простая aspect correction;
5. items сортируются по phase;
6. material активирует shader, textures и uniforms;
7. задаётся `uTransform`;
8. mesh выполняет draw call;
9. target добавляется в список для presentation.

После всех views `ImGuiOverlay` рисует окно статистики поверх backbuffer.
Затем каждый использованный target получает `Present()`.

В `OnShutdown()`:

1. render items освобождают ссылки на материалы;
2. views удаляются;
3. material cache очищается;
4. при уничтожении приватного scope освобождаются ImGui, GPU textures,
   shader programs и `GraphicsDevice`.

Это происходит до `GraphicsDevice.Dispose()` и уничтожения окна.

### Временный debug UI

Для статистики используется
`Silk.NET.OpenGL.Extensions.ImGui` версии `2.23.0`. Это официальный адаптер
Silk.NET для Dear ImGui и OpenGL. Он выбран как временный immediate-mode
debug UI: интеграция занимает один update/render pass и не требует строить
виджеты, layout и input routing движка заранее.

`ImGuiOverlay` зарегистрирован только внутри приватного scope
`RenderingModule`. Наружу экспортируется лишь `RenderingStatistics`, причём
через свойство `IRenderer`; Game-код не зависит от ImGui API. Поэтому
будущая собственная UI-система сможет заменить overlay без изменения
материалов, views и render phases.

Окно сейчас показывает:

- сглаженные FPS;
- frame time;
- количество активных views;
- количество отправленных render items;
- количество draw calls.

## 5. Как OpenGL строит изображение

OpenGL — state machine. Большинство вызовов не принимает все данные
рисования напрямую. Вместо этого код по очереди активирует состояние:

```text
какой framebuffer?
какой viewport?
какой shader program?
какие textures в каких units?
какие uniforms?
какой VAO?
какие render states?
```

Затем `DrawElements()` говорит: «используй всё текущее состояние и нарисуй».

### 5.1 OpenGL context

Context хранит OpenGL state и связь с драйвером.

Без current context нельзя надёжно:

- создавать buffers;
- компилировать shaders;
- загружать textures;
- рисовать;
- удалять GPU-ресурсы.

Если рендеринг станет многопоточным, context должен быть current на render
thread. Просто передать `GL` в другой поток недостаточно.

### 5.2 Framebuffer и viewport

Framebuffer — набор изображений, куда попадает результат.

Обычно есть:

- color attachment;
- depth attachment;
- иногда stencil attachment.

В текущем backbuffer color/depth создаются оконной системой.

`Viewport(x, y, width, height)` задаёт, в какую область target переводить
нормализованные координаты.

`ClearColor()` только сохраняет выбранный цвет в state.

Реальная очистка происходит при:

```csharp
gl.Clear(ClearBufferMask.ColorBufferBit);
```

Если появится depth buffer, понадобится:

```csharp
gl.Clear(ColorBufferBit | DepthBufferBit);
```

### 5.3 Vertex shader

Vertex shader запускается один раз для каждой вершины, которую потребовал
draw call.

В текущем shader:

```glsl
layout(location = 0) in vec2 aPosition;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 uTransform;

out vec2 vTexCoord;

void main()
{
    gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
    vTexCoord = aTexCoord;
}
```

Связь с `Mesh`:

```text
VAO location 0 -> aPosition
VAO location 1 -> aTexCoord
```

`gl_Position` обязателен. Это положение вершины в clip space.

### 5.4 Clip space и NDC

После vertex shader координата имеет четыре компонента:

```text
(x, y, z, w)
```

GPU выполняет perspective divide:

```text
NDC = (x/w, y/w, z/w)
```

В OpenGL видимая область NDC:

```text
x: -1 .. +1
y: -1 .. +1
z: -1 .. +1
```

Текущий renderer не имеет камеры и projection matrix. `uTransform`
помещает quad прямо в clip space.

Aspect correction масштабирует X на:

```text
target.Height / target.Width
```

Поэтому квадрат не растягивается в прямоугольник в окне с соотношением
сторон 4:3 или 16:9.

### 5.5 Сборка примитивов

`DrawElements(Triangles, 6, UnsignedInt, 0)` читает индексы:

```text
0, 1, 2 — первый треугольник
2, 3, 0 — второй треугольник
```

После vertex shader GPU собирает результаты по три вершины.

### 5.6 Clipping и rasterization

Треугольники за пределами clip volume обрезаются.

Оставшаяся геометрия rasterizer превращает в fragments — кандидаты на
пиксели.

Для каждого fragment интерполируются выходы vertex shader. Поэтому
`vTexCoord` плавно меняется по поверхности квадрата.

### 5.7 Fragment shader

Fragment shader запускается для каждого fragment:

```glsl
in vec2 vTexCoord;

uniform sampler2D uTexture;
uniform vec4 uTint;

out vec4 oColor;

void main()
{
    oColor = texture(uTexture, vTexCoord) * uTint;
}
```

`texture()`:

1. смотрит номер texture unit в `uTexture`;
2. берёт привязанную к unit `Texture2D`;
3. использует `vTexCoord`;
4. применяет filtering/wrap;
5. возвращает sampled RGBA.

Затем цвет умножается на `uTint`.

### 5.8 Per-fragment tests

После fragment shader OpenGL может выполнить:

- scissor test;
- stencil test;
- depth test;
- blending.

В текущем renderer они явно не настроены, поэтому используется исходное
default state. Для двух непересекающихся непрозрачных квадратов этого
достаточно.

Для 3D понадобятся минимум:

```csharp
gl.Enable(EnableCap.DepthTest);
gl.DepthFunc(DepthFunction.Less);
```

Для прозрачности:

```csharp
gl.Enable(EnableCap.Blend);
gl.BlendFunc(
    BlendingFactor.SrcAlpha,
    BlendingFactor.OneMinusSrcAlpha);
```

### 5.9 Запись и presentation

Прошедший tests fragment записывается в color attachment.

Рисование происходит в backbuffer. После завершения кадра:

```csharp
window.SwapBuffers();
```

готовое изображение становится frontbuffer и отображается.

## 6. Точная последовательность текущего draw call

Для одного `RenderItem` порядок выглядит так.

### Подготовка target

```text
window.MakeCurrent()
gl.Viewport(...)
gl.ClearColor(...)
gl.Clear(ColorBufferBit)
```

### Подготовка material

```text
ShaderLibrary.Get(shaderAsset)
gl.UseProgram(program)
```

Для каждого texture parameter:

```text
TextureLibrary.Get(textureAsset)
gl.ActiveTexture(Texture0 + slot)
gl.BindTexture(Texture2D, texture)
gl.GetUniformLocation(program, samplerName)
gl.Uniform1(location, slot)
```

Для `uTint`:

```text
gl.GetUniformLocation(program, "uTint")
gl.Uniform4(location, r, g, b, a)
```

### Подготовка object

```text
gl.GetUniformLocation(program, "uTransform")
gl.UniformMatrix4(...)
```

### Подготовка geometry и draw

```text
gl.BindVertexArray(vao)
gl.DrawElements(Triangles, 6, UnsignedInt, 0)
```

### Завершение target

После всех items:

```text
window.SwapBuffers()
```

Нельзя, например, вызвать `DrawElements()` до `UseProgram()` или без
настроенного VAO: draw call использует текущее состояние и не знает, что код
«собирался» активировать позже.

## 7. Что задаётся один раз, а что каждый кадр

### Один раз при создании Mesh

- создание VAO/VBO/EBO;
- загрузка вершин и индексов;
- описание vertex layout.

### Один раз при первой загрузке Shader

- compile vertex stage;
- compile fragment stage;
- link program.

### Один раз при первой загрузке Texture

- создание texture handle;
- загрузка pixels;
- настройка filtering/wrap.

### Для каждого материала перед draw

- `UseProgram`;
- bind textures к units;
- material uniforms.

### Для каждого объекта перед draw

- object transform;
- bind VAO;
- draw call.

### Для каждого view

- bind target;
- viewport;
- clear;
- camera/view data в будущем;
- presentation после всех draws.

## 8. Как связаны два демонстрационных квадрата

Оба item используют один `Mesh`, один `ShaderAsset` и одну `TextureAsset`:

```text
RenderItem Blue ─┐
                 ├── Mesh Quad
RenderItem Warm ─┘

checker-blue.material ─┐
                       ├── textured.glsl
checker-warm.material ─┤
                       └── checker.ppm
```

Различаются:

- `AssetRef<MaterialAsset>`;
- значение `uTint`;
- transform.

GPU shader и texture не создаются дважды, потому что libraries кэшируют их
по `AssetId`.

Каждый item всё же выполняет отдельный draw call. Позже одинаковую геометрию
можно рисовать instancing-ом.

## 9. Как добавить новый параметр материала

Для существующих типов достаточно добавить uniform в shader и YAML.

GLSL:

```glsl
uniform float uBrightness;
```

```glsl
oColor = texture(uTexture, vTexCoord) * uTint * uBrightness;
```

YAML:

```yaml
parameters:
  uBrightness:
    float: 1.5
```

`Material.Bind()` распознает `FloatMaterialParameter` и вызовет
`shader.Set(name, value)`.

Для нового типа, например `Vector3`:

1. добавить новый `MaterialParameter`;
2. расширить YAML descriptor;
3. импортировать значение в `MaterialAssetImporter`;
4. добавить overload `Shader.Set(Vector3)`;
5. обработать тип в `Material.Bind()`.

## 10. Как добавить новую геометрию

Сейчас `Mesh` жёстко ожидает layout:

```text
location 0: vec2 position
location 1: vec2 UV
```

Чтобы добавить цвет или normal, должны согласованно измениться:

1. структура vertex data;
2. stride;
3. offsets в `VertexAttribPointer`;
4. locations в vertex shader.

Например:

```text
position vec3 — 12 байт
normal   vec3 — 12 байт
uv       vec2 — 8 байт
stride        — 32 байта
```

```glsl
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
```

Следующий архитектурный шаг — создать `VertexLayout`, чтобы `Mesh` не был
жёстко связан с одним форматом.

## 11. Частые ошибки OpenGL

### Чёрный экран

Проверить:

1. context создан и current;
2. viewport не равен нулю;
3. framebuffer очищается ожидаемым цветом;
4. shader compile/link успешен;
5. `UseProgram()` вызван;
6. VAO привязан;
7. attributes совпадают с shader locations;
8. draw count не равен нулю;
9. transform оставляет геометрию в clip space;
10. `SwapBuffers()` вызван.

### Текстура чёрная

Проверить:

1. texture успешно создана;
2. texture привязана к ожидаемой unit;
3. sampler uniform содержит номер unit, а не texture handle;
4. UV находятся в ожидаемом диапазоне;
5. имя uniform совпадает с YAML;
6. shader program активен во время `Uniform1`.

### Геометрия искажена

Проверить:

1. stride;
2. attribute offsets;
3. размер components;
4. тип данных;
5. порядок матриц;
6. transpose при передаче матрицы;
7. aspect ratio.

### Shader uniform всегда `-1`

Причины:

- опечатка в имени;
- uniform отсутствует в shader;
- uniform объявлен, но не используется и удалён оптимизатором;
- запрашивается location не у того program.

### После reload всё падает

Нельзя удалять старый GPU-ресурс до успешного создания нового. Текущие
`ShaderLibrary` и `TextureLibrary` сначала создают replacement и только затем
удаляют предыдущий объект.

## 12. Текущие ограничения и разумные следующие шаги

Система намеренно небольшая. Сейчас отсутствуют:

- camera и полноценные view/projection matrices;
- depth buffer management;
- blending и реальные state changes между фазами;
- culling;
- сортировка opaque/transparent по расстоянию;
- uniform location cache;
- mipmaps;
- anisotropic filtering;
- sampler objects;
- framebuffer/offscreen render target;
- batching и instancing;
- command buffer на один кадр;
- vertex layout abstraction;
- автоматический file watcher;
- dependency graph для reload;
- стабильный registry, переживающий rename.

Рекомендуемая последовательность развития:

1. добавить `CameraData` в `GameView`;
2. добавить depth test и depth clear;
3. сделать `RenderState` для каждой phase/material;
4. добавить `VertexLayout`;
5. реализовать offscreen framebuffer target;
6. добавить uniform location cache;
7. добавить file watcher и dependency propagation;
8. затем переходить к batching/render graph.

## 13. Краткая памятка

Чтобы OpenGL нарисовал объект, должны быть готовы и активны:

```text
1. Current context
2. Render target / framebuffer
3. Viewport
4. Shader program
5. Textures в texture units
6. Uniforms
7. VAO с vertex layout и EBO
8. Render state
9. Draw call
10. Present / SwapBuffers
```

В терминах текущего Vecxy:

```text
BackbufferRenderTarget.Bind()
Material.Bind()
Shader.Set("uTransform")
Mesh.Draw()
BackbufferRenderTarget.Present()
```

Assets отвечают на вопрос «какие данные описаны файлами?».

Rendering libraries отвечают на вопрос «какие GPU-ресурсы соответствуют этим
данным?».

`GameView` отвечает на вопрос «что и куда рисовать?».

`RenderingModule` отвечает на вопрос «в каком порядке активировать состояние
и выполнить draw calls?».
