namespace HardCore.Cultivation.Game.Cheats;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CheatActionAttribute(
    string title,
    string group,
    int order = 0,
    int groupOrder = 0) : Attribute
{
    public string Title { get; } = title;
    public string Group { get; } = group;
    public int Order { get; } = order;
    public int GroupOrder { get; } = groupOrder;
}
