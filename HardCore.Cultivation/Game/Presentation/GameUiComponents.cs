using Vecxy.Assets;
using Vecxy.UI;

namespace HardCore.Cultivation.Game.Presentation;

[AssetPath("UI/Components/ShopCard.xml")]
internal sealed class ShopCardView : AUiComponent
{
    public ShopCardView(UiElement root) : base(root)
    {
        IconWell = Element<UiPanel>(".item-icon-well");
        Icon = Element<UiImage>(".item-icon");
        Name = Element<UiText>(".item-name");
        Meta = Element<UiText>(".item-meta");
        QualityHost = Element<UiPanel>(".shop-item-quality");
        Effect = Element<UiText>(".item-effect");
        Buy = Element<UiButton>(".buy-button");
    }

    public UiElement Card => Root;
    public UiPanel IconWell { get; }
    public UiImage Icon { get; }
    public UiText Name { get; }
    public UiText Meta { get; }
    public UiPanel QualityHost { get; }
    public QualityStarsView QualityStars { get; set; } = null!;
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
        Quantity = Element<UiText>(".inventory-quantity");
    }

    public UiElement Card => Root;
    public UiPanel IconWell { get; }
    public UiImage Icon { get; }
    public UiPanel QualityHost { get; }
    public QualityStarsView QualityStars { get; set; } = null!;
    public UiText Quantity { get; }
}

internal sealed class MissionCardView : AUiComponent
{
    public MissionCardView(UiElement root) : base(root)
    {
        Danger = Element<UiText>(".mission-danger");
        Name = Element<UiText>(".mission-card-title");
        Description = Element<UiText>(".mission-card-description");
        Duration = Element<UiText>(".mission-duration-value");
        RewardIcons = Element<UiPanel>(".mission-reward-icons");
        Start = Element<UiButton>(".mission-start");
    }

    public UiElement Card => Root;
    public UiText Danger { get; }
    public UiText Name { get; }
    public UiText Description { get; }
    public UiText Duration { get; }
    public UiPanel RewardIcons { get; }
    public UiButton Start { get; }
}

internal sealed class MissionQueueItemView : AUiComponent
{
    public MissionQueueItemView(UiElement root) : base(root)
    {
        Number = Element<UiText>(".queue-number-text");
        Name = Element<UiText>(".queue-item-name");
        Progress = Element<UiText>(".queue-progress");
        MoveUp = Element<UiButton>(".queue-left");
        MoveDown = Element<UiButton>(".queue-right");
        Remove = Element<UiButton>(".queue-remove");
    }

    public UiElement Card => Root;
    public UiText Number { get; }
    public UiText Name { get; }
    public UiText Progress { get; }
    public UiButton MoveUp { get; }
    public UiButton MoveDown { get; }
    public UiButton Remove { get; }
}

internal sealed class QualityStarsView : AUiComponent
{
    private const string GrayStar = "Assets/Textures/UIIconsAtlas.atlas#star-gray";
    private const string RainbowStar = "Assets/Textures/UIIconsAtlas.atlas#star-rainbow";

    private readonly IReadOnlyList<UiImage> _emptyStars;
    private readonly IReadOnlyList<UiPanel> _fills;

    public QualityStarsView(UiElement root) : base(root)
    {
        _emptyStars = Elements<UiImage>("quality-star-empty");
        _fills = Elements<UiPanel>("quality-star-fill");
    }

    public void SetQuality(decimal quality)
    {
        Root.SetAttribute("aria-label", "Качество предмета известно");
        Root.ToggleClass("unknown-quality", false);
        foreach (var star in _emptyStars)
            star.Sprite = GrayStar;
        for (var index = 0; index < _fills.Count; index++)
            _fills[index].Style.SetWidthPercent((float)Math.Clamp(quality - index, 0m, 1m));
    }

    public void SetUnknown()
    {
        Root.SetAttribute("aria-label", "Качество предмета неизвестно");
        Root.ToggleClass("unknown-quality", true);
        foreach (var star in _emptyStars)
            star.Sprite = RainbowStar;
        foreach (var fill in _fills)
            fill.Style.SetWidthPercent(0f);
    }
}
