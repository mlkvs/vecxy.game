namespace HardCore.Cultivation;

public sealed class MissionRuntime
{
    public string MissionId { get; set; } = string.Empty;
    public EMissionState State { get; set; } = EMissionState.Available;
    public int RemainingTicks { get; set; }
}
