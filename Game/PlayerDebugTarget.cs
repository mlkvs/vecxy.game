using System.Numerics;
using Vecxy.Diagnostics.Console;

namespace Game;

public enum PlayerState
{
    Idle,
    Walking,
    Running,
    Dead
}

[ConsoleObject("player", Description = "Local player debug controls")]
public sealed class PlayerDebugTarget(Player player)
{
    [ConsoleMember("health", Description = "Current player health")]
    private float _health = 100.0f;

    [ConsoleMember("speed", Description = "Player movement speed")]
    public float Speed
    {
        get => player.WalkSpeed;
        set => player.WalkSpeed = value;
    }

    [ConsoleMember("god", Description = "Invulnerability state")]
    public bool IsGodMode { get; set; }

    [ConsoleMember("state", Description = "Current player state")]
    public PlayerState State { get; set; }

    [ConsoleMember("bonus", Description = "Optional bonus multiplier")]
    public float? BonusMultiplier { get; set; }

    [ConsoleMember("move", Description = "Moves player by an offset")]
    private void Move(float x, float y, float z)
    {
        player.Transform.WorldPosition += new Vector3(x, y, z);
        player.SyncView();
    }

    [ConsoleMember("damage", Description = "Applies damage to the player")]
    private float Damage(float amount)
    {
        if (!IsGodMode)
            _health = Math.Max(0.0f, _health - amount);

        return _health;
    }
}
