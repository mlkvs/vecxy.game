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
Один универсальный атрибут `Authoring` ставится и на класс компонента, и на
свойства вложенного `Prototype.Options`:

```csharp
[Authoring("Мой компонент", Category = "Gameplay", Key = "myComponent")]
// public sealed class MyComponent : AComponent ...

// В Prototype.Options:
[Authoring("Скорость", Min = 0, Unit = "m/s")]
public float Speed { get; set; } = 2;
```

Выполни `vecxy -p Horror generate` или обычную сборку C#. Открытый Blender
автоматически перечитает изменившийся каталог; кнопка **Reload project components**
позволяет обновить его вручную. Каталог
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

## Плашка UI над машинкой

В `05_UI` находится **Washer_01_UI_Area** — плейн, определяющий прямоугольную область.
В Blender он отображается проволочным контуром; в игре его меш не рисуется.
На нём компонент **UI / UI Canvas**: Document выбирается поиском по ассетам,
Render mode = WorldSpace. Изменение положения, поворота или размера меняет область UI.
Этот объект расположен отдельно от instanceable USD-прототипа машинки.

Рисуется обычный документ `Assets/UI/WasherStatus.xml`, стили — `WasherStatus.css`.
Шрифт Noto Sans подключён через `@font-face`, TTF и лицензия находятся в `Assets/Fonts`.
Скругления, Flex, текст и hot reload — те же, что в экранном UI. XML/CSS можно править
при запущенной игре. Сам UI Canvas не требует коллайдера.

В поле Document сохраняется стабильный AssetId, поэтому переименование XML через
обычный asset pipeline не ломает ссылку. `Pixels per metre` задаёт плотность раскладки,
а физический размер приходит из плейна. Interactive включает hover и события кнопок.
При выборе ScreenOverlay тот же документ становится экранным интерфейсом.

После изменения плейна: Ctrl+S → Export scene USD → перезапуск игры.
В файле есть текст **World UI — Guide**. `Tools/add_world_ui.py` повторно добавляет или
обновляет только этот пример, сохраняя остальные объекты существующей сцены.

### Взаимодействие с World UI

В коллекции `05_UI` две области без видимого меша в игре:

- `Washer_01_UI_Area`: центральная точка → кнопка → ЛКМ или **E**. Включает/выключает питание первой стиралки.
- `Washer_02_UI_Area`: при наведении курсор свободно перемещается по панели, камера не вращается. ЛКМ/**E** меняют цвет и счётчик. За краем курсор исчезает и возвращается управление камерой. Для повторного входа отведите взгляд и наведите снова.

В компоненте **UI Canvas** доступны **Pointer mode**, **Activation key**, **Cursor image**, **Cursor size**.
Иконку можно выбрать из текстур проекта; hotspot — левый верхний угол. Настройки сохраняются в USD.
При свободной мыши (**Esc**) используется её обычная экранная позиция.
`Assets/UI/Crosshair.xml` задаёт оверлей-точку, `WorldUiDemo.cs` — действия кнопок и physics raycast для препятствий.
