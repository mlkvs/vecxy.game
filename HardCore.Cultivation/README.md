# HardCore.Cultivation

## Содержимое

- YAML-конфиги предметов, миссий, рецептов, мира и стадий культивации
- GameDatabase с проверкой ссылок между конфигами
- Inventory
- ItemSystem
- CultivationSystem
- MissionSystem
- CraftingSystem
- GameSaveSystem
- CultivationManager
- GameLayer
- MainScene

## Основные вызовы из UI

```csharp
cultivation.Cultivation.SetMeditating(true);
cultivation.Cultivation.SetTraining(true);

cultivation.Cultivation.TryAdvanceLevel();
cultivation.Cultivation.TryBreakthrough();

cultivation.Missions.TryStart("gather_herbs");
cultivation.Missions.TryClaim("gather_herbs");

cultivation.Crafting.TryCraft("craft_qi_pill", 1);
```

`CultivationManager` специально не обращается к конкретным UI-элементам, потому что API поиска элементов и подписки на события `UiDocument` в предоставленном коде не показан. Подписки можно добавить в `Initialize()` после загрузки `Main.xml`.
