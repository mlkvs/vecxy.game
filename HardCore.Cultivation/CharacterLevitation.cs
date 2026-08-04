using System.Numerics;
using Vecxy.Scene;

namespace HardCore.Cultivation;

public sealed class CharacterLevitation : AComponent
{
    private Vector3 _origin;
    private float _elapsed;

    public float Amplitude { get; init; } = 8.0f;
    public float PeriodSeconds { get; init; } = 3.6f;

    public override void Start()
    {
        _origin = Transform.Position;
    }

    public override void Update(float deltaTime)
    {
        _elapsed += deltaTime;
        var angularSpeed = MathF.Tau / Math.Max(0.1f, PeriodSeconds);
        Transform.Position = _origin + Vector3.UnitY * (MathF.Sin(_elapsed * angularSpeed) * Amplitude);
    }
}
