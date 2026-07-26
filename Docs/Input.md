# Input

`Vecxy.Input` сейчас устроен как простая runtime-система с asset-конфигом.

Основные сущности:

- `IInputManager` — глобальный сервис ввода
- `InputAsset` — asset с maps/actions/bindings
- `InputMap` — экземпляр набора действий, который можно `Enable()` / `Disable()`
- `InputAction` / `InputAction<T>` — конкретное действие

## Быстрый пример

```csharp
public sealed class PlayerController
{
    private readonly InputMap _input;

    public PlayerController(
        IInputManager inputManager,
        IAssetsManager assets)
    {
        var inputAsset = assets.Load<InputAsset>("Controls.input");
        _input = inputManager.Create(inputAsset, "Player");

        _input.GetAction("Jump").Started += OnJump;
        _input.GetAction<Vector2>("Move").Performed += OnMove;
    }

    public void Enable() => _input.Enable();
    public void Disable() => _input.Disable();

    private void OnJump(InputActionContext context)
    {
    }

    private void OnMove(InputActionContext<Vector2> context)
    {
        var value = context.Value;
    }
}
```

## Опрос состояния

Для кнопок:

```csharp
if (_input.GetAction("Sprint").IsPressed)
{
}
```

Для значений:

```csharp
var move = _input.GetAction<Vector2>("Move").Value;
```

## Жизненный цикл

Каждый `Create(...)` создаёт отдельный runtime-экземпляр карты:

```csharp
var gameplay = inputManager.Create(assetRef, "Player");
var ui = inputManager.Create(assetRef, "UI");
```

Подписки и состояние у них независимые.

Когда карта выключена:

- события не приходят
- действия сбрасываются

Не забывай вызывать `Dispose()` у `InputMap`, когда карта больше не нужна.

## YAML / .input

Пример:

```yaml
namespace: Game
className: GameInput

maps:
  - name: Player
    actions:
      - name: Move
        type: Vector2
        bindings:
          - type: Composite
            composite: WASD

      - name: Look
        type: Button
        bindings:
          - type: Mouse
            mouse: Right

      - name: Sprint
        type: Button
        bindings:
          - type: Keyboard
            key: LeftShift
```

Поддерживается сейчас:

- `Button`
- `Vector2`
- `Keyboard`
- `Mouse`
- `Composite: WASD`

## Полный доступный набор клавиш

`EKeyboardKey` содержит полный набор основных keyboard-кодов:

- буквы `A-Z`
- цифры `Number0-Number9`
- `F1-F25`
- стрелки
- `Space`, `Enter`, `Escape`, `Tab`, `Backspace`
- `Insert`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`
- `LeftShift`, `RightShift`
- `LeftControl`, `RightControl`
- `LeftAlt`, `RightAlt`
- `LeftSuper`, `RightSuper`
- keypad-клавиши
- и прочие стандартные клавиши GLFW/Silk.NET

Для мыши доступны:

- `Left`
- `Right`
- `Middle`
- `Button4-Button8`

## Где лучше обрабатывать input

Обычно:

- глобальные системные хоткеи — в engine/editor слое
- gameplay input — в игровых системах / компонентах
- UI input — в отдельной `UI` map

Пример для engine-горячей клавиши:

```yaml
- name: Engine
  actions:
    - name: ToggleFullscreen
      type: Button
      bindings:
        - type: Keyboard
          key: F11
```

## Что важно сейчас

- если `.input` asset меняется и валиден, `InputMap` подхватывает новые binding'и
- если asset сломан, старая рабочая версия карты продолжает использоваться
- имена actions и maps чувствительны к строке, потому что runtime API сейчас строковый
