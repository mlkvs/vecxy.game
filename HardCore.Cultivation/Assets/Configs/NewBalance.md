# Задача

Необходимо переработать существующую систему игрового баланса так, чтобы математическая модель из Excel-файла баланса была корректно интегрирована в уже существующую YAML-конфигурационную архитектуру игры.

Не нужно переносить Excel «один в один» в YAML и тем более не нужно сохранять в конфиге все рассчитанные числа.

Основной принцип:

> **В YAML храним исходные балансные параметры.
> В C# реализуем универсальные формулы.
> Производные значения вычисляем автоматически.**

Например:

* коэффициент стадии `0.70` — YAML;
* формула `next = k * (prev + prevPrev)` — C#;
* рассчитанные `210`, `287`, `347.9` — НЕ YAML;
* бонус `+5 HP за уровень` — YAML;
* формула накопления характеристик — C#;
* итоговые `150 HP` в конце стадии — НЕ YAML.

---

# 1. Перед началом реализации

Сначала изучить существующий код проекта и найти реальные классы, отвечающие за:

* загрузку `GameBalance.yaml`;
* загрузку `Cultivation.yaml`;
* загрузку `Combat.yaml`;
* загрузку `Alchemy.yaml`;
* загрузку `Items.yaml`;
* загрузку `Rarities.yaml`;
* загрузку `Missions.yaml`;
* загрузку `Monsters.yaml`;
* генерацию `ItemInstance`;
* расчёт качества предмета;
* расчёт редкости;
* расчёт цены;
* тик системы;
* накопление Spiritual Power;
* повышение уровня культивации;
* попытку прорыва;
* эффекты предметов;
* постоянные эффекты;
* временные эффекты;
* генерацию миссий;
* генерацию врагов;
* автобой;
* сохранение/загрузку текущей стадии и уровня.

Не создавать параллельную систему поверх существующей.

Существующие сервисы, DTO, конфиги и пайплайн загрузки нужно расширить или заменить там, где это необходимо.

---

# 2. Главный архитектурный принцип

Разделить систему на три уровня.

## 2.1. Configuration data

YAML содержит значения, которые могут изменяться при балансировке:

* коэффициенты;
* базовые значения;
* кривые;
* веса;
* приросты;
* бонусы;
* пороги;
* матрицы;
* длительности;
* вероятности;
* базовые цены.

## 2.2. Balance math

C# содержит формулы:

* стоимость уровня;
* стоимость предмета;
* характеристики персонажа;
* характеристики врага;
* совместимость ингредиентов;
* длительность прокачки;
* DPS;
* модификаторы.

## 2.3. Runtime state

Save/GameState содержит только состояние конкретной игры:

* текущая стадия;
* текущий уровень;
* текущий Spiritual Power;
* HP;
* возраст;
* предметы;
* их качество;
* их редкость;
* загрязнение конкретного экземпляра;
* активные эффекты;
* постоянные бонусы;
* текущие миссии.

Нельзя сохранять в GameState рассчитанные балансные таблицы вроде стоимости всех будущих уровней.

---

# 3. Что НЕ хранить в YAML

Не добавлять туда:

```text
requiredPowerLevel1
requiredPowerLevel2
requiredPowerLevel3
...
requiredPowerLevel70
```

Не хранить:

* итоговый DPS;
* время лечения;
* время прохождения стадии;
* HP персонажа в конце стадии;
* характеристики слабого врага;
* характеристики среднего врага;
* характеристики сильного врага;
* готовую цену Rare/Legendary предмета;
* полные строки рангов `E-`, `E`, `E+`.

Всё это derived data.

---

# 4. Критические расхождения существующих конфигов с Excel

Необходимо учитывать, что сейчас в игре уже реализована другая математика.

## Cultivation.yaml

Сейчас:

```text
requiredPower =
    baseRequiredPower
    * levelMultiplier
    * stageMultiplier
```

Excel использует совершенно другую рекуррентную модель.

Старую формулу необходимо заменить.

---

## Combat.yaml

Сейчас:

```text
stat =
    base
    + completedLevels * perLevel
    + stageIndex * perStage
```

Excel задаёт:

* отдельные приросты за уровень для каждой стадии;
* отдельный большой бонус за успешный прорыв.

Для HP / Regen / Attack / AttackSpeed старую линейную модель необходимо заменить.

---

## Cultivation stages

Сейчас в YAML 8 стадий:

```text
body_tempering
qi_gathering
foundation
golden_core
nascent_soul
soul_formation
void_refinement
immortal_ascension
```

В Excel 7 стадий:

```text
Закалка тела
Сбор Ци
Золотое ядро
Зарождающаяся душа
Формирование души
Очищение пустоты
Вознесение бессмертного
```

`foundation` в Excel отсутствует.

Целевая активная последовательность должна стать:

```text
body_tempering
qi_gathering
golden_core
nascent_soul
soul_formation
void_refinement
immortal_ascension
```

Перед удалением `foundation` проверить сохранения и serialization.

Не полагаться на числовой ordinal enum стадии.

Стадии должны идентифицироваться стабильным `id`.

Если требуется совместимость со старыми development-save, сделать migration отдельно.

---

# 5. Новая модель Cultivation.yaml

Убрать из основной новой формулы:

```yaml
baseRequiredPower
levelMultipliers
stageMultiplier
```

Они относятся к старой системе.

Добавить глобальные параметры:

```yaml
initialRequiredPower:
  - 100
  - 200

stageEntryCoefficient: 0.70
```

`initialRequiredPower` — первые две стоимости первой стадии.

`stageEntryCoefficient` используется для первых двух значений каждой следующей стадии.

---

# 6. Конфигурация каждой стадии

Каждая стадия должна содержать примерно следующие balance-параметры:

```yaml
- id: body_tempering
  name: Закалка тела

  cultivationBackgroundTexture: ...
  missionBackgroundTexture: ...

  recursiveCoefficient: 0.70
  spiritualPowerMultiplier: 3

  missionRankBase: E

  baseBreakthroughChance: 80

  statsPerLevel:
    maximumHealth: 5
    healthRegeneration: 0.1
    attack: 0.5
    attacksPerSecond: 0.05
    longevityYears: 0.5

  breakthroughBonus:
    maximumHealth: 50
    healthRegeneration: 0.5
    attack: 5
    attacksPerSecond: 0.5
    longevityYears: 12
```

Фоны и `baseBreakthroughChance` сохранить из существующего конфига.

---

# 7. Все коэффициенты стадий

Использовать:

| Stage              | recursiveCoefficient | spiritualPowerMultiplier | Rank |
| ------------------ | -------------------: | -----------------------: | ---- |
| body_tempering     |                 0.70 |                        3 | E    |
| qi_gathering       |                 0.67 |                        6 | D    |
| golden_core        |                 0.64 |                        9 | C    |
| nascent_soul       |                 0.61 |                       13 | B    |
| soul_formation     |                 0.58 |                       18 | A    |
| void_refinement    |                 0.55 |                       40 | S    |
| immortal_ascension |                 0.52 |                       50 | SS   |

Это конфигурационные значения.

Не выводить их формулой `0.70 - 0.03 * stage`.

Несмотря на существующую закономерность, это геймдизайнерские значения и они должны независимо редактироваться.

---

# 8. Формула стоимости культивации

Реализовать отдельный pure calculator, например:

```csharp
ICultivationCostCalculator
```

или аналогичную существующей архитектуре сущность.

## Первая стадия

Начальные значения:

```text
L1 = 100
L2 = 200
```

Для `L3..L10`:

```text
required[level] =
    recursiveCoefficient
    * (
        required[level - 1]
        + required[level - 2]
      )
```

Для первой стадии:

```text
k = 0.70
```

Получается:

```text
100
200
210
287
347.9
444.43
554.631
699.3427
877.78159
1103.987003
```

---

# 9. Начало следующей стадии

Для любой стадии после первой:

```text
L1 =
    stageEntryCoefficient
    * (
        previousStage.L10
        + previousStage.L9
      )
```

Затем:

```text
L2 =
    stageEntryCoefficient
    * (
        currentStage.L1
        + previousStage.L10
      )
```

Начиная с L3:

```text
Ln =
    currentStage.recursiveCoefficient
    * (
        L(n - 1)
        + L(n - 2)
      )
```

Важно:

`stageEntryCoefficient = 0.70`.

Для первых двух значений новой стадии НЕ использовать `recursiveCoefficient` новой стадии.

Это именно логика Excel.

---

# 10. Не округлять balance math

Внутренние расчёты производить через `double`.

Не делать:

```csharp
Math.Round(requiredPower)
```

при каждом уровне.

Иначе ошибка будет накапливаться на следующих стадиях.

Округление допустимо только в UI.

Например:

```text
1 303 661.62
```

можно показывать:

```text
1.30M
```

Но внутренняя модель должна хранить исходное число.

---

# 11. Не исправлять автоматически неравномерность прогрессии

Поздние уровни Excel иногда становятся немного дешевле предыдущего.

Например `void_refinement`:

```text
L1 = 1 064 219.55
L2 = 1 303 661.62
L3 = 1 302 334.65
```

Это следствие исходной рекуррентной формулы.

НЕ добавлять самовольно:

```csharp
Math.Max(calculated, previous * 1.05)
```

Сначала необходимо реализовать Excel точно.

Если затем понадобится монотонная прогрессия — это будет отдельное изменение баланса.

---

# 12. Проверочные значения Cultivation

Unit tests должны проверять минимум следующие числа.

## Body Tempering

```text
L1 = 100
L2 = 200
L3 = 210
L4 = 287
L10 = 1103.987003
```

## Qi Gathering

```text
L1 = 1387.238015
L2 = 1743.857513
L3 = 2097.834004
L10 = 8447.515467
```

## Golden Core

```text
L1 ≈ 10762.193359
L10 ≈ 50234.441809
```

## Immortal Ascension

```text
L1 ≈ 2823368.631655
L2 ≈ 3433404.343543
L3 ≈ 3253521.947103
L10 ≈ 4016909.690882
```

Использовать tolerance для floating point.

---

# 13. Spiritual Power за тик

Существующий:

```yaml
GameBalance.yaml

baseSpiritualPowerPerTick: 1
```

сохранить.

Но добавить stage multiplier.

Формула базовой духовной силы текущей стадии:

```text
stageBasePowerPerTick =
    GameBalance.baseSpiritualPowerPerTick
    * CurrentStage.spiritualPowerMultiplier
```

При текущем base = `1`:

```text
body_tempering       = 3
qi_gathering         = 6
golden_core          = 9
nascent_soul         = 13
soul_formation       = 18
void_refinement      = 40
immortal_ascension   = 50
```

Это соответствует столбцу `Тапы/сек` из Excel, но в архитектуре игры не нужно увеличивать физическую скорость тика.

**Тик по-прежнему остаётся одним игровым тиком.**

Меняется эффективность получения Spiritual Power.

---

# 14. TickEfficiency не должен менять частоту игрового loop

Не делать:

```text
1000 ms / bonus
```

и не ускорять сам update loop.

Правильная идея:

```text
powerForTick =
    stageBasePower
    * tickEfficiency
```

То есть ускоряется результат тика, а не частота симуляции.

---

# 15. Прогноз времени прокачки

Не хранить время в конфиге.

Рассчитывать.

```text
secondsPerTick =
    realMillisecondsPerTick / 1000
```

```text
powerPerRealSecond =
    powerPerTick / secondsPerTick
```

```text
requiredRealSeconds =
    requiredPower / powerPerRealSecond
```

При:

```text
realMillisecondsPerTick = 1000
baseSpiritualPowerPerTick = 1
```

получатся Excel-значения.

Проверочные суммы стадий:

```text
Body Tempering       ≈ 26.805957 минут
Qi Gathering         ≈ 112.411322
Golden Core          ≈ 490.636707
Nascent Soul         ≈ 1729.533953
Soul Formation       ≈ 4898.245542
Void Refinement      ≈ 6585.928072
Immortal Ascension   ≈ 11854.652672
```

Всего:

```text
≈ 25 698.214225 минут
```

без учёта предметов и бафов.

---

# 16. Характеристики персонажа

Excel задаёт базу:

```text
MaximumHealth        = 100
HealthRegeneration   = 1
Attack               = 1
AttacksPerSecond     = 1
Longevity            = 63
```

Поэтому привести базовые значения к этой модели.

Например в `Combat.yaml`:

```yaml
heroBaseHealth: 100
heroBaseAttack: 1
heroAttacksPerSecond: 1
healthRegenerationPerSecond: 1
```

`GameBalance.maximumAgeYears` должен выполнять роль базового долголетия:

```yaml
maximumAgeYears: 63
```

`startingAgeYears` — отдельная характеристика.

Её не заменять на 63.

Возраст персонажа и предел жизни — разные значения.

---

# 17. Defense

Excel не содержит Defense.

Но существующая игра уже использует защиту.

Не выдумывать новые Excel-значения.

Существующую модель:

```yaml
heroBaseDefense
heroDefensePerStage
heroDefensePerLevel
```

временно сохранить отдельно.

Новая Excel-прогрессия относится к:

* HP;
* Regen;
* Attack;
* AttackSpeed;
* Longevity.

Defense остаётся existing-game-only параметром, пока для него не появится отдельный баланс.

---

# 18. Прирост характеристик за уровень

Хранить в `Cultivation.yaml`.

## Body Tempering

```yaml
statsPerLevel:
  maximumHealth: 5
  healthRegeneration: 0.1
  attack: 0.5
  attacksPerSecond: 0.05
  longevityYears: 0.5
```

## Qi Gathering

```text
HP            +10
Regen         +0.25
Attack        +1
AttackSpeed   +0.1
Longevity     +1
```

## Golden Core

```text
HP            +25
Regen         +0.35
Attack        +2
AttackSpeed   +0.15
Longevity     +1.5
```

## Nascent Soul

```text
HP            +50
Regen         +0.75
Attack        +3
AttackSpeed   +0.20
Longevity     +2
```

## Soul Formation

```text
HP            +150
Regen         +2
Attack        +4
AttackSpeed   +0.25
Longevity     +3
```

## Void Refinement

```text
HP            +250
Regen         +4
Attack        +5
AttackSpeed   +0.30
Longevity     +5
```

## Immortal Ascension

```text
HP            +500
Regen         +8
Attack        +10
AttackSpeed   +0.50
Longevity     +7.5
```

---

# 19. Бонусы прорыва

Также хранить в stage config.

После Body Tempering:

```text
HP          +50
Regen       +0.5
Attack      +5
AttackSpeed +0.5
Longevity   +12
```

После Qi Gathering:

```text
+200
+0.75
+10
+0.5
+25
```

После Golden Core:

```text
+750
+2
+20
+0.75
+40
```

После Nascent Soul:

```text
+1500
+5
+40
+1.25
+80
```

После Soul Formation:

```text
+2500
+10
+80
+2
+125
```

После Void Refinement:

```text
+5000
+20
+150
+3
+300
```

Для `immortal_ascension` breakthrough bonus не требуется, если после неё нет следующей стадии.

---

# 20. Не мутировать базовые характеристики при каждом level-up

Предпочтительная архитектура:

```text
FinalCharacterStats =
    CultivationProgressionStats
    + PermanentItemBonuses
    + TemporaryEffects
```

Прогрессионные характеристики должны быть воспроизводимы из:

```text
Stage
Level
```

а не существовать только потому, что однажды был вызван:

```csharp
player.Health += 5;
```

Это значительно упрощает:

* сохранения;
* миграции;
* баланс;
* ресет;
* тестирование.

---

# 21. Расчёт theoretical start/end stats стадии

Сделать:

```csharp
GetStageStartStats(stageIndex)
GetStageEndStats(stageIndex)
```

Формула конца стадии:

```text
End =
    Start
    + StatsPerLevel * 10
```

Следующая стадия:

```text
NextStart =
    PreviousEnd
    + PreviousBreakthroughBonus
```

---

# 22. Проверочные характеристики

## Body Tempering

Начало:

```text
HP      100
Regen   1
Attack  1
Speed   1
Life    63
```

Конец:

```text
HP      150
Regen   2
Attack  6
Speed   1.5
Life    68
```

После breakthrough / начало Qi Gathering:

```text
HP      200
Regen   2.5
Attack  11
Speed   2
Life    80
```

---

## Golden Core start

```text
HP      500
Regen   5.75
Attack  31
Speed   3.5
Life    115
```

## Immortal Ascension start

```text
HP      15000
Regen   113.75
Attack  461
Speed   19.5
Life    775
```

## Immortal Ascension end

```text
HP      20000
Regen   193.75
Attack  561
Speed   24.5
Life    850
```

---

# 23. DPS

Не хранить.

```text
DPS =
    Attack
    * AttacksPerSecond
```

Сделать derived property/calculator.

---

# 24. Full heal time

Не хранить.

```text
FullHealSeconds =
    MaximumHealth
    / HealthRegenerationPerSecond
```

Только если Regen > 0.

---

# 25. Важно про HealthRegeneration

Сейчас `Combat.yaml` явно говорит, что регенерация применяется ВНЕ боя.

Excel содержит Regen также у врагов.

Это конфликт двух моделей.

В рамках этой задачи:

1. реализовать прогрессию Regen;
2. рассчитывать Regen врагов;
3. **не менять молча существующее правило боя**, если текущая система не регенерирует в бою.

Если потребуется Regen непосредственно во время autobattle, это должно быть отдельным осознанным изменением gameplay semantics.

---

# 26. Enemy generation

Сейчас `Monsters.yaml` содержит абсолютные:

```text
maximumHealth
attack
defense
attacksPerSecond
```

а `Combat.yaml` дополнительно содержит:

```text
monsterPowerMultiplier
```

Excel использует другую модель.

Базовая сила противника зависит от текущей стадии игрока и сложности миссии.

---

# 27. Три difficulty profile

В `Combat.yaml` сделать data-driven профили.

Например:

```yaml
dangerLevels:
  - level: 1
    name: Weak
    encounterChancePercent: 35
    statReference: StageStart
    statMultiplier: 0.90
    rankSuffix: "-"

  - level: 2
    name: Medium
    encounterChancePercent: 65
    statReference: StageEnd
    statMultiplier: 0.45
    rankSuffix: ""

  - level: 3
    name: Strong
    encounterChancePercent: 90
    statReference: StageEnd
    statMultiplier: 0.90
    rankSuffix: "+"
```

Существующий `encounterChancePercent` сохранить.

Существующий `monsterPowerMultiplier` после миграции больше не применять.

Иначе получится двойное масштабирование.

---

# 28. Enemy formulas

Weak:

```text
EnemyStats =
    CurrentStageStartStats
    * 0.90
```

Medium:

```text
EnemyStats =
    CurrentStageEndStats
    * 0.45
```

Strong:

```text
EnemyStats =
    CurrentStageEndStats
    * 0.90
```

Применять к:

```text
MaximumHealth
HealthRegeneration
Attack
AttacksPerSecond
```

---

# 29. Ошибка Excel, которую НЕ переносить

В строке Medium для `void_refinement` в Excel есть ошибочная ссылка на характеристики начала стадии.

Общее правило всех остальных стадий:

```text
Medium = StageEnd * 0.45
```

Именно его реализовать.

Для Void Refinement правильный Medium должен быть:

```text
HP:
10000 * 0.45 = 4500

Regen:
93.75 * 0.45 = 42.1875

Attack:
311 * 0.45 = 139.95

AttackSpeed:
16.5 * 0.45 = 7.425
```

Не воспроизводить ошибочные Excel-значения:

```text
3375
24.1875
117.45
6.075
```

---

# 30. Monsters.yaml

Монстр должен в первую очередь описывать контент:

* id;
* name;
* spriteSet;
* selectionWeight;
* визуальный archetype.

Не нужно иметь отдельные абсолютные HP для каждой стадии.

После внедрения stage-scaled enemy generation поля:

```text
maximumHealth
attack
attacksPerSecond
```

должны перестать быть главным источником силы.

Если удалить их сразу рискованно — оставить как legacy fallback на время миграции.

`defense` пока сохранить, потому что Excel не определяет его прогрессию.

---

# 31. Не привязывать animation FPS напрямую к позднему AttackSpeed

Excel в late-game получает:

```text
20+ attacks/sec
```

Если сейчас `attacksPerSecond` напрямую определяет скорость sprite animation, это необходимо разделить:

```text
CombatSimulationAttackRate
```

и:

```text
VisualAttackAnimationRate
```

Simulation может делать условные 20 атак в секунду.

Спрайт не должен пытаться проиграть 20 полных четырёхкадровых анимаций каждую секунду.

Визуальную скорость ограничить/нормализовать существующей animation-системой.

---

# 32. Ранги миссий

Полный ранг миссии не нужно хранить у каждой миссии.

В stage config есть:

```text
E
D
C
B
A
S
SS
```

А difficulty определяет суффикс.

Формула:

```text
rank =
    stage.missionRankBase
    + danger.rankSuffix
```

Получается:

```text
Body:
E-
E
E+

Qi:
D-
D
D+

Golden:
C-
C
C+

Nascent:
B-
B
B+

Soul:
A-
A
A+

Void:
S-
S
S+

Immortal:
SS-
SS
SS+
```

Для безопасной миссии без `dangerLevel` боевой rank можно не показывать.

---

# 33. Missions.yaml

Текущие:

* duration;
* boardWeight;
* rewards;
* possible monsters;
* backgrounds;
* dangerLevel;

сохранить.

Excel пока не содержит достаточно данных для формулы денежных наград миссий.

Поэтому НЕ генерировать новую экономическую формулу награды из воздуха.

`reward.money` остаётся в YAML.

---

# 34. Rarities.yaml

Здесь должен храниться rarity multiplier.

Не вычислять его через:

```csharp
Math.Pow(4, rarityIndex)
```

потому что это balance data и уже существует дополнительная редкость `Transcendent`.

Для Excel-редкостей заменить priceMultiplier.

```yaml
Common:
  priceMultiplier: 1

Uncommon:
  priceMultiplier: 4

Rare:
  priceMultiplier: 16

Epic:
  priceMultiplier: 64

Legendary:
  priceMultiplier: 256

Mythic:
  priceMultiplier: 1024

Divine:
  priceMultiplier: 4096
```

---

# 35. Transcendent

Excel не определяет `Transcendent`.

Не придумывать для неё multiplier молча.

Enum/entry сохранить, чтобы не ломать проект.

До отдельного решения:

```yaml
shopWeight: 0
```

То есть новые Transcendent-предметы временно не генерировать.

Текущее значение `50` нельзя оставлять в активном новом балансе рядом с:

```text
Divine = 4096
```

иначе Transcendent окажется дешевле Divine.

---

# 36. Цена предмета

Формулу хранить в C#:

```text
TruePrice =
    BasePrice
    * QualityPriceMultiplier
    * RarityPriceMultiplier
```

BasePrice находится в:

```text
Items.yaml
```

Rarity multiplier:

```text
Rarities.yaml
```

Quality curve:

```text
GameBalance.yaml
```

---

# 37. BasePrice предметов

Не создавать в коде:

```csharp
IngredientTier1Price = 50;
IngredientTier2Price = 100;
...
```

Уже существует нормальная data-driven система:

```yaml
basePrice: ...
```

Её сохранить.

Excel-строки:

```text
50
100
200
500
1000
```

следует воспринимать как примеры базовых цен ингредиентов/tiers.

Конкретные игровые предметы по-прежнему получают `basePrice` в `Items.yaml`.

---

# 38. Quality price multiplier

В Excel:

```text
Quality 1 = ×1.00
Quality 2 = ×1.25
Quality 3 = ×1.75
Quality 4 = ×2.50
Quality 5 = ×3.50
```

В `GameBalance.yaml` заменить текущую неправильную кривую.

Например:

```yaml
qualityPriceCurve:
  - quality: 1.0
    multiplier: 1.0

  - quality: 2.0
    multiplier: 1.25

  - quality: 3.0
    multiplier: 1.75

  - quality: 4.0
    multiplier: 2.50

  - quality: 5.0
    multiplier: 3.50
```

Между точками — линейная интерполяция.

---

# 39. Исправить существующий конфликт комментария и значения

Сейчас `GameBalance.yaml` утверждает:

```text
quality 2.5 = ×1
quality 5 = ×3
```

но фактический YAML содержит:

```text
2.5 -> 3
5 -> 5
```

После изменения полностью переписать комментарии так, чтобы documentation соответствовала runtime.

---

# 40. Качество 0.1

Excel задаёт отдельные правила.

Для Ingredient:

```text
0.1 quality Common =
BasePrice / 5
```

то есть multiplier:

```text
0.20
```

Для Pill:

```text
0.1 quality Common =
BasePrice / 2
```

то есть:

```text
0.50
```

Это различается по категориям.

Добавить data-driven special points.

Например:

```yaml
lowQualityPriceMultipliers:
  Ingredient:
    quality: 0.1
    multiplier: 0.20

  Pill:
    quality: 0.1
    multiplier: 0.50
```

Для `0.1 < quality < 1` можно интерполировать от category-specific low point до:

```text
quality 1 = ×1
```

Core в Excel не определён.

Не придумывать отдельный special multiplier для Core, если его качество в нормальной генерации всё равно находится в диапазоне 1–5.

---

# 41. Проверка цены

Ingredient:

```text
basePrice = 50
quality = 5
quality multiplier = 3.5
rarity = Common
```

```text
50 * 3.5 * 1 = 175
```

Uncommon:

```text
50 * 3.5 * 4 = 700
```

Rare:

```text
50 * 3.5 * 16 = 2800
```

Divine:

```text
50 * 3.5 * 4096 = 716800
```

Все эти числа должны быть unit tests.

---

# 42. Shop.yaml

Структура в целом уже правильная.

После TruePrice применять магазинную наценку:

```text
BuyPrice =
    TruePrice
    * (1 + BuyMarkupPercent / 100)
```

Продажа:

```text
SellPrice =
    TruePrice
    * (1 + sellAdjustmentPercent / 100)
```

При:

```text
sellAdjustmentPercent = -33
```

игрок получает примерно:

```text
67%
```

истинной стоимости.

Не смешивать shop markup с вычислением базовой стоимости предмета.

---

# 43. Эффекты качества существующих предметов

В `GameBalance.yaml` уже есть отдельная система:

```yaml
effectQualityBase
effectQualityPerPoint
```

Она используется для силы effects в `Items.yaml`.

Excel явно использует quality multiplier в формулах цены, но не содержит достаточно однозначной формулы, которая связывает эту таблицу со всеми existing ItemEffect.

Поэтому в рамках этой миграции:

**не удалять автоматически `effectQualityBase/effectQualityPerPoint`.**

Система цены и система силы обычных ItemEffect должны оставаться раздельными, пока не будет отдельного решения их объединить.

Особенно важно не сломать существующие:

* TickEfficiency;
* AgingSpeed;
* BreakthroughChance;
* SpiritualPowerGain;
* MissionProgress;
* HealthRegeneration.

---

# 44. Alchemy.yaml

Текущую систему алхимии не переписывать полностью.

Она уже содержит:

* 2–5 ингредиентов;
* property matching;
* coreQualityWeight;
* качество;
* distillation;
* potency;
* максимум эффектов;
* CraftedEffect;
* ingredientCharacteristicCoefficient.

Этого нет в Excel в достаточно полном виде.

Следовательно эта логика остаётся существующей игровой механикой.

---

# 45. Существующая алхимическая формула

Сохранить:

```text
ingredientCharacteristicMultiplier =
    quality
    * ingredientCount
    * (
        1
        + ingredientCharacteristicCoefficient
        * ingredientCount
      )
```

При текущем:

```text
ingredientCharacteristicCoefficient = 8
```

Не дублировать quality modifier поверх CraftedEffect повторно.

Существующий комментарий `Items.yaml` уже правильно говорит, что CraftedEffect включает качество и повторная обработка качества для него не применяется.

---

# 46. Стихии ингредиентов

Добавить enum:

```csharp
Fire
Water
Earth
Air
Void
```

В `Items.yaml` у алхимического ингредиента добавить необязательное:

```yaml
element: Fire
```

Не назначать автоматически стихии всем существующим ингредиентам на основании названия.

Если `element` отсутствует — считать ингредиент нейтральным для elemental compatibility.

Позже значения можно расставить вручную через YAML.

---

# 47. Матрица совместимости

Это balance data.

Она должна находиться в `Alchemy.yaml`, а не быть захардкожена в switch.

Например:

```yaml
elementCompatibilityCoefficient: 0.15

elementCompatibility:
  Fire:
    Fire: 0.5
    Water: -1
    Earth: 0
    Air: 1
    Void: 0

  Water:
    Fire: -1
    Water: 0.5
    Earth: 1
    Air: 0
    Void: 0

  Earth:
    Fire: 0
    Water: 1
    Earth: 0.5
    Air: -1
    Void: 0

  Air:
    Fire: 1
    Water: 0
    Earth: -1
    Air: 0.5
    Void: 0

  Void:
    Fire: 0
    Water: 0
    Earth: 0
    Air: 0
    Void: 0.5
```

---

# 48. Формула elemental compatibility

В коде перебрать каждую уникальную пару ингредиентов:

```csharp
for (var i = 0; i < ingredients.Count; i++)
{
    for (var j = i + 1; j < ingredients.Count; j++)
    {
        ...
    }
}
```

Не сравнивать:

```text
A-B
B-A
```

дважды.

Получить:

```text
compatibilitySum
```

Затем:

```text
ElementModifier =
    1
    + elementCompatibilityCoefficient
    * compatibilitySum
```

При:

```text
coefficient = 0.15
```

---

# 49. Проверки elemental compatibility

Fire + Air:

```text
sum = +1
modifier = 1.15
```

Fire + Water:

```text
sum = -1
modifier = 0.85
```

Fire + Air + Air:

```text
Fire-Air = 1
Fire-Air = 1
Air-Air = 0.5

sum = 2.5

modifier =
1 + 2.5 * 0.15
= 1.375
```

Void + Fire:

```text
modifier = 1
```

---

# 50. Core и elemental compatibility

Текущий алхимический core:

* влияет на quality;
* входит в rarity averaging;
* не участвует в property matching.

Сохранить аналогичное правило для элементов:

**core не участвует в pairwise compatibility внешних ингредиентов.**

Compatibility считается только между основными ингредиентами смеси.

---

# 51. Загрязнение

Добавить в модель экземпляра предмета:

```csharp
double Contamination
```

Диапазон:

```text
0.0 .. 1.0
```

Где:

```text
0 = чистый предмет
1 = полностью загрязнённый
```

Это runtime property конкретного `ItemInstance`.

Не помещать contamination в `ItemDefinition`, поскольку два экземпляра одного ингредиента потенциально могут иметь разное загрязнение.

---

# 52. Contamination curve

Хранить в `Alchemy.yaml`:

```yaml
contaminationModifierCurve:
  - contamination: 0.0
    multiplier: 1.25

  - contamination: 0.01
    multiplier: 0.99

  - contamination: 0.50
    multiplier: 0.50

  - contamination: 0.75
    multiplier: 0.25

  - contamination: 1.00
    multiplier: 0.10
```

Между точками использовать линейную интерполяцию.

Clamp contamination:

```text
0 .. 1
```

Обратить внимание на намеренно очень сильный скачок:

```text
0%  -> 1.25
1%  -> 0.99
```

Не сглаживать его самостоятельно.

---

# 53. Загрязнение алхимической смеси

Excel не задаёт явной формулы происхождения загрязнения.

Поэтому не создавать случайную генерацию contamination без отдельного параметра.

Для backward compatibility:

```text
ItemInstance.Contamination = 0
```

по умолчанию.

Для crafted mixture допустимо использовать простое среднее contamination участвующих внешних ингредиентов:

```text
RecipeContamination =
    Average(ingredient.Contamination)
```

Core в это усреднение пока не включать, аналогично property matching.

Сохранить это значение у созданной пилюли.

---

# 54. Итоговая формула existing crafted property

Существующую алхимическую систему расширить двумя новыми multiplier:

```text
ElementModifier
ContaminationModifier
```

То есть концептуально:

```text
FinalCraftedProperty =
    ExistingCraftedPropertyCalculation
    * ElementModifier
    * ContaminationModifier
```

Не применять:

```text
quality
```

второй раз.

Не применять rarity как effect multiplier без отдельного решения.

Сейчас rarity из Excel однозначно используется в price formula, но прямой связи rarity → CraftedEffect таблица не содержит.

---

# 55. Новые EffectType из Excel

Excel описывает дополнительные типы эффектов, которые текущая система поддерживает не полностью.

Добавить инфраструктурную поддержку:

```text
MaximumHealth
Attack
AttackSpeed
HealthRestore
```

Существующие уже покрывают:

```text
SpiritualPowerGain
TickEfficiency
AgingSpeed
HealthRegeneration
MissionProgress
BreakthroughChance
```

Таким образом можно выразить:

### Water

* мгновенная Spiritual Power;
* постоянный/временный SpiritualPowerGain;
* бонус ко всему получаемому духу.

### Earth

* HealthRegeneration;
* MaximumHealth;
* HealthRestore.

### Air

* AttackSpeed Permanent;
* AttackSpeed Temporary AdditivePercent.

### Fire

* Attack Permanent;
* Attack Temporary AdditivePercent.

### Void

* TickEfficiency;
* MissionProgress;
* BreakthroughChance.

---

# 56. Не создавать выдуманные baseValue

Excel перечисляет возможные виды пилюль, но не задаёт численные базовые значения для:

* Attack;
* AttackSpeed;
* MaximumHealth;
* Instant Heal.

Поэтому:

1. добавить EffectType;
2. добавить поддержку их применения;
3. НЕ создавать произвольные пилюли с придуманными цифрами.

Числа должны появляться через `Items.yaml` или `Alchemy.yaml` позже.

---

# 57. Distillation

Существующую систему сохранить.

Цена экстракта должна пользоваться обычным ItemPriceCalculator.

То есть не создавать отдельную формулу цены дистиллята:

```text
BasePrice
* QualityMultiplier
* RarityMultiplier
```

Если `alchemy_extract` имеет собственный динамический basePrice экземпляра — использовать соответствующую существующую механику.

---

# 58. Item price calculator

Создать/переработать единый сервис, например:

```csharp
IItemPriceCalculator
```

Он должен знать только:

```text
ItemDefinition.BasePrice
ItemInstance.Quality
ItemInstance.Rarity
ItemCategory
```

и balance database.

Он НЕ должен знать ничего о UI или магазине.

Пример API:

```csharp
decimal CalculateIntrinsicPrice(ItemInstance item);
```

А магазин уже отдельно делает:

```csharp
decimal CalculateBuyPrice(...);
decimal CalculateSellPrice(...);
```

---

# 59. Curve interpolation

Не писать отдельные куски интерполяции для:

* quality;
* contamination;
* других будущих кривых.

Создать общий helper типа:

```csharp
PiecewiseLinearCurve
```

или существующий аналог.

API концептуально:

```csharp
double Evaluate(double x);
```

Точки заранее сортировать и валидировать при загрузке конфигурации.

Для значений за пределами кривой — clamp к крайней точке, если конкретный config не говорит иначе.

---

# 60. Balance database / snapshot

После загрузки всех YAML желательно собрать immutable runtime snapshot:

```text
GameBalanceDatabase
```

В нём заранее построить:

* dictionary rarity -> rarity config;
* dictionary stageId -> stage;
* ordered stage list;
* cultivation cost matrix;
* theoretical stage start stats;
* theoretical stage end stats;
* interpolation curves;
* element compatibility lookup;
* mission/danger dictionaries.

Не рассчитывать рекуррентные 70 значений заново каждый игровой тик.

---

# 61. Cultivation costs precomputation

При загрузке:

```text
7 stages × 10 levels
```

сгенерировать один раз.

Например:

```csharp
double[,] RequiredPower;
```

или immutable collections.

Runtime:

```csharp
GetRequiredPower(stage, level)
```

должен быть O(1).

---

# 62. Stage statistics precomputation

Аналогично один раз рассчитать:

```text
StageStartStats
StageEndStats
```

для каждой стадии.

Их затем использует:

* UI;
* прогноз;
* enemy generation;
* баланс;
* mission rank system.

---

# 63. Характеристики текущего героя

Для actual player не использовать `StageEndStats`.

Actual progression stats вычислять исходя из текущего level.

Если `level` означает текущую полосу от `1..10`:

```text
completedLevelUps =
    level - 1
```

То есть на начале Level 1:

```text
0 level bonuses
```

на начале Level 10:

```text
9 level bonuses
```

После успешного завершения десятой полосы и прорыва предыдущая стадия считается полностью завершённой и даёт все 10 level bonuses плюс breakthrough bonus.

Это предотвращает off-by-one ошибку.

---

# 64. Прорыв

Существующую систему:

```text
baseBreakthroughChance
breakthroughChancePerExtraPowerBar
maximumBreakthroughChance
items
```

сохранить.

Excel не заменяет её.

Стоимость level 10 определяет размер основной шкалы, необходимой перед breakthrough.

Дополнительный накопленный Spiritual Power продолжает давать существующий bonus chance.

---

# 65. Не смешивать RequiredPower и BreakthroughChance

Должны существовать отдельные понятия:

```text
RequiredPowerForCurrentBar
```

и:

```text
BreakthroughChance
```

Стоимость прокачки не должна непосредственно определять вероятность прорыва.

---

# 66. Сохранения

Не сохранять generated balance:

```text
RequiredPower table
StageStartStats
StageEndStats
Rarity multipliers
Quality curves
```

Save должен ссылаться на:

```text
stageId
level
currentSpiritualPower
```

После изменения YAML игра автоматически должна использовать новый баланс.

---

# 67. ItemInstance migration

После добавления contamination старые сохранения должны загружаться.

Если поле отсутствует:

```text
Contamination = 0
```

Аналогично для новых optional алхимических данных.

---

# 68. Config validation

При запуске игры делать fail-fast validation.

Проверить:

### Cultivation

* stages не пустой;
* id уникальны;
* `recursiveCoefficient > 0`;
* `spiritualPowerMultiplier > 0`;
* ровно два `initialRequiredPower`;
* оба > 0;
* `stageEntryCoefficient > 0`.

### Rarity

* все enum entries существуют;
* multiplier > 0;
* weight >= 0.

### Quality curve

* точки отсортированы;
* нет duplicate quality;
* multiplier > 0.

### Alchemy

* min ingredients <= max;
* compatibility matrix содержит известные элементы;
* matrix symmetric;
* contamination points 0..1;
* property IDs уникальны.

### Combat

* danger levels уникальны;
* multiplier >= 0;
* reference только StageStart/StageEnd.

---

# 69. Что оставить без изменений

## Dog.yaml

Не относится к Excel-балансу.

Не менять.

## UI.yaml

Не относится к новой математической модели.

Не менять.

## Missions.yaml

Контент, длительности, reward values и weights оставить, кроме использования нового rank/danger pipeline.

## Shop.yaml

Наценки и количество слотов оставить.

## Items.yaml

Контентные предметы, эффекты, изображения и базовые цены оставить, кроме необходимых новых optional полей.

---

# 70. Что удалить/депрекейтнуть

После того как новая модель заработает и usages будут удалены:

### Cultivation.yaml

Legacy:

```text
baseRequiredPower
levelMultipliers
stageMultiplier
```

### Combat.yaml

Для HP/Attack:

```text
heroHealthPerStage
heroHealthPerLevel
heroAttackPerStage
heroAttackPerLevel
```

Их заменяет `statsPerLevel` + `breakthroughBonus` конкретной стадии.

### Combat danger

Legacy:

```text
monsterPowerMultiplier
```

после перехода на StageStart/StageEnd scaling.

### Monsters

Legacy absolute:

```text
maximumHealth
attack
attacksPerSecond
```

только после полного перевода combat generation.

Не оставлять два одновременно работающих источника истины.

---

# 71. Важное правило migration

Сначала:

1. реализовать новую модель;
2. перевести consumers;
3. написать tests;
4. убедиться, что старые поля больше нигде не читаются;
5. только после этого удалять legacy поля.

Не делать массовое удаление конфигов до изменения runtime code.

---

# 72. Предлагаемые классы

Имена можно адаптировать под существующий стиль проекта.

Логически необходимы сущности уровня:

```text
CultivationCostCalculator
CultivationStatsCalculator
ItemPriceCalculator
EnemyStatsCalculator
ElementCompatibilityCalculator
BalanceCurve
```

Не обязательно делать каждый отдельным DI service, если в проекте принято иначе.

Но responsibilities должны быть разделены.

---

# 73. Stats value object

Желательно иметь единый value-object для progression/combat stats:

```csharp
public readonly record struct CharacterStats(
    double MaximumHealth,
    double HealthRegeneration,
    double Attack,
    double AttacksPerSecond,
    double LongevityYears);
```

Defense можно пока хранить отдельно либо добавить optional field, если это лучше соответствует существующей combat architecture.

---

# 74. Arithmetic для Stats

Поддержать операции:

```text
Stats + Stats
Stats * scalar
```

Это упростит:

```text
EndStats =
    StartStats
    + PerLevelStats * 10
```

и:

```text
Enemy =
    StageEndStats * 0.45
```

---

# 75. Tests — обязательная часть задачи

Не ограничиваться «игра запускается».

Нужны unit tests на математическое ядро.

## CultivationCostCalculator

Проверить значения Excel.

## CultivationStatsCalculator

Проверить start/end стадий.

## EnemyStatsCalculator

Проверить Weak/Medium/Strong.

## ItemPriceCalculator

Проверить цены.

## ElementCompatibilityCalculator

Проверить пары.

## Curve

Проверить exact points и interpolation.

---

# 76. Enemy tests

Body Tempering Weak:

```text
Start:
100 / 1 / 1 / 1

×0.9

Enemy:
90
0.9
0.9
0.9

Rank:
E-
```

Body Medium:

```text
End:
150
2
6
1.5

×0.45:

67.5
0.9
2.7
0.675

Rank E
```

Body Strong:

```text
135
1.8
5.4
1.35

Rank E+
```

---

# 77. Final-stage test

Immortal Ascension end:

```text
HP = 20000
Regen = 193.75
Attack = 561
AttackSpeed = 24.5
```

Strong enemy:

```text
18000
174.375
504.9
22.05
```

Rank:

```text
SS+
```

---

# 78. Alchemy tests

## Compatibility

```text
Fire + Air = 1.15
Fire + Water = 0.85
Fire + Air + Air = 1.375
Fire + Void = 1.0
```

## Contamination exact points

```text
0.00 -> 1.25
0.01 -> 0.99
0.50 -> 0.50
0.75 -> 0.25
1.00 -> 0.10
```

Проверить interpolation между точками.

---

# 79. Price tests

```text
Ingredient
Base 50
Quality 5
Common
=
175
```

```text
Uncommon
=
700
```

```text
Rare
=
2800
```

```text
Divine
=
716800
```

Low quality:

```text
Ingredient base 50
quality 0.1
Common
=
10
```

Pill:

```text
base 500
quality 0.1
Common
=
250
```

---

# 80. Не тестировать YAML вручную через UI

Tests должны напрямую вызывать математические calculators.

UI/integration tests могут быть дополнительными.

Главное — чтобы изменение формулы сразу показывало, какое expected balance value сломалось.

---

# 81. Итоговая схема зависимости данных

```text
GameBalance.yaml
    ↓
global tick/economy rules

Cultivation.yaml
    ↓
stage coefficients
stage spirit rate
stats progression
breakthrough
mission rank
    ↓
CultivationCostCalculator
CultivationStatsCalculator

Combat.yaml
    ↓
danger profiles
combat-specific settings
    ↓
EnemyStatsCalculator

Items.yaml
    ↓
base item data
basePrice
effects
alchemy properties
element

Rarities.yaml
    ↓
price multiplier
generation weight

Alchemy.yaml
    ↓
recipe rules
properties
element matrix
contamination curve
    ↓
AlchemyCalculator

Shop.yaml
    ↓
markup
sell adjustment

Missions.yaml
    ↓
content/duration/rewards
danger level
```

---

# 82. Price pipeline

Должен выглядеть так:

```text
ItemDefinition.BasePrice
        ↓
Quality multiplier
        ↓
Rarity multiplier
        ↓
Intrinsic item price
        ↓
Shop markup / sell adjustment
        ↓
Displayed transaction price
```

---

# 83. Cultivation pipeline

```text
Stage configuration
        ↓
Recursive cost table
        ↓
Current required power
        ↓
Spiritual Power generated by ticks
        ↓
Level progression
        ↓
Stage level stats
        ↓
Level 10
        ↓
Breakthrough system
        ↓
Breakthrough bonus
        ↓
Next stage
```

---

# 84. Enemy pipeline

```text
Player cultivation stage
        ↓
Theoretical StageStart / StageEnd
        ↓
Mission danger level
        ↓
Weak / Medium / Strong profile
        ↓
Generated enemy combat stats
        ↓
Monster visual/archetype
        ↓
Battle
```

---

# 85. Alchemy pipeline

```text
Ingredient ItemInstances
        ↓
Properties
Potency
Quality
Distillation
Elements
Contamination
        ↓
Property matching
        ↓
Existing ingredient-count multiplier
        ↓
Element compatibility
        ↓
Contamination modifier
        ↓
Crafted effects
        ↓
Crafted ItemInstance
```

---

# 86. Что особенно важно НЕ сделать

Не делать giant:

```csharp
GameBalanceManager
```

на несколько тысяч строк со всем подряд.

Не хардкодить:

```csharp
if (stage == BodyTempering)
{
    ...
}
else if (stage == QiGathering)
{
    ...
}
```

Не создавать switch на каждую стадию для чисел.

Не записывать 70 calculated required-power values в YAML.

Не хранить enemy stats для каждой стадии вручную.

Не рассчитывать rarity multiplier через enum ordinal.

Не применять одновременно старую и новую систему Cultivation.

Не применять одновременно:

```text
monsterPowerMultiplier
```

и новый StageStart/StageEnd multiplier.

Не применять quality два раза к CraftedEffect.

Не менять tick frequency ради ускорения культивации.

Не придумывать отсутствующие в таблице numeric values.

---

# 87. Definition of Done

Работа считается завершённой, когда:

1. Новая Cultivation progression полностью работает через рекуррентную формулу Excel.

2. В активной прогрессии используются 7 стадий Excel.

3. Spiritual Power rate зависит от стадии через конфиг.

4. Character stats рассчитываются через stage-specific `statsPerLevel` и `breakthroughBonus`.

5. Базовые характеристики соответствуют Excel.

6. Enemy Weak/Medium/Strong генерируются от theoretical stage stats.

7. Mission rank выводится автоматически как `E- ... SS+`.

8. Цены предметов используют новые rarity multiplier.

9. Quality price curve соответствует Excel.

10. Special quality `0.1` различается для Ingredient и Pill.

11. Shop markup остаётся отдельным слоем.

12. Element compatibility реализована универсальной формулой.

13. Contamination curve реализована.

14. Старые сохранения не падают из-за отсутствующего contamination.

15. Existing alchemy property/potency/distillation system продолжает работать.

16. `Dog.yaml` и `UI.yaml` не затронуты.

17. Старые conflicting formulas больше не используются.

18. Все основные математические значения покрыты unit tests.

19. Для конфигов есть validation с понятными ошибками.

20. При изменении коэффициента в YAML не требуется изменять C#.

---

# 88. Основное правило результата

После реализации я должен иметь возможность открыть, например:

```yaml
recursiveCoefficient: 0.55
```

поменять его на:

```yaml
recursiveCoefficient: 0.57
```

и после перезапуска игры автоматически должны измениться:

* стоимость соответствующих уровней;
* время прохождения стадии;
* все последующие связанные значения;

без редактирования C#.

А если я изменю:

```yaml
statsPerLevel:
  attack: 5
```

на:

```yaml
statsPerLevel:
  attack: 7
```

автоматически должны измениться:

* характеристики персонажа;
* theoretical StageEnd;
* сила Medium/Strong врагов этой и зависимых стадий;
* DPS;
* всё, что зависит от Attack.

Именно такой data-driven результат требуется получить.
