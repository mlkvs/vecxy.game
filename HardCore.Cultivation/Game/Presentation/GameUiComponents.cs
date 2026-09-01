using Vecxy.Assets;
using Vecxy.UI;

namespace HardCore.Cultivation.Game.Presentation;

internal sealed class ShopCardView : AUiComponent
{
    public ShopCardView(UiElement root) : base(root)
    {
        IconWell = Element<UiPanel>(".item-icon-well");
        Icon = Element<UiImage>(".item-icon");
        Name = Element<UiText>(".item-name");
        QualityHost = Element<UiPanel>(".shop-item-quality");
        Contamination = Element<UiText>(".contamination-badge");
        Effect = Element<UiText>(".item-effect");
        Buy = Element<UiButton>(".buy-button");
    }

    public UiElement Card => Root;
    public UiPanel IconWell { get; }
    public UiImage Icon { get; }
    public UiText Name { get; }
    public UiPanel QualityHost { get; }
    public QualityStarsView QualityStars { get; set; } = null!;
    public UiText Contamination { get; }
    public UiText Effect { get; }
    public UiButton Buy { get; }
}

internal sealed class InventoryIconView : AUiComponent
{
    public InventoryIconView(UiElement root) : base(root)
    {
        IconWell = Element<UiPanel>(".inventory-icon-well");
        Icon = Element<UiImage>(".inventory-icon");
        QualityHost = Element<UiPanel>(".inventory-quality");
        Contamination = Element<UiText>(".contamination-badge");
        Quantity = Element<UiText>(".inventory-quantity");
    }

    public UiElement Card => Root;
    public UiPanel IconWell { get; }
    public UiImage Icon { get; }
    public UiPanel QualityHost { get; }
    public QualityStarsView QualityStars { get; set; } = null!;
    public UiText Contamination { get; }
    public UiText Quantity { get; }
}

internal sealed class MissionCardView : AUiComponent
{
    public MissionCardView(UiElement root) : base(root)
    {
        Name = Element<UiText>(".mission-card-title");
        Description = Element<UiText>(".mission-card-description");
        Duration = Element<UiText>(".mission-duration-value");
        Rank = Element<UiImage>(".mission-rank");
        RewardIcons = Element<UiPanel>(".mission-reward-icons");
        Start = Element<UiButton>(".mission-start");
    }

    public UiElement Card => Root;
    public UiText Name { get; }
    public UiText Description { get; }
    public UiText Duration { get; }
    public UiImage Rank { get; }
    public UiPanel RewardIcons { get; }
    public UiButton Start { get; }
}

internal sealed class MissionQueueItemView : AUiComponent
{
    public MissionQueueItemView(UiElement root) : base(root)
    {
        Number = Element<UiText>(".queue-number-text");
        Rank = Element<UiImage>(".queue-rank");
        Name = Element<UiText>(".queue-item-name");
        Progress = Element<UiText>(".queue-progress");
        MoveUp = Element<UiButton>(".queue-left");
        MoveDown = Element<UiButton>(".queue-right");
        Remove = Element<UiButton>(".queue-remove");
    }

    public UiElement Card => Root;
    public UiText Number { get; }
    public UiImage Rank { get; }
    public UiText Name { get; }
    public UiText Progress { get; }
    public UiButton MoveUp { get; }
    public UiButton MoveDown { get; }
    public UiButton Remove { get; }
}

internal sealed class QualityStarsView : AUiComponent
{
    private readonly IReadOnlyList<UiImage> _emptyStars;
    private readonly IReadOnlyList<UiPanel> _fills;
    private readonly string _grayStar;
    private readonly string _rainbowStar;

    public QualityStarsView(UiElement root, string grayStar, string rainbowStar) : base(root)
    {
        _grayStar = grayStar;
        _rainbowStar = rainbowStar;
        _emptyStars = Elements<UiImage>("quality-star-empty");
        _fills = Elements<UiPanel>("quality-star-fill");
    }

    public void SetQuality(decimal quality)
    {
        Root.SetAttribute("aria-label", "Качество предмета известно");
        Root.ToggleClass("unknown-quality", false);
        foreach (var star in _emptyStars)
            star.Sprite = _grayStar;
        for (var index = 0; index < _fills.Count; index++)
            _fills[index].Style.SetWidthPercent((float)Math.Clamp(quality - index, 0m, 1m));
    }

    public void SetUnknown()
    {
        Root.SetAttribute("aria-label", "Качество предмета неизвестно");
        Root.ToggleClass("unknown-quality", true);
        foreach (var star in _emptyStars)
            star.Sprite = _rainbowStar;
        foreach (var fill in _fills)
            fill.Style.SetWidthPercent(0f);
    }
}
