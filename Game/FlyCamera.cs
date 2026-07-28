using System.Numerics;
using Vecxy.Assets;
using Vecxy.Input;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Game;

public sealed class FlyCamera : AComponent
{
    private readonly IInputManager _inputManager;
    private readonly InputMap _input;

    private float _yaw;
    private float _pitch;

    public float MoveSpeed { get; set; } = 4.0f;
    public float SprintMultiplier { get; set; } = 2.0f;
    public float LookSensitivity { get; set; } = 0.0025f;
    public float MaximumPitch { get; set; } = 1.45f;
    public bool RequireLookButton { get; set; } = true;

    public FlyCamera(
        IInputManager input,
        AssetRef<InputAsset> inputAsset,
        string mapName = "Player")
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(inputAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapName);

        _inputManager = input;
        _input = input.Create(inputAsset, mapName);
    }

    public override void Awake()
    {
        if (SceneObject?.GetComponent<Camera>() is null)
        {
            throw new InvalidOperationException(
                "FlyCamera requires a Camera component on the same scene object.");
        }
    }

    public override void Start()
    {
        var forward = Transform.Forward;

        _yaw = MathF.Atan2(
            -forward.X,
            -forward.Z);
        _pitch = MathF.Asin(
            Math.Clamp(forward.Y, -1.0f, 1.0f));

        ApplyRotation();
    }

    public override void OnEnable()
    {
        _input.Enable();
    }

    public override void OnDisable()
    {
        _input.Disable();
    }

    public override void Update(float deltaTime)
    {
        UpdateLook();
        UpdateMovement(deltaTime);
    }

    public override void OnDestroy()
    {
        _input.Dispose();
    }

    private void UpdateLook()
    {
        if (RequireLookButton &&
            !_input.GetAction("Look").IsPressed)
        {
            return;
        }

        var mouseDelta = _inputManager.MouseDelta;
        if (mouseDelta.LengthSquared() <= float.Epsilon)
            return;

        _yaw -= mouseDelta.X * LookSensitivity;
        _pitch -= mouseDelta.Y * LookSensitivity;
        _pitch = Math.Clamp(
            _pitch,
            -MaximumPitch,
            MaximumPitch);

        ApplyRotation();
    }

    private void UpdateMovement(float deltaTime)
    {
        var move =
            _input.GetAction<Vector2>("Move").Value;

        if (move.LengthSquared() > 1.0f)
            move = Vector2.Normalize(move);

        var speed = MoveSpeed;
        if (_input.GetAction("Sprint").IsPressed)
            speed *= SprintMultiplier;

        var forward = Transform.Forward;
        var right = Transform.Right;

        var direction =
            right * move.X +
            forward * move.Y;

        if (direction.LengthSquared() > 1.0f)
            direction = Vector3.Normalize(direction);

        Transform.WorldPosition +=
            direction * speed * deltaTime;
    }

    private void ApplyRotation()
    {
        Transform.WorldRotation =
            Quaternion.CreateFromYawPitchRoll(
                _yaw,
                _pitch,
                0.0f);
    }
}
