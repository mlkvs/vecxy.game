namespace HardCore.Cultivation;

public sealed class PlayerProgress
{
    public ECultivationStage Stage { get; set; } = ECultivationStage.BodyTempering;
    public int RealmLevel { get; set; } = 1;
    public int SectReputation { get; set; }
    public int AlchemyLevel { get; set; } = 1;
    public double AlchemyExperience { get; set; }
}
