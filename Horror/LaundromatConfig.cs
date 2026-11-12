using Vecxy.Assets;

namespace Horror;

public sealed class LaundromatConfig : IYamlConfig
{
    public float[] SpawnPosition { get; set; } = [];
    public float SpawnYaw { get; set; }
    public List<Obstacle> Colliders { get; set; } = [];

    public sealed class Obstacle
    {
        public string Name { get; set; } = "Obstacle";
        public float[] Position { get; set; } = [];
        public float[] Size { get; set; } = [];
        public float Yaw { get; set; }
    }

    public void Validate()
    {
        if (!IsVector(SpawnPosition) || !float.IsFinite(SpawnYaw) || Colliders.Count == 0)
            throw new InvalidDataException("Laundromat requires a finite spawn and collision geometry.");

        foreach (var collider in Colliders)
        {
            if (!IsVector(collider.Position) || !IsVector(collider.Size) ||
                collider.Size.Any(value => value <= 0.0f) || !float.IsFinite(collider.Yaw))
                throw new InvalidDataException($"Invalid laundromat collider '{collider.Name}'.");
        }
    }

    private static bool IsVector(float[] value) => value is { Length: 3 } && value.All(float.IsFinite);
}
