# Изменённые файлы

Добавлена самостоятельная игра в `DoNotRelease/`, рядом с `Horror/`.

Вне нового каталога:

- `vecxy.game.sln`: добавлены только два проекта DoNotRelease и DoNotRelease.Tests; существующие записи не переставлялись.
- `Engine/Vecxy/Code/Vecxy.Platforms/PlatformContext.cs`: четыре строки загрузчика VPack, устраняющие исключение на повторяющихся путях конфигураций игры и движка. Правило выбора совпадает с существующим AssetRegistry. Дополнительно исправлен перенос подключённого diffuseColor в USD-cooker (`Vecxy.Usd/Tools/cook_usd.py`), добавлен регрессионный тест в `test_cook_usd.py`: текстуры больше не умножаются на серый fallback.

Фильтрация текстур: `Vecxy.Assets/ModelAsset.cs` читает sampler из GLB, `Vecxy.Rendering/RenderingModule.cs` передаёт его материалам. `tools/Blender/vecxy_usd/exporter.py`, `finalize_usd.py` и USD-cooker сохраняют Closest из Blender. Обновлены установленный аддон, USD-кеш игры и проверки.

Переход на авторинг через Blender/USD:

- `Authoring/Cell.blend`: вся локация, иерархии мебели, коллизии, свет, компоненты взаимодействий, UI Canvas, начальная видимость и игровые роли.
- `Authoring/vecxy.components.json`, `CellAuthoring.cs`: компоненты и каталог аддона.
- `Assets/Scenes/Cell.usda`, `Textures/`, `Cell.usda.usdscene`, `Compiled/Cell.glb`: экспорт USD и штатный кеш движка.
- `Assets/Prototypes/Cell.prototype`, `Program.cs`, `CellScene.cs`, `DoNotRelease.csproj`: подключение UsdModule и загрузка сцены штатным IPrototypes.
- `RoomController.cs`, `PlayerController.cs`, `GameSession.cs`, `InteractionSystem.cs`: использование компонентов и поз из USD вместо координат локации в коде.
- `Gameplay/Content.cs`, `Assets/Configs/Story.yaml`, `Tools/create_content.py`: удалены массивы статической расстановки и коллизий.
- `Tools/export_usd.py`, `build.sh`: экспорт аддоном и подготовка USD до упаковки VPack.
- `Tests/validate_assets.py`, `Tests/validate_authoring.py`: проверки USD и переноса мебели из Blender.
- `README.md`, `Authoring/README.md`, `Docs/TEST_REPORT.md`: инструкция и результаты проверок.
- Удалены старый прямой `Assets/Models/Cell.glb` и генератор локации `Tools/create_cell.py`.

Содержимое новой игры (кроме игнорируемых Build, bin, obj и резервных копий Blender):

```text
.gitignore
Assets.manifest
Assets/Audio/Breathing.wav
Assets/Audio/Cloth.wav
Assets/Audio/Distant.wav
Assets/Audio/Ending.wav
Assets/Audio/Equipment.wav
Assets/Audio/Hatch.wav
Assets/Audio/Knock.wav
Assets/Audio/Light.wav
Assets/Audio/Lock.wav
Assets/Audio/Room.wav
Assets/Audio/Speaker.wav
Assets/Audio/Step0.wav
Assets/Audio/Step1.wav
Assets/Audio/Step2.wav
Assets/Audio/Step3.wav
Assets/Audio/Tap.wav
Assets/Audio/Tray.wav
Assets/Audio/Voice/ack.wav
Assets/Audio/Voice/agreement.wav
Assets/Audio/Voice/autoplay.wav
Assets/Audio/Voice/available.wav
Assets/Audio/Voice/calm.wav
Assets/Audio/Voice/conflict.wav
Assets/Audio/Voice/denied.wav
Assets/Audio/Voice/denis.MODEL_CARD.txt
Assets/Audio/Voice/dmitri.MODEL_CARD.txt
Assets/Audio/Voice/night.wav
Assets/Audio/Voice/respect.wav
Assets/Audio/Voice/routine.wav
Assets/Audio/Voice/statement01.wav
Assets/Audio/Voice/statement02.wav
Assets/Audio/Voice/statement03.wav
Assets/Audio/Voice/statement04.wav
Assets/Audio/Voice/statement05.wav
Assets/Audio/Voice/statement06.wav
Assets/Audio/Voice/statement07.wav
Assets/Audio/Voice/statement08.wav
Assets/Audio/Voice/statement09.wav
Assets/Audio/Voice/statement10.wav
Assets/Audio/Voice/statement11.wav
Assets/Audio/Voice/statement12.wav
Assets/Audio/Voice/statement13.wav
Assets/Audio/Voice/statement14.wav
Assets/Audio/Voice/statement15.wav
Assets/Audio/Voice/statement16.wav
Assets/Audio/Voice/statement17.wav
Assets/Audio/Voice/statement18.wav
Assets/Audio/Water.wav
Assets/Configs/Physics.yaml
Assets/Configs/Story.yaml
Assets/Configs/UI.yaml
Assets/Configs/Voices.yaml
Assets/Controls.input
Assets/Fonts/NotoSans.OFL.txt
Assets/Fonts/NotoSans.ttf
Assets/PostProcessing/Retro.postfx
Assets/PostProcessing/Vignette.postfx
Assets/Prototypes/Cell.prototype
Assets/Scenes/Cell.usda
Assets/Scenes/Cell.usda.usdscene
Assets/Scenes/Compiled/Cell.glb
Assets/Scenes/Textures/00dd458a6e7b_Cast concrete • 64px.png
Assets/Scenes/Textures/4ff27e5d776c_Off white ceramic • 64px.png
Assets/Scenes/Textures/5b9d7bea0f34_Rubber floor • 64px.png
Assets/Scenes/Textures/5e049da533a9_Meal • 64px.png
Assets/Scenes/Textures/7f3343d3a659_Blackened steel • 64px.png
Assets/Scenes/Textures/93b9d1cafd76_Bread • 64px.png
Assets/Scenes/Textures/c8632d801aa8_Brushed stainless • 64px.png
Assets/Scenes/Textures/efcee403315d_Service enamel — faded sage • 64px.png
Assets/Scenes/Textures/f181ec2c0702_Institutional cotton • 64px.png
Assets/Scenes/Textures/fec3b30583ca_Document stock • 64px.png
Assets/Shaders/PostProcessing/Retro.glsl
Assets/UI/Archive.css
Assets/UI/Archive.xml
Assets/UI/Game.css
Assets/UI/Game.xml
Assets/UI/Indicator.css
Assets/UI/Indicator.xml
Authoring/.gitignore
Authoring/Cell.blend
Authoring/README.md
Authoring/vecxy.components.json
CellAuthoring.cs
CellScene.cs
DoNotRelease.csproj
Docs/CHANGES.md
Docs/CREDITS.md
Docs/TEST_REPORT.md
GameSession.cs
GameSettings.cs
Gameplay/Content.cs
Gameplay/DialogueQueue.cs
Gameplay/GameFlow.cs
Generated/Assets.g.cs
InteractionSystem.cs
PlayerController.cs
Program.cs
README.md
RecordingPlayer.cs
RoomController.cs
Soundscape.cs
Tests/DoNotRelease.Tests.csproj
Tests/Program.cs
Tests/validate_assets.py
Tests/validate_authoring.py
Tools/create_content.py
Tools/export_usd.py
Tools/generate_audio.py
Tools/package.sh
Tools/requirements.txt
build.sh
run.sh
```

Готовые артефакты: `Build/Linux-x64/`, `Build/NeVypuskat-linux-x64.tar.gz` и контрольная сумма `.sha256`. Логи и снимки проверок хранятся в `.agents/local/donotrelease/usd-migration/`.
