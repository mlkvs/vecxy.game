using System.Numerics;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Scene;

namespace Sponza;

public sealed class FreeFlyCamera : AComponent
{
    public sealed class Prototype(
        IWindow window,
        IInputManager inputManager) : APrototype<FreeFlyCamera, Prototype.Options>
    {
        public sealed class Options : IPrototype.IOptions
        {
            public InputMap? Input { get; init; }
            public float MoveSpeed { get; init; } = 5.0f;
            public float SprintMultiplier { get; init; } = 4.0f;
            public float LookSensitivity { get; init; } = 0.0025f;
            public float MaximumPitch { get; init; } = 1.55f;
        }

        protected override FreeFlyCamera Instantiate(InstantiateContext ctx)
        {
            if (ctx.Parent is null)
                throw new InvalidOperationException("FreeFlyCamera requires a parent object.");

            return ctx.Parent.AddComponent<FreeFlyCamera>();
        }

        protected override void Configure(FreeFlyCamera controller, Options options)
        {
            controller.Input = options.Input ?? throw new ArgumentNullException(nameof(options.Input));
            controller.Window = window;
            controller.InputManager = inputManager;
            controller.MoveSpeed = options.MoveSpeed;
            controller.SprintMultiplier = options.SprintMultiplier;
            controller.LookSensitivity = options.LookSensitivity;
            controller.MaximumPitch = options.MaximumPitch;
        }
    }

    private InputMap Input { get; set; } = null!;
    private IWindow Window { get; set; } = null!;
    private IInputManager InputManager { get; set; } = null!;
    private float _yaw;
    private float _pitch;

    public float MoveSpeed { get; private set; }
    public float SprintMultiplier { get; private set; }
    public float LookSensitivity { get; private set; }
    public float MaximumPitch { get; private set; }

    public override void Start()
    {
        var forward = Transform.Forward;
        _yaw = MathF.Atan2(-forward.X, -forward.Z);
        _pitch = MathF.Asin(Math.Clamp(forward.Y, -1.0f, 1.0f));
    }

    public override void Update(float deltaTime)
    {
        UpdateLook();
        UpdateMovement(deltaTime);
        Transform.WorldRotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0.0f);
    }

    private void UpdateLook()
    {
        if (!Window.IsCursorCaptured)
            return;

        var mouseDelta = InputManager.MouseDelta;
        _yaw -= mouseDelta.X * LookSensitivity;
        _pitch = Math.Clamp(
            _pitch - mouseDelta.Y * LookSensitivity,
            -MaximumPitch,
            MaximumPitch);
    }

    private void UpdateMovement(float deltaTime)
    {
        var move = Input.GetAction<Vector2>("Move").Value;
        if (move.LengthSquared() > 1.0f)
            move = Vector2.Normalize(move);

        var rotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0.0f);
        var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        var right = Vector3.Transform(Vector3.UnitX, rotation);
        var vertical = Input.GetAction("Up").IsPressed ? 1.0f : 0.0f;
        vertical -= Input.GetAction("Down").IsPressed ? 1.0f : 0.0f;

        var direction = right * move.X + forward * move.Y + Vector3.UnitY * vertical;
        if (direction.LengthSquared() > 1.0f)
            direction = Vector3.Normalize(direction);

        var speed = Input.GetAction("Sprint").IsPressed
            ? MoveSpeed * SprintMultiplier
            : MoveSpeed;
        Transform.WorldPosition += direction * speed * deltaTime;
    }
}
