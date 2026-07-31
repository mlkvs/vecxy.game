using System.Numerics;
using Vecxy.Assets;
using Vecxy.Scene;

namespace Sandbox;

public sealed class FogSettingsConfig : IYamlConfig
{
    public bool Enabled { get; set; } = true;
    public EFogMode Mode { get; set; } = EFogMode.Linear;
    public float[]? Color { get; set; } = [0.025f, 0.035f, 0.05f];
    public float StartDistance { get; set; } = 4.0f;
    public float EndDistance { get; set; } = 18.0f;
    public float Density { get; set; } = 0.08f;
    public bool HeightEnabled { get; set; } = true;
    public float Height { get; set; } = 1.2f;
    public float HeightFalloff { get; set; } = 0.6f;
    public float VolumetricStrength { get; set; } = 0.35f;

    public Vector3 GetColor()
    {
        if (Color is null)
            return new Vector3(0.025f, 0.035f, 0.05f);

        if (Color.Length != 3)
        {
            throw new InvalidDataException(
                $"Fog config color must contain three components.");
        }

        return new Vector3(Color[0], Color[1], Color[2]);
    }

    public void Validate()
    {
        _ = GetColor();

        if (StartDistance < 0.0f)
        {
            throw new InvalidDataException(
                $"Fog config  has negative StartDistance.");
        }

        if (EndDistance <= StartDistance)
        {
            throw new InvalidDataException(
                $"Fog config must have EndDistance greater than StartDistance.");
        }

        if (Density < 0.0f)
        {
            throw new InvalidDataException(
                $"Fog config  has negative Density.");
        }

        if (HeightFalloff < 0.0f)
        {
            throw new InvalidDataException(
                $"Fog config  has negative HeightFalloff.");
        }

        if (VolumetricStrength < 0.0f)
        {
            throw new InvalidDataException(
                $"Fog config  has negative VolumetricStrength.");
        }
    }
}
