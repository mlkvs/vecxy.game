using System.Numerics;
using Vecxy.Assets;
using Vecxy.Interaction;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation;

public sealed class CharacterPointerFeedback(
    SpriteRenderer sprite,
    CultivationInteraction interaction) :
    AComponent,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler
{
    private bool _hovered;

    public void OnPointerEnter(in PointerEventData eventData)
    {
        _hovered = true;
        sprite.Color = new Vector4(1.08f, 1.05f, 0.90f, 1.0f);
    }

    public void OnPointerExit(in PointerEventData eventData)
    {
        _hovered = false;
        sprite.Color = Vector4.One;
    }

    public void OnPointerDown(in PointerEventData eventData)
    {
        if (eventData.Button == EMouseButton.Left)
            sprite.Color = new Vector4(0.75f, 0.82f, 1.0f, 1.0f);
    }

    public void OnPointerUp(in PointerEventData eventData)
    {
        if (eventData.Button != EMouseButton.Left)
            return;

        sprite.Color = _hovered
            ? new Vector4(1.08f, 1.05f, 0.90f, 1.0f)
            : Vector4.One;
    }

    public void OnPointerClick(in PointerEventData eventData)
    {
        if (eventData.Button == EMouseButton.Left)
            interaction.ClickCharacter();
    }

    public override void OnDisable()
    {
        _hovered = false;
        if (!sprite.IsDestroyed)
            sprite.Color = Vector4.One;
    }
}
