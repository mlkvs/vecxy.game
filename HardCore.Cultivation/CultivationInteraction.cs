namespace HardCore.Cultivation;

public sealed class CultivationInteraction
{
    public event Action? CharacterClicked;

    public void ClickCharacter() => CharacterClicked?.Invoke();
}
