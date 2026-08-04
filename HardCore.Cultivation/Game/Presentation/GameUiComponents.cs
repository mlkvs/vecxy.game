using Vecxy.UI;

namespace HardCore.Cultivation.Game.Presentation;

internal abstract class ItemCardView : UiComponent
{
    protected ItemCardView(UiElement root) : base(root)
    {
        Rarity = Element<UiText>(".item-rarity");
        Quality = Element<UiPanel>(".item-quality");
        IconWell = Element<UiPanel>(".item-icon-well");
        Meta = Element<UiText>(".item-meta");
    }

    public UiElement Card => Root;
    public UiText Rarity { get; }
    public UiPanel Quality { get; }
    public UiPanel IconWell { get; }
    public UiText Meta { get; }
}

internal sealed class ShopCardView : ItemCardView
{
    public ShopCardView(UiElement root) : base(root) =>
        Buy = Element<UiButton>(".buy-button");

    public UiButton Buy { get; }
}

internal sealed class InventoryCardView : ItemCardView
{
    public InventoryCardView(UiElement root) : base(root)
    {
        Use = Element<UiButton>(".use-button");
        Sell = Element<UiButton>(".sell-button");
    }

    public UiButton Use { get; }
    public UiButton Sell { get; }
}

internal sealed class MissionCardView : UiComponent
{
    public MissionCardView(UiElement root) : base(root)
    {
        RewardIcons = Element<UiPanel>(".mission-reward-icons");
        Start = Element<UiButton>(".mission-start");
    }

    public UiElement Card => Root;
    public UiPanel RewardIcons { get; }
    public UiButton Start { get; }
}

internal sealed class MissionQueueItemView : UiComponent
{
    public MissionQueueItemView(UiElement root) : base(root)
    {
        Progress = Element<UiText>(".queue-progress");
        MoveUp = Element<UiButton>(".queue-left");
        MoveDown = Element<UiButton>(".queue-right");
        Remove = Element<UiButton>(".queue-remove");
    }

    public UiText Progress { get; }
    public UiButton MoveUp { get; }
    public UiButton MoveDown { get; }
    public UiButton Remove { get; }
}

internal sealed class QualityStarsView : UiComponent
{
    private readonly IReadOnlyList<UiPanel> _fills;

    public QualityStarsView(UiElement root) : base(root) =>
        _fills = Elements<UiPanel>("quality-star-fill");

    public void SetQuality(decimal quality)
    {
        for (var index = 0; index < _fills.Count; index++)
            _fills[index].Style.SetWidthPercent((float)Math.Clamp(quality - index, 0m, 1m));
    }
}
