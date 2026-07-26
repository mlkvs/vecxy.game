using System.Numerics;
using Vecxy.Assets;
using Vecxy.Diagnostics;
using Vecxy.Editor;
using Vecxy.Input;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Game;

public sealed class Player : AComponent
{
    private readonly IInputManager _inputManager;
    private readonly IPhysicsSystem _physics;
    private readonly Vecxy.Kernel.IWindow _window;
    private readonly InputMap _input;

    private SceneObject? _cameraObject;
    private Camera? _camera;
    private RigidBody? _body;
    private CapsuleCollider? _collider;

    private float _yaw;
    private float _pitch;
    private bool _jumpWasPressed;

    [EditorProperty(Label = "Walk Speed", Order = 10)]
    public float WalkSpeed { get; set; } = 3.5f;

    [EditorProperty(Label = "Sprint Multiplier", Order = 11)]
    public float SprintMultiplier { get; set; } = 1.8f;

    [EditorProperty(Label = "Ground Acceleration", Order = 12)]
    public float GroundAcceleration { get; set; } = 24.0f;

    [EditorProperty(Label = "Ground Deceleration", Order = 13)]
    public float GroundDeceleration { get; set; } = 30.0f;

    [EditorProperty(Label = "Air Acceleration", Order = 14)]
    public float AirAcceleration { get; set; } = 8.0f;

    [EditorProperty(Label = "Jump Velocity", Order = 15)]
    public float JumpVelocity { get; set; } = 5.2f;

    [EditorProperty(Label = "Look Sensitivity", Order = 20)]
    public float LookSensitivity { get; set; } = 0.0025f;

    [EditorProperty(Label = "Maximum Pitch", Order = 21)]
    public float MaximumPitch { get; set; } = 1.45f;

    [EditorProperty(Label = "Height", Order = 30)]
    public float Height { get; set; } = 1.8f;

    [EditorProperty(Label = "Radius", Order = 31)]
    public float Radius { get; set; } = 0.35f;

    [EditorProperty(Label = "Eye Height", Order = 32)]
    public float EyeHeight { get; set; } = 1.62f;

    [EditorProperty(Label = "Ground Probe Distance", Order = 33)]
    public float GroundProbeDistance { get; set; } = 0.15f;

    [EditorProperty(Label = "View Ray Distance", Order = 34)]
    public float ViewRayDistance { get; set; } = 3.0f;

    public bool IsGrounded { get; private set; }

    public Vector3 Velocity =>
        _body?.Velocity ?? Vector3.Zero;

    public Vector3 ViewPosition =>
        _cameraObject?.Transform.WorldPosition
        ?? GetViewPosition();

    public Vector3 ViewForward =>
        _cameraObject?.Transform.Forward
        ?? Vector3.Transform(
            -Vector3.UnitZ,
            Quaternion.CreateFromYawPitchRoll(
                _yaw,
                _pitch,
                0.0f));

    public PhysicsRaycastHit? ViewHit { get; private set; }

    public Camera Camera =>
        _camera ??
        throw new InvalidOperationException("Player camera is not initialized.");

    public Player(
        Vecxy.Kernel.IWindow window,
        IInputManager inputManager,
        IPhysicsSystem physics,
        AssetRef<InputAsset> inputAsset)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(inputManager);
        ArgumentNullException.ThrowIfNull(physics);
        ArgumentNullException.ThrowIfNull(inputAsset);

        _window = window;
        _inputManager = inputManager;
        _physics = physics;
        _input = inputManager.Create(inputAsset, "Player");
    }

    protected override void Awake()
    {
        _body =
            SceneObject?.GetComponent<RigidBody>()
            ?? SceneObject?.AddComponent<RigidBody>();

        _collider =
            SceneObject?.GetComponent<CapsuleCollider>()
            ?? SceneObject?.AddComponent<CapsuleCollider>();

        if (_body is null || _collider is null)
        {
            throw new InvalidOperationException(
                "Player could not initialize its physics components.");
        }

        _body.MotionType = EPhysicsMotionType.Dynamic;
        _body.AffectedByGravity = true;
        _body.Mass = 80.0f;
        _body.Friction = 0.0f;
        _body.Restitution = 0.0f;
        _body.EnableSpeculativeContacts = true;

        var cylinderHeight = Math.Max(0.0f, Height - Radius * 2.0f);
        _collider.Radius = Radius;
        _collider.Height = cylinderHeight;
        _collider.Center = new Vector3(
            0.0f,
            Radius + cylinderHeight * 0.5f,
            0.0f);
    }
    

    protected override void Start()
    {
        _cameraObject = Scene.CreateObject("Player Camera");
        _camera = _cameraObject.AddComponent<Camera>();

        _camera.FieldOfView = 70.0f;
        _camera.NearPlane = 0.05f;
        _camera.FarPlane = 500.0f;
        _camera.UsePostProcessing = true;
        _camera.ClearColor = new Vector4(
            0.025f,
            0.035f,
            0.055f,
            1.0f);

        var forward = Transform.Forward;
        _yaw = MathF.Atan2(-forward.X, -forward.Z);
        _pitch = 0.0f;

        ApplyBodyRotation();
        SyncView();
        _window.SetCursorCaptured(true);
    }

    protected override void OnEnable()
    {
        _input.Enable();
    }

    protected override void OnDisable()
    {
        _input.Disable();
        _window.SetCursorCaptured(false);
    }

    protected override void Update(float deltaTime)
    {
        UpdateLook();
        UpdateGroundedState();
        UpdateMovement(deltaTime);
        ApplyBodyRotation();
    }

    protected override void OnDestroy()
    {
        _input.Dispose();

        if (_cameraObject is not null &&
            !_cameraObject.IsDestroyed)
        {
            _cameraObject.Destroy();
        }

        _cameraObject = null;
        _camera = null;
        _body = null;
        _collider = null;
        ViewHit = null;
    }

    protected override void OnGizmos(ISceneGizmoDrawer gizmos)
    {
        var origin = ViewPosition;
        var target = origin + ViewForward * ViewRayDistance;
        var color = ViewHit is null
            ? new Vector4(0.6f, 0.85f, 1.0f, 1.0f)
            : new Vector4(1.0f, 0.85f, 0.2f, 1.0f);

        gizmos.Line(origin, target, color, 1.5f);

        if (ViewHit is { } hit)
        {
            gizmos.WireSphere(
                hit.Point,
                0.05f,
                new Vector4(1.0f, 0.25f, 0.25f, 1.0f),
                10,
                1.5f);

            gizmos.Line(
                hit.Point,
                hit.Point + hit.Normal * 0.35f,
                new Vector4(0.25f, 1.0f, 0.4f, 1.0f),
                1.5f);
        }
    }

    public void SyncView()
    {
        if (_cameraObject is null)
            return;

        _cameraObject.Transform.WorldPosition = GetViewPosition();
        _cameraObject.Transform.WorldRotation =
            Quaternion.CreateFromYawPitchRoll(
                _yaw,
                _pitch,
                0.0f);

        UpdateViewRaycast();
    }

    private void UpdateLook()
    {
        if (!_window.IsCursorCaptured)
            return;

        var mouseDelta = _inputManager.MouseDelta;
        if (mouseDelta.LengthSquared() <= float.Epsilon)
            return;

        _yaw -= mouseDelta.X * LookSensitivity;
        _pitch -= mouseDelta.Y * LookSensitivity;
        _pitch = Math.Clamp(_pitch, -MaximumPitch, MaximumPitch);
    }

    private void UpdateGroundedState()
    {
        var probeOrigin = Transform.WorldPosition + Vector3.UnitY * (Radius + 0.05f);
        var probeDistance = Radius + GroundProbeDistance + 0.05f;

        if (_physics.Raycast(
                probeOrigin,
                -Vector3.UnitY,
                probeDistance,
                SceneObject,
                out var hit))
        {
            IsGrounded = hit.Normal.Y >= 0.35f;
            return;
        }

        IsGrounded = false;
    }

    private void UpdateMovement(float deltaTime)
    {
        if (_body is null)
            return;

        var move = _input.GetAction<Vector2>("Move").Value;
        if (move.LengthSquared() > 1.0f)
            move = Vector2.Normalize(move);

        var yawRotation =
            Quaternion.CreateFromAxisAngle(
                Vector3.UnitY,
                _yaw);

        var forward = Vector3.Transform(-Vector3.UnitZ, yawRotation);
        var right = Vector3.Transform(Vector3.UnitX, yawRotation);

        forward.Y = 0.0f;
        right.Y = 0.0f;

        if (forward.LengthSquared() > float.Epsilon)
            forward = Vector3.Normalize(forward);

        if (right.LengthSquared() > float.Epsilon)
            right = Vector3.Normalize(right);

        var direction = right * move.X + forward * move.Y;
        if (direction.LengthSquared() > 1.0f)
            direction = Vector3.Normalize(direction);

        var speed = WalkSpeed;
        if (_input.GetAction("Sprint").IsPressed)
            speed *= SprintMultiplier;

        var jumpPressed = _input.GetAction("Jump").IsPressed;

        var targetHorizontalVelocity = direction * speed;
        var currentVelocity = _body.Velocity;
        var currentHorizontalVelocity =
            new Vector3(
                currentVelocity.X,
                0.0f,
                currentVelocity.Z);

        float acceleration;
        if (direction.LengthSquared() <= float.Epsilon)
        {
            acceleration = IsGrounded
                ? GroundDeceleration
                : AirAcceleration;
        }
        else
        {
            acceleration = IsGrounded
                ? GroundAcceleration
                : AirAcceleration;
        }

        var nextHorizontalVelocity = MoveTowards(
            currentHorizontalVelocity,
            targetHorizontalVelocity,
            acceleration * deltaTime);

        var nextVerticalVelocity = currentVelocity.Y;
        if (IsGrounded && nextVerticalVelocity < -1.0f)
            nextVerticalVelocity = -1.0f;

        if (IsGrounded &&
            jumpPressed &&
            !_jumpWasPressed)
        {
            nextVerticalVelocity = JumpVelocity;
            IsGrounded = false;
        }

        _body.Velocity = new Vector3(
            nextHorizontalVelocity.X,
            nextVerticalVelocity,
            nextHorizontalVelocity.Z);
        _body.AngularVelocity = Vector3.Zero;
        _jumpWasPressed = jumpPressed;
    }

    private void ApplyBodyRotation()
    {
        var rotation = Quaternion.CreateFromAxisAngle(
            Vector3.UnitY,
            _yaw);

        Transform.WorldRotation = rotation;

    }

    private void UpdateViewRaycast()
    {
        ViewHit = null;

        if (_physics.Raycast(
                ViewPosition,
                ViewForward,
                ViewRayDistance,
                SceneObject,
                out var hit))
        {
            ViewHit = hit;
        }
    }

    private Vector3 GetViewPosition()
    {
        return Transform.WorldPosition +
               new Vector3(0.0f, EyeHeight, 0.0f);
    }

    private static Vector3 MoveTowards(
        Vector3 current,
        Vector3 target,
        float maximumDelta)
    {
        var difference = target - current;
        var distance = difference.Length();

        if (distance <= maximumDelta ||
            distance <= float.Epsilon)
        {
            return target;
        }

        return current +
               difference / distance *
               maximumDelta;
    }
}
