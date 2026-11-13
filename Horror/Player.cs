using System.Numerics;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Physics;
using Vecxy.Prototypes;
using Vecxy.Rendering;
using Vecxy.Scene;
using Vecxy.UI;

namespace Horror;

public sealed class Player : AComponent
{
    public sealed class Prototype(
        IWindow window,
        IInputManager inputManager,
        IInputCaptureState inputCapture,
        IUiManager ui) : APrototype<Player, Prototype.Options>
    {
        public sealed class Options
        {
            public InputMap? Input { get; init; }
            public Camera? Camera { get; init; }
            public IPhysicsSystem? Physics { get; init; }
            public float WalkSpeed { get; init; } = 2.5f;
            public float SprintMultiplier { get; init; } = 1.8f;
            public float GroundAcceleration { get; init; } = 24.0f;
            public float GroundDeceleration { get; init; } = 30.0f;
            public float AirAcceleration { get; init; } = 8.0f;
            public float JumpVelocity { get; init; } = 2.8f;
            public float LookSensitivity { get; init; } = 0.0025f;
            public float MaximumPitch { get; init; } = 1.45f;
            public float Height { get; init; } = 1.8f;
            public float Radius { get; init; } = 0.35f;
            public float EyeHeight { get; init; } = 1.6f;
            public float GroundProbeDistance { get; init; } = 0.15f;
        }

        protected override Player Instantiate(IPrototypeContext context)
        {
            var ctx = RequireContext<ScenePrototypeContext>(context);
            var playerObject = ctx.Object ?? ctx.CreateObject("Player");
            if (ctx.Object is null)
            {
                playerObject.Transform.Position = ctx.Position;
                playerObject.Transform.Rotation = ctx.Rotation;
            }

            return playerObject.AddComponent<Player>();
        }

        protected override void Configure(Player player, Options options)
        {
            player.Input = options.Input ?? throw new ArgumentNullException(nameof(options.Input));
            player.Camera = options.Camera ?? throw new ArgumentNullException(nameof(options.Camera));
            player.Window = window;
            player.InputManager = inputManager;
            player.InputCapture = inputCapture;
            player.Ui = ui;
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
    private IInputCaptureState InputCapture { get; set; } = null!;
    private IUiManager Ui { get; set; } = null!;
    private IPhysicsSystem Physics { get; set; } = null!;
    private InputAction? _releaseCursorAction;
    private InputAction? _captureCursorAction;
    private float _yaw;
    private float _pitch;
    private bool _jumpWasPressed;
    private bool _jumpRequested;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;

    public float WalkSpeed { get; private set; } = 2.5f;
    public float SprintMultiplier { get; private set; } = 1.8f;
    public float GroundAcceleration { get; private set; } = 24.0f;
    public float GroundDeceleration { get; private set; } = 30.0f;
    public float AirAcceleration { get; private set; } = 8.0f;
    public float JumpVelocity { get; private set; } = 2.8f;
    public float LookSensitivity { get; private set; } = 0.0025f;
    public float MaximumPitch { get; private set; } = 1.45f;
    public float Height { get; private set; } = 1.8f;
    public float Radius { get; private set; } = 0.35f;
    public float EyeHeight { get; private set; } = 1.6f;
    public float GroundProbeDistance { get; private set; } = 0.15f;
    public bool IsGrounded { get; private set; }

    public override void Start()
    {
        _spawnPosition = Transform.WorldPosition;
        _spawnRotation = Transform.WorldRotation;
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
        UpdateLook(deltaTime);
        var jumpPressed = Window.IsCursorCaptured && Input.GetAction("Jump").IsPressed;
        _jumpRequested |= jumpPressed && !_jumpWasPressed;
        _jumpWasPressed = jumpPressed;
    }

    public override void FixedUpdate(float deltaTime)
    {
        if (Transform.WorldPosition.Y < _spawnPosition.Y - 10.0f)
        {
            Physics.Teleport(Body, _spawnPosition, _spawnRotation);
            Body.Velocity = Vector3.Zero;
            Body.AngularVelocity = Vector3.Zero;
            var spawnForward = Vector3.Transform(-Vector3.UnitZ, _spawnRotation);
            _yaw = MathF.Atan2(-spawnForward.X, -spawnForward.Z);
            _pitch = 0.0f;
            _jumpRequested = false;
        }

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

    private void UpdateLook(float deltaTime)
    {
        if (!Window.IsCursorCaptured)
            return;

        if (InputCapture.SuppressMouseMotion)
        {
            foreach (var document in Ui.Documents)
                if (document.Canvas?.CursorWorldPosition is { } target)
                {
                    var direction = target - Camera.Transform.WorldPosition;
                    if (direction.LengthSquared() < 1e-8f) return;
                    direction = Vector3.Normalize(direction);
                    var yaw = MathF.Atan2(-direction.X,-direction.Z);
                    var pitch = Math.Clamp(MathF.Asin(Math.Clamp(direction.Y,-1,1)),-MaximumPitch,MaximumPitch);
                    var yawDelta = MathF.Atan2(MathF.Sin(yaw-_yaw),MathF.Cos(yaw-_yaw));
                    var blend = 1-MathF.Exp(-2.5f*deltaTime);
                    var maxStep = .65f*deltaTime;
                    _yaw += Math.Clamp(yawDelta*blend,-maxStep,maxStep);
                    _pitch += Math.Clamp((pitch-_pitch)*blend,-maxStep,maxStep);
                    return;
                }
            return;
        }
        if (InputCapture.SuppressMouse) return;

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
        var move = Window.IsCursorCaptured
            ? Input.GetAction<Vector2>("Move").Value
            : Vector2.Zero;
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

        if (Window.IsCursorCaptured && IsGrounded && _jumpRequested)
        {
            verticalVelocity = JumpVelocity;
            IsGrounded = false;
        }

        Body.Velocity = new Vector3(horizontalVelocity.X, verticalVelocity, horizontalVelocity.Z);
        Body.AngularVelocity = Vector3.Zero;
        _jumpRequested = false;
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
