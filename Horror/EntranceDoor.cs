using System.Numerics;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Scene;

namespace Horror;

public sealed class EntranceDoor(InputMap input, Player player, IWindow window) : AComponent
{
    private InputAction? _interact;
    private float _angle;
    public bool IsOpen { get; private set; }

    public override void Start()
    {
        _interact = input.GetAction("Interact");
        _interact.Started += OnInteract;
    }

    private void OnInteract(InputActionContext context)
    {
        if (!window.IsCursorCaptured)
            return;

        var toPlayer = player.Transform.WorldPosition - Transform.WorldPosition;
        var toHandle = toPlayer - new Vector3(0.0f, 0.0f, 1.65f);
        if (new Vector2(toHandle.X, toHandle.Z).LengthSquared() > 2.2f * 2.2f)
            return;

        // Do not close the leaf through someone standing in the doorway.
        if (IsOpen && MathF.Abs(toPlayer.X) < 0.65f && toPlayer.Z is > -0.35f and < 2.25f)
            return;

        IsOpen = !IsOpen;
    }

    public override void FixedUpdate(float deltaTime)
    {
        var target = IsOpen ? -MathF.PI * 0.5f : 0.0f;
        _angle += Math.Clamp(target - _angle, -2.0f * deltaTime, 2.0f * deltaTime);
        Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, _angle);
    }

    public override void OnDestroy()
    {
        if (_interact is not null)
            _interact.Started -= OnInteract;
    }
}
