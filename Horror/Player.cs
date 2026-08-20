using System.Numerics;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Horror;

public sealed class Player : AComponent
{
    public sealed class Prototype(
        IWindow window,
        IInputManager inputManager) : APrototype<Player, Prototype.Options>
    {
        public sealed class Options : IPrototype.IOptions
        {
            public InputMap? Input { get; init; }
            public Camera? Camera { get; init; }
            public IPhysicsSystem? Physics { get; init; }
            public float WalkSpeed { get; init; } = 3.5f;
            public float SprintMultiplier { get; init; } = 1.8f;
            public float GroundAcceleration { get; init; } = 24.0f;
            public float GroundDeceleration { get; init; } = 30.0f;
            public float AirAcceleration { get; init; } = 8.0f;
            public float JumpVelocity { get; init; } = 5.2f;
            public float LookSensitivity { get; init; } = 0.0025f;
            public float MaximumPitch { get; init; } = 1.45f;
            public float Height { get; init; } = 1.8f;
            public float Radius { get; init; } = 0.35f;
            public float EyeHeight { get; init; } = 1.62f;
            public float GroundProbeDistance { get; init; } = 0.15f;
        }

        protected override Player Instantiate(InstantiateContext ctx)
        {
            if (ctx.Parent is null)
                throw new InvalidOperationException("Player requires a parent object.");

            return ctx.Parent.AddComponent<Player>();
        }

        protected override void Configure(Player player, Options options)
        {
            player.Input = options.Input ?? throw new ArgumentNullException(nameof(options.Input));
            player.Camera = options.Camera ?? throw new ArgumentNullException(nameof(options.Camera));
            player.Window = window;
            player.InputManager = inputManager;
            player.Physics = options.Physics ?? throw new ArgumentNullException(nameof(options.Physics));
            player.WalkSpeed = options.WalkSpeed;
            player.SprintMultiplier = options.SprintMultiplier;
            player.GroundAcceleration = options.GroundAcceleration;
            player.GroundDeceleration = options.GroundDeceleration;
            player.AirAcceleration = options.AirAcceleration;
            player.JumpVelocity = options.JumpVelocity;
            player.LookSensitivity = options.LookSensitivity;
            player.MaximumPitch = options.MaximumPitch;
            player.Height = options.Height;
            player.Radius = options.Radius;
            player.EyeHeight = options.EyeHeight;
            player.GroundProbeDistance = options.GroundProbeDistance;

            var cylinderHeight = Math.Max(0.0f, player.Height - player.Radius * 2.0f);
            var collider = player.SceneObject!.AddComponent<CapsuleCollider>();
            collider.Radius = player.Radius;
            collider.Height = cylinderHeight;
            collider.Center = new Vector3(0.0f, player.Radius + cylinderHeight * 0.5f, 0.0f);
            collider.Material.Friction = 0.0f;
            collider.Material.Restitution = 0.0f;

            player.Body = player.SceneObject.AddComponent<RigidBody>();
            player.Body.MotionType = EPhysicsMotionType.Dynamic;
            player.Body.AffectedByGravity = true;
            player.Body.Mass = 80.0f;
            player.Body.EnableSpeculativeContacts = true;
        }
    }

    public InputMap Input { get; private set; } = null!;
    public Camera Camera { get; private set; } = null!;
    public RigidBody Body { get; private set; } = null!;
    private IWindow Window { get; set; } = null!;
    private IInputManager InputManager { get; set; } = null!;
    private IPhysicsSystem Physics { get; set; } = null!;
    private InputAction? _releaseCursorAction;
    private InputAction? _captureCursorAction;
    private float _yaw;
    private float _pitch;
    private bool _jumpWasPressed;

    public float WalkSpeed { get; private set; } = 3.5f;
    public float SprintMultiplier { get; private set; } = 1.8f;
    public float GroundAcceleration { get; private set; } = 24.0f;
    public float GroundDeceleration { get; private set; } = 30.0f;
    public float AirAcceleration { get; private set; } = 8.0f;
    public float JumpVelocity { get; private set; } = 5.2f;
    public float LookSensitivity { get; private set; } = 0.0025f;
    public float MaximumPitch { get; private set; } = 1.45f;
    public float Height { get; private set; } = 1.8f;
    public float Radius { get; private set; } = 0.35f;
    public float EyeHeight { get; private set; } = 1.62f;
    public float GroundProbeDistance { get; private set; } = 0.15f;
    public bool IsGrounded { get; private set; }

    public override void Start()
    {
        var forward = Transform.Forward;
        _yaw = MathF.Atan2(-forward.X, -forward.Z);
        ApplyBodyRotation();
        SyncView();

        _releaseCursorAction = Input.GetAction("ReleaseCursor");
        _releaseCursorAction.Started += OnReleaseCursor;
        _captureCursorAction = Input.GetAction("CaptureCursor");
        _captureCursorAction.Started += OnCaptureCursor;
        Window.SetCursorCaptured(true);
    }

    public override void Update(float deltaTime)
    {
        UpdateLook();
        UpdateGroundedState();
        UpdateMovement(deltaTime);
        ApplyBodyRotation();
    }

    public override void LateUpdate(float deltaTime) => SyncView();

    public override void OnDestroy()
    {
        if (_releaseCursorAction is not null)
            _releaseCursorAction.Started -= OnReleaseCursor;
        if (_captureCursorAction is not null)
            _captureCursorAction.Started -= OnCaptureCursor;

        Window.SetCursorCaptured(false);
    }

    private void OnReleaseCursor(InputActionContext context) => Window.SetCursorCaptured(false);
    private void OnCaptureCursor(InputActionContext context) => Window.SetCursorCaptured(true);

    private void UpdateLook()
    {
        if (!Window.IsCursorCaptured)
            return;

        var mouseDelta = InputManager.MouseDelta;
        if (mouseDelta.LengthSquared() <= float.Epsilon)
            return;

        _yaw -= mouseDelta.X * LookSensitivity;
        _pitch -= mouseDelta.Y * LookSensitivity;
        _pitch = Math.Clamp(_pitch, -MaximumPitch, MaximumPitch);
    }

    private void UpdateGroundedState()
    {
        var origin = Transform.WorldPosition + Vector3.UnitY * (Radius + 0.05f);
        var distance = Radius + GroundProbeDistance + 0.05f;
        IsGrounded = Physics.Raycast(origin, -Vector3.UnitY, distance, SceneObject, out var hit) &&
                     hit.Normal.Y >= 0.35f;
    }

    private void UpdateMovement(float deltaTime)
    {
        var move = Input.GetAction<Vector2>("Move").Value;
        if (move.LengthSquared() > 1.0f)
            move = Vector2.Normalize(move);

        var yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, _yaw);
        var forward = Vector3.Transform(-Vector3.UnitZ, yawRotation);
        var right = Vector3.Transform(Vector3.UnitX, yawRotation);
        var direction = right * move.X + forward * move.Y;
        if (direction.LengthSquared() > 1.0f)
            direction = Vector3.Normalize(direction);

        var speed = Input.GetAction("Sprint").IsPressed
            ? WalkSpeed * SprintMultiplier
            : WalkSpeed;
        var targetHorizontalVelocity = direction * speed;
        var currentVelocity = Body.Velocity;
        var currentHorizontalVelocity = new Vector3(currentVelocity.X, 0.0f, currentVelocity.Z);
        var acceleration = direction.LengthSquared() <= float.Epsilon
            ? IsGrounded ? GroundDeceleration : AirAcceleration
            : IsGrounded ? GroundAcceleration : AirAcceleration;
        var horizontalVelocity = MoveTowards(
            currentHorizontalVelocity,
            targetHorizontalVelocity,
            acceleration * deltaTime);

        var verticalVelocity = currentVelocity.Y;
        if (IsGrounded && verticalVelocity < -1.0f)
            verticalVelocity = -1.0f;

        var jumpPressed = Input.GetAction("Jump").IsPressed;
        if (IsGrounded && jumpPressed && !_jumpWasPressed)
        {
            verticalVelocity = JumpVelocity;
            IsGrounded = false;
        }

        Body.Velocity = new Vector3(horizontalVelocity.X, verticalVelocity, horizontalVelocity.Z);
        Body.AngularVelocity = Vector3.Zero;
        _jumpWasPressed = jumpPressed;
    }

    private void ApplyBodyRotation() =>
        Transform.WorldRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, _yaw);

    private void SyncView()
    {
        Camera.Transform.WorldPosition = Transform.WorldPosition + new Vector3(0.0f, EyeHeight, 0.0f);
        Camera.Transform.WorldRotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0.0f);
    }

    private static Vector3 MoveTowards(Vector3 current, Vector3 target, float maximumDelta)
    {
        var difference = target - current;
        var distance = difference.Length();
        return distance <= maximumDelta || distance <= float.Epsilon
            ? target
            : current + difference / distance * maximumDelta;
    }
}
