# Чистая сцена для работы в Blender

Открой **[Sandbox.blend](Sandbox.blend)**. Аддон установлен: **N → Vecxy**.
Если Blender уже открыт со старым аддоном, перезапусти его перед открытием файла.
Одна единица — метр, вертикаль — Z. Масштаб обеих машинок равен 1.

В игре: пол 16 × 16 м, две новые стиральные машины, дождь и управление от первого
лица. Машинка имеет обычный бытовой размер: примерно 60 × 70 × 88 см.
Постобработка выключена. Дождь и его звук создаются отдельно игровым кодом.

## Иерархия

```text
Sandbox
├─ 01_Environment
│  └─ Floor                    плейн + компонент Box Collider
├─ 02_Instances
│  ├─ Washer_01                ссылка на Washer_Prototype; Number = 1
│  └─ Washer_02                ссылка на Washer_Prototype; Number = 2
├─ 03_Lighting
│  ├─ KeyLight                 направленный свет
│  └─ FillLight                мягкая подсветка спереди
├─ 04_Markers
│  └─ PlayerSpawn              ноги игрока; локальная +Y — направление взгляда
└─ 90_PrototypeLibrary         исключена из расстановки
   └─ Washer_Prototype         одна общая модель и общие компоненты
      └─ Washer_Model
         ├─ 01_Cabinet
         ├─ 02_Door_and_drum
         ├─ 03_Controls
         └─ 04_Feet
```

В `01_Environment` добавляй обычную геометрию окружения, в `02_Instances` —
экземпляры переиспользуемых моделей. Свет держи в `03_Lighting`, игровые точки —
в `04_Markers`. Библиотека содержит исходники моделей; её объекты сами по себе
не являются расстановкой. Её не нужно включать во вьюпорте ради редактирования.
Внутри файла есть текст **START HERE — Scene guide** с той же памяткой.

## Изменить машинку

1. Выбери `Washer_01` в `02_Instances`.
2. **Edit original prototype** изолирует сам исходный прототип, как `/` в Blender.
3. Выбери деталь, перейди в Edit Mode и измени меш. Обе копии используют общие данные.
4. В Object Mode нажми **Return to scene**.
5. Сохрани `.blend` и нажми **Export scene USD**.

Остальная сцена временно скрыта. Редактируется исходник в его собственном масштабе,
без копий и без применения масштаба экземпляра. Новые детали добавляются прямо
в коллекцию прототипа. Return to scene восстанавливает вид сцены. Материалы общие.
**Create linked copy** или Shift+D у объекта `Washer_01` создаёт ещё один экземпляр.
Положение и поворот каждого экземпляра независимы.

## Общие настройки и переопределения

**Common settings (all copies)** показывает компоненты прототипа: **Box Collider**
и **Стиральная машина**. Их значения наследуют все экземпляры.
Без этой галочки панель показывает выбранную копию. Нажми **Override** у компонента,
затем включи галочку только у нужного поля. В примере у копий переопределён только
номер машинки; питание и время цикла общие. Выключенная галочка восстанавливает
наследование поля. **Reset instance overrides** удаляет все настройки копии.

Компоненты добавляются из списка: **Physics / Box Collider**, **Sphere Collider**,
**Capsule Collider**, **Rigid Body** и компоненты игры. **Add selected component**
добавляет выбранный тип. У коллайдера есть **Fit to model** и проволочный preview;
отдельные объекты для физической формы не нужны. Center/Size заданы в локальных
координатах владельца. У плейна коллайдер находится под поверхностью, поэтому
видимый верх пола и опора игрока совпадают.

## Добавить свой компонент из C#

Пример целиком — [LaundryMachine.cs](../LaundryMachine.cs).
На типе компонента ставится `EditorComponent`, на свойствах вложенного
`Prototype.Options` — `EditorField`:

```csharp
[EditorComponent("Мой компонент", Category = "Gameplay", Key = "myComponent")]
// public sealed class MyComponent : AComponent ...

// В Prototype.Options:
[EditorField("Скорость", Min = 0, Unit = "m/s")]
public float Speed { get; set; } = 2;
```

Выполни сборку C#, затем **Reload project components** в Blender. Каталог
`Authoring/vecxy.components.json` создаётся автоматически из проекта и его
зависимостей: стандартные компоненты движка и игровые типы находятся в одном списке.
Аддон читает его при открытии `.blend`. Поля имеют названия, типы и defaults из C#;
поддержаны float/double, int, bool, string, Vector3 и enum.

## Как связаны файлы

```text
Authoring/Sandbox.blend                  редактируемый исходник
    ↓ Export scene USD
Assets/Scenes/Sandbox.usda                пол, свет, spawn, две ссылки на модель
    └─ Prototypes/Washer.usda             одна геометрия и общие компоненты
    ↓ автоматическая подготовка при экспорте / сборке
Assets/Scenes/Compiled/Sandbox.glb        игровой render-кеш
Assets/Scenes/Sandbox.usda.usdscene       иерархия и компоненты для движка
    ↓ IPrototypes
Assets/Prototypes/Sandbox.prototype      создаёт всю USD-сцену
MainScene.cs                            добавляет игрока и дождь
```

**Export this prototype USD** обновляет только `Washer.usda`. Сцена продолжает
ссылаться на этот файл; после подготовки кеша обе копии получают изменения.
Для нового общего объекта выбери его иерархию → **Make shared prototype**,
задай имя и отдельный USD-путь в Common settings, создавай связанные копии.

Кеши не редактируй вручную. Рабочий исходник — `.blend`. `Tools/create_sandbox.py`
создаёт стартовую сцену заново: после начала своей работы его запускать не нужно.
`Tools/generate_rain.py` пересоздаёт только геометрию дождя.

## Запуск и инструменты

Из корня репозитория:

```bash
PATH="$PWD/.agents/local/dotnet:$PATH" dotnet run --project Horror/Horror.csproj
```

Экспорт без окна Blender:

```bash
blender --background Horror/Authoring/Sandbox.blend --python Horror/Tools/export_usd.py
```

Для другой машины: Blender 5.2+, .NET SDK 10.0.110 и OpenUSD Python:

```bash
python3 -m venv .agents/local/usd-env
.agents/local/usd-env/bin/pip install -r Engine/Vecxy/Code/Vecxy.Usd/Tools/requirements.txt
python3 Engine/Vecxy/tools/Blender/package_addon.py
```

Установи `.agents/local/vecxy_usd.zip` через Preferences → Add-ons → Install from Disk.
Панель автоматически находит Python в `.agents/local/usd-env`; можно указать свой
в поле OpenUSD Python. Запущенной игре Python не нужен.
