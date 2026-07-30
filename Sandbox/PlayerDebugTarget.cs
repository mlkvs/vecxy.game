using System.Numerics;
using Vecxy.Diagnostics.Console;

namespace Sandbox;

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
    public float Health
    {
        get => player.Health;
        set => player.SetHealth(value);
    }

    [ConsoleMember("maxHealth", Description = "Maximum player health")]
    public float MaxHealth
    {
        get => player.MaxHealth;
        set => player.SetMaxHealth(value);
    }

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
            player.ApplyDamage(amount);

        return player.Health;
    }
}
