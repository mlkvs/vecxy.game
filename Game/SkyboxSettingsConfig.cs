using System.Numerics;
using Vecxy.Assets;

namespace Game;

public sealed class SkyboxSettingsConfig : IYamlConfig
{
    public bool Enabled { get; set; } = true;
    public string PositiveX { get; set; } = "SkyBox/cubemap/px.png";
    public string NegativeX { get; set; } = "SkyBox/cubemap/nx.png";
    public string PositiveY { get; set; } = "SkyBox/cubemap/py.png";
    public string NegativeY { get; set; } = "SkyBox/cubemap/ny.png";
    public string PositiveZ { get; set; } = "SkyBox/cubemap/pz.png";
    public string NegativeZ { get; set; } = "SkyBox/cubemap/nz.png";
    public float[]? Tint { get; set; } = [1.0f, 1.0f, 1.0f];
    public float[]? Rotation { get; set; } = [0.0f, 0.0f, 0.0f];
    public float Exposure { get; set; } = 1.0f;

    public Vector3 GetTint(string path)
    {
        if (Tint is null)
            return Vector3.One;

        if (Tint.Length != 3)
        {
            throw new InvalidDataException(
                $"Skybox config '{path}' tint must contain three components.");
        }

        return new Vector3(Tint[0], Tint[1], Tint[2]);
    }

    public Vector3 GetRotation(string path)
    {
        if (Rotation is null)
            return Vector3.Zero;

        if (Rotation.Length != 3)
        {
            throw new InvalidDataException(
                $"Skybox config '{path}' rotation must contain three components.");
        }

        return new Vector3(Rotation[0], Rotation[1], Rotation[2]);
    }

    public void Validate(string path)
    {
        _ = GetTint(path);
        _ = GetRotation(path);
        ValidateFace(PositiveX, nameof(PositiveX), path);
        ValidateFace(NegativeX, nameof(NegativeX), path);
        ValidateFace(PositiveY, nameof(PositiveY), path);
        ValidateFace(NegativeY, nameof(NegativeY), path);
        ValidateFace(PositiveZ, nameof(PositiveZ), path);
        ValidateFace(NegativeZ, nameof(NegativeZ), path);

        if (Exposure < 0.0f)
        {
            throw new InvalidDataException(
                $"Skybox config '{path}' cannot have negative exposure.");
        }
    }

    private static void ValidateFace(
        string value,
        string name,
        string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Skybox config '{path}' has empty face path '{name}'.");
        }
    }
}
