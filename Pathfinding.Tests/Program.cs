using System.Numerics;
using Autofac;
using MemoryPack;
using Vecxy.Assets;
using Vecxy.Diagnostics;
using Vecxy.Engine;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Networking;
using Vecxy.Platforms;
using Vecxy.Rendering;
using Vecxy.Scene;
using Vecxy.UI;

namespace Pathfinding.Tests;

[App]
public sealed class App : IVEntry
{
    public void OnConfigureEngine(PlatformContext context, Engine.Options options)
    {
        options.Headless = Launch.IsServer;
        options.ShowSplashScreen = false;
        options.Window = new IWindow.Options($"Vecxy Arena — {Launch.Name}", Arena.Width, Arena.Height);
    }

    public void OnConfigureLayers(PlatformContext context, List<AAppLayer.IDefinition> layers)
    {
        if (Launch.IsServer) { layers.Add(new ShooterLayer.Definition(true)); return; }
        layers.Add(new EngineLayer.Definition(new AssetsModule.Options
        {
            AssetsDirectory = context.AssetsDirectory,
            HotReloadEnabled = true
        }));
        layers.Add(new ShooterLayer.Definition());
    }
}

internal static class Launch
{
    private static readonly string[] Args = Environment.GetCommandLineArgs();
    public static bool IsServer => Has("--server") || IsRole("Server");
    public static int Port => int.TryParse(Read("--port"), out var value) ? value : 7777;
    public static string Host => Read("--host") ?? "127.0.0.1";
    public static string Name => Read("--name") ?? Environment.GetEnvironmentVariable("VECXY_CLIENT_NAME") ?? "Player";
    private static bool Has(string value) => Args.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
    private static bool IsRole(string role) => string.Equals(Environment.GetEnvironmentVariable("VECXY_ROLE"), role, StringComparison.OrdinalIgnoreCase);
    private static string? Read(string key)
    {
        var index = Array.FindIndex(Args, x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < Args.Length ? Args[index + 1] : null;
    }
}

internal static class Arena
{
    public const int Width = 960;
    public const int Height = 640;
    public const float PlayerRadius = 14;
    public const float PlayerSpeed = 230;
    public const float BotSpeed = 145;
    public const float ShotRange = 700;
    public const float ShotRadius = 20;
    public const float ShotCooldown = .16f;
    public const float TickDelta = 1f / 60f;
    public const int SnapshotEveryTicks = 3;
}

[MemoryPackable]
public partial struct InputCommand
{
    public uint Sequence { get; set; }
    public uint SeenServerTick { get; set; }
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public float AimX { get; set; }
    public float AimY { get; set; }
    public bool Shoot { get; set; }
}

[MemoryPackable]
public partial struct EntityState
{
    public ulong Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float AimX { get; set; }
    public float AimY { get; set; }
    public int Health { get; set; }
    public int Score { get; set; }
    public bool IsBot { get; set; }
    public uint AckInputSequence { get; set; }
}

[MemoryPackable]
public partial struct WorldSnapshot
{
    public uint Tick { get; set; }
    public EntityState[] Entities { get; set; }
}

[MemoryPackable]
public partial struct ShotEvent
{
    public ulong ShooterId { get; set; }
    public float FromX { get; set; }
    public float FromY { get; set; }
    public float ToX { get; set; }
    public float ToY { get; set; }
    public ulong HitId { get; set; }
}

public sealed class ShooterNetworkBehaviour : NetworkBehaviour
{
    public event Action<RpcContext, InputCommand>? InputReceived;
    public event Action<WorldSnapshot>? SnapshotReceived;
    public event Action<ShotEvent>? ShotReceived;

    [ServerRpc(RequireAuthority = false, Channel = RpcChannel.Unreliable)]
    public void SubmitInput(InputCommand input, RpcContext context = default) => InputReceived?.Invoke(context, input);

    [ClientRpc(Channel = RpcChannel.Unreliable, Target = RpcTarget.Observers)]
    public void PublishSnapshot(WorldSnapshot snapshot) => SnapshotReceived?.Invoke(snapshot);

    [ClientRpc(Channel = RpcChannel.Reliable, Target = RpcTarget.Observers)]
    public void PublishShot(ShotEvent shot) => ShotReceived?.Invoke(shot);
}

[Layer("shooter")]
public sealed class ShooterLayer(
    INetworking networking,
    IEnumerable<IModule> modules,
    IUiManager? ui = null,
    IInputManager? input = null,
    IRenderer? renderer = null,
    ISceneManager? scenes = null,
    IAssetsManager? assets = null) : AAppLayer
{
    public sealed class Definition : ADefinition<ShooterLayer>
    {
        public override IReadOnlyList<Vecxy.Kernel.IDefinition> Children { get; }
        public Definition(bool server = false) => Children = server ? [new NetworkingModule.Definition()] : [];
        public override void RegisterGlobal(ContainerBuilder builder) => builder.RegisterType<ArenaScene>().AsSelf();
    }

    private readonly Dictionary<ulong, ServerEntity> _serverEntities = [];
    private readonly Dictionary<ulong, EntityView> _views = [];
    private readonly Dictionary<uint, EntityState[]> _history = [];
    private readonly List<InputCommand> _pendingInputs = [];
    private NetworkObject? _networkObject;
    private ShooterNetworkBehaviour? _networkGame;
    private UiDocument? _document;
    private UiText? _status;
    private AssetRef<TextureAsset>? _whiteTexture;
    private SceneInstance? _scene;
    private SceneObject? _tracerObject;
    private SpriteRenderer? _tracerSprite;
    private ulong _localId;
    private uint _serverTick;
    private uint _inputSequence;
    private uint _lastSnapshotTick;
    private float _serverAccumulator;
    private float _inputAccumulator;
    private float _botSpawnTimer;
    private Vector2 _predictedPosition;
    private DateTime _tracerUntil;

    public override void OnInitialize()
    {
        _networkObject = networking.CreateObject(new NetworkObjectId(1));
        _networkGame = new ShooterNetworkBehaviour();
        _networkObject.AddBehaviour(_networkGame);
        if (Launch.IsServer)
        {
            foreach (var module in modules) module.OnInitialize();
            networking.Connected += OnConnected;
            networking.Disconnected += OnDisconnected;
            _networkGame.InputReceived += OnInput;
            _ = StartServerAsync();
            return;
        }

        _networkGame.SnapshotReceived += OnSnapshot;
        _networkGame.ShotReceived += OnShot;
        _whiteTexture = assets!.Load<TextureAsset>(Assets.Engine.Textures.TQuadWhite64);
        _scene = scenes!.LoadScene<ArenaScene>();
        CreateArenaSprites();
        _document = ui!.Load(Assets.UI.World);
        _document.Reloaded += BuildUi;
        BuildUi(_document);
        _ = ConnectAsync();
    }

    public override void OnUpdate(float deltaTime)
    {
        if (Launch.IsServer) { ServerUpdate(Math.Min(deltaTime, .1f)); return; }
        ClientUpdate(Math.Min(deltaTime, .1f));
    }

    public override void OnUnload()
    {
        networking.Connected -= OnConnected;
        networking.Disconnected -= OnDisconnected;
        if (_networkGame is not null)
        {
            _networkGame.InputReceived -= OnInput;
            _networkGame.SnapshotReceived -= OnSnapshot;
            _networkGame.ShotReceived -= OnShot;
        }
        if (_document is not null) { _document.Reloaded -= BuildUi; ui!.Unload(_document); }
        _whiteTexture?.Dispose();
        _whiteTexture = null;
        if (Launch.IsServer) foreach (var module in modules) module.OnShutdown();
    }

    private async Task StartServerAsync()
    {
        await networking.StartServerAsync(Launch.Port);
        Logger.Info($"[Arena] Authoritative UDP server listening on {Launch.Port}.");
    }

    private async Task ConnectAsync()
    {
        try
        {
            await networking.ConnectAsync(Launch.Host, Launch.Port);
            _localId = networking.LocalConnection!.Id;
            SetStatus($"Connected as {Launch.Name} (#{_localId})");
        }
        catch (Exception exception) { SetStatus($"Connection failed: {exception.Message}"); }
    }

    private void OnConnected(NetworkConnection connection)
    {
        _networkObject!.AddObserver(connection);
        _serverEntities[connection.Id] = SpawnEntity(connection.Id, false);
        Logger.Info($"[Arena] Player {connection.Id} joined.");
    }

    private void OnDisconnected(NetworkConnection connection)
    {
        _networkObject!.RemoveObserver(connection);
        _serverEntities.Remove(connection.Id);
    }

    private void OnInput(RpcContext context, InputCommand input)
    {
        if (!_serverEntities.TryGetValue(context.Sender.Id, out var entity)) return;
        if (input.Sequence <= entity.LastInput.Sequence) return;
        input.MoveX = Math.Clamp(input.MoveX, -1, 1);
        input.MoveY = Math.Clamp(input.MoveY, -1, 1);
        entity.LastInput = input;
    }

    private void ServerUpdate(float deltaTime)
    {
        foreach (var module in modules) if (module is IModule.IUpdatable updatable) updatable.OnUpdate(deltaTime);
        _serverAccumulator += deltaTime;
        while (_serverAccumulator >= Arena.TickDelta)
        {
            _serverAccumulator -= Arena.TickDelta;
            ServerTick();
        }
    }

    private void ServerTick()
    {
        _serverTick++;
        _botSpawnTimer += Arena.TickDelta;
        if (_botSpawnTimer >= 2f && _serverEntities.Values.Count(x => x.IsBot) < 6)
        {
            _botSpawnTimer = 0;
            var id = 1_000_000UL + _serverTick;
            _serverEntities[id] = SpawnEntity(id, true);
        }

        foreach (var entity in _serverEntities.Values.ToArray())
        {
            entity.ShotCooldown = Math.Max(0, entity.ShotCooldown - Arena.TickDelta);
            if (entity.Respawn > 0)
            {
                entity.Respawn -= Arena.TickDelta;
                if (entity.Respawn <= 0) Respawn(entity);
                continue;
            }
            var input = entity.IsBot ? ThinkBot(entity) : entity.LastInput;
            SimulateMovement(ref entity.Position, input, entity.IsBot ? Arena.BotSpeed : Arena.PlayerSpeed, Arena.TickDelta);
            entity.Aim = SafeNormal(new Vector2(input.AimX, input.AimY));
            if (input.Shoot && entity.ShotCooldown <= 0) Fire(entity, input.SeenServerTick);
        }

        _history[_serverTick] = CreateStates();
        while (_history.Count > 120) _history.Remove(_history.Keys.Min());
        if (_serverTick % Arena.SnapshotEveryTicks == 0)
            _networkGame!.PublishSnapshot(new WorldSnapshot { Tick = _serverTick, Entities = CreateStates() });
    }

    private InputCommand ThinkBot(ServerEntity bot)
    {
        var targets = _serverEntities.Values.Where(x => x.Id != bot.Id && x.Respawn <= 0).ToArray();
        if (targets.Length == 0) return default;
        var target = targets.MinBy(x => Vector2.DistanceSquared(bot.Position, x.Position))!;
        var delta = target.Position - bot.Position;
        var direction = SafeNormal(delta);
        var strafe = new Vector2(-direction.Y, direction.X) * MathF.Sin((_serverTick + bot.Id) * .025f);
        var movement = SafeNormal(direction * .65f + strafe * .55f);
        return new InputCommand
        {
            MoveX = movement.X, MoveY = movement.Y, AimX = direction.X, AimY = direction.Y,
            Shoot = delta.LengthSquared() < 430 * 430, SeenServerTick = _serverTick
        };
    }

    private void Fire(ServerEntity shooter, uint rewindTick)
    {
        shooter.ShotCooldown = shooter.IsBot ? .5f : Arena.ShotCooldown;
        var direction = shooter.Aim == Vector2.Zero ? Vector2.UnitX : shooter.Aim;
        var states = FindHistory(rewindTick);
        ulong hitId = 0;
        float hitDistance = Arena.ShotRange;
        foreach (var target in states)
        {
            if (target.Id == shooter.Id || target.Health <= 0) continue;
            var offset = new Vector2(target.X, target.Y) - shooter.Position;
            var along = Vector2.Dot(offset, direction);
            if (along < 0 || along > hitDistance) continue;
            var perpendicular = offset - direction * along;
            if (perpendicular.LengthSquared() <= Arena.ShotRadius * Arena.ShotRadius) { hitId = target.Id; hitDistance = along; }
        }
        var end = shooter.Position + direction * hitDistance;
        if (hitId != 0 && _serverEntities.TryGetValue(hitId, out var hit))
        {
            hit.Health -= 25;
            if (hit.Health <= 0) { hit.Respawn = 2f; shooter.Score++; }
        }
        _networkGame!.PublishShot(new ShotEvent
        {
            ShooterId = shooter.Id, FromX = shooter.Position.X, FromY = shooter.Position.Y,
            ToX = end.X, ToY = end.Y, HitId = hitId
        });
    }

    private EntityState[] FindHistory(uint tick)
    {
        if (_history.Count == 0) return CreateStates();
        var clamped = Math.Clamp(tick, _history.Keys.Min(), _history.Keys.Max());
        return _history.TryGetValue(clamped, out var state) ? state : _history[_history.Keys.MinBy(x => Math.Abs((long)x - clamped))];
    }

    private EntityState[] CreateStates() => _serverEntities.Values.Select(x => new EntityState
    {
        Id = x.Id, X = x.Position.X, Y = x.Position.Y, AimX = x.Aim.X, AimY = x.Aim.Y,
        Health = x.Health, Score = x.Score, IsBot = x.IsBot, AckInputSequence = x.LastInput.Sequence
    }).ToArray();

    private static ServerEntity SpawnEntity(ulong id, bool bot)
    {
        var entity = new ServerEntity { Id = id, IsBot = bot };
        Respawn(entity);
        return entity;
    }

    private static void Respawn(ServerEntity entity)
    {
        entity.Position = new Vector2(Random.Shared.Next(50, Arena.Width - 50), Random.Shared.Next(70, Arena.Height - 50));
        entity.Health = 100;
        entity.Respawn = 0;
        entity.ShotCooldown = 1;
    }

    private void ClientUpdate(float deltaTime)
    {
        if (_localId == 0 || _networkGame is null) return;
        var movement = Movement();
        var aim = SafeNormal(PointerInArena() - _predictedPosition);
        SimulateMovement(ref _predictedPosition, new InputCommand { MoveX = movement.X, MoveY = movement.Y }, Arena.PlayerSpeed, deltaTime);
        _inputAccumulator += deltaTime;
        if (_inputAccumulator >= 1f / 30f)
        {
            _inputAccumulator = 0;
            var command = new InputCommand
            {
                Sequence = ++_inputSequence, SeenServerTick = _lastSnapshotTick,
                MoveX = movement.X, MoveY = movement.Y, AimX = aim.X, AimY = aim.Y,
                Shoot = input!.IsMouseButtonPressed(EMouseButton.Left)
            };
            _pendingInputs.Add(command);
            _networkGame.SubmitInput(command);
        }
        RenderViews(deltaTime, aim);
        if (_tracerObject is not null) _tracerObject.Enabled = DateTime.UtcNow < _tracerUntil;
    }

    private void OnSnapshot(WorldSnapshot snapshot)
    {
        if (snapshot.Tick <= _lastSnapshotTick) return;
        _lastSnapshotTick = snapshot.Tick;
        var live = snapshot.Entities.Select(x => x.Id).ToHashSet();
        foreach (var id in _views.Keys.Where(x => !live.Contains(x)).ToArray()) RemoveView(id);
        foreach (var state in snapshot.Entities)
        {
            var view = GetView(state);
            view.Previous = view.Target;
            view.Target = new Vector2(state.X, state.Y);
            view.Interpolation = 0;
            view.State = state;
            if (state.Id != _localId) continue;
            _pendingInputs.RemoveAll(x => x.Sequence <= state.AckInputSequence);
            _predictedPosition = view.Target;
            foreach (var input in _pendingInputs) SimulateMovement(ref _predictedPosition, input, Arena.PlayerSpeed, 1f / 30f);
        }
    }

    private void RenderViews(float deltaTime, Vector2 localAim)
    {
        foreach (var view in _views.Values)
        {
            Vector2 position;
            if (view.State.Id == _localId) position = _predictedPosition;
            else
            {
                view.Interpolation = Math.Min(1, view.Interpolation + deltaTime / .1f);
                position = Vector2.Lerp(view.Previous, view.Target, Smooth(view.Interpolation));
            }
            view.Root.Transform.Position = ToWorld(position, 0);
            var aim = view.State.Id == _localId ? localAim : new Vector2(view.State.AimX, view.State.AimY);
            view.Gun.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.Atan2(aim.Y, aim.X));
            view.Health.Transform.Scale = new Vector3(30f / 64f * Math.Clamp(view.State.Health / 100f, 0, 1), 4f / 64f, 1);
            var color = view.Body.Color;
            view.Body.Color = color with { W = view.State.Health > 0 ? 1 : .25f };
        }
        if (_views.TryGetValue(_localId, out var local))
            SetStatus($"{Launch.Name}  HP {Math.Max(0, local.State.Health)}  Kills {local.State.Score}  |  WASD + mouse");
    }

    private void OnShot(ShotEvent shot)
    {
        if (_tracerObject is null || _tracerSprite is null) return;
        var from = new Vector2(shot.FromX, shot.FromY);
        var to = new Vector2(shot.ToX, shot.ToY);
        var delta = to - from;
        _tracerObject.Transform.Position = ToWorld(from, 1);
        _tracerObject.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.Atan2(delta.Y, delta.X));
        _tracerObject.Transform.Scale = new Vector3(delta.Length() / 64f, 2f / 64f, 1);
        _tracerSprite.Color = shot.HitId == 0 ? new Vector4(1, .72f, .25f, 1) : new Vector4(1, .18f, .15f, 1);
        _tracerObject.Enabled = true;
        _tracerUntil = DateTime.UtcNow.AddMilliseconds(70);
    }

    private void BuildUi(UiDocument document)
    {
        _status = document.Query<UiText>("#status");
    }

    private EntityView GetView(EntityState state)
    {
        if (_views.TryGetValue(state.Id, out var view)) return view;
        var root = _scene!.CreateObject(state.IsBot ? $"Bot {state.Id}" : $"Player {state.Id}");
        var body = AddSprite(root, state.IsBot ? new Vector4(.78f, .16f, .18f, 1) : state.Id == _localId ? new Vector4(.12f, .85f, .38f, 1) : new Vector4(.12f, .48f, .9f, 1), 3);
        body.PixelsPerUnit = 64f / 28f;
        var gun = root.CreateChild("Gun");
        gun.Transform.Position = new Vector3(10, 0, 0);
        gun.Transform.Scale = new Vector3(25f / 64f, 6f / 64f, 1);
        var gunSprite = AddSprite(gun, new Vector4(.95f, .88f, .68f, 1), 4);
        gunSprite.Pivot = new Vector2(0, .5f);
        var healthBack = root.CreateChild("Health background");
        healthBack.Transform.Position = new Vector3(-15, 20, 0);
        healthBack.Transform.Scale = new Vector3(30f / 64f, 4f / 64f, 1);
        var backSprite = AddSprite(healthBack, new Vector4(.18f, .04f, .05f, 1), 5);
        backSprite.Pivot = new Vector2(0, .5f);
        var health = root.CreateChild("Health");
        health.Transform.Position = new Vector3(-15, 20, 0);
        health.Transform.Scale = new Vector3(30f / 64f, 4f / 64f, 1);
        var healthSprite = AddSprite(health, new Vector4(.2f, .9f, .35f, 1), 6);
        healthSprite.Pivot = new Vector2(0, .5f);
        view = new EntityView { Root = root, Gun = gun, Health = health, Body = body, State = state, Previous = new(state.X, state.Y), Target = new(state.X, state.Y) };
        _views[state.Id] = view;
        return view;
    }

    private void RemoveView(ulong id)
    {
        if (!_views.Remove(id, out var view)) return;
        view.Root.Destroy();
    }

    private Vector2 Movement()
    {
        var value = new Vector2(
            (input!.IsKeyPressed(EKeyboardKey.D) ? 1 : 0) - (input.IsKeyPressed(EKeyboardKey.A) ? 1 : 0),
            (input.IsKeyPressed(EKeyboardKey.S) ? 1 : 0) - (input.IsKeyPressed(EKeyboardKey.W) ? 1 : 0));
        return SafeNormal(value);
    }

    private Vector2 PointerInArena()
    {
        if (renderer!.TryCreateCameraRay(input!.MousePosition, out var ray) && Math.Abs(ray.Direction.Z) > .0001f)
        {
            var distance = -ray.Origin.Z / ray.Direction.Z;
            var point = ray.Origin + ray.Direction * distance;
            return new Vector2(point.X, Arena.Height - point.Y);
        }
        return _predictedPosition + Vector2.UnitX;
    }

    private void CreateArenaSprites()
    {
        var background = _scene!.CreateObject("Arena background");
        background.Transform.Position = new Vector3(Arena.Width / 2f, Arena.Height / 2f, -1);
        background.Transform.Scale = new Vector3(Arena.Width / 64f, Arena.Height / 64f, 1);
        AddSprite(background, new Vector4(.035f, .07f, .1f, 1), -10);
        _tracerObject = _scene.CreateObject("Shot tracer");
        _tracerSprite = AddSprite(_tracerObject, Vector4.One, 2);
        _tracerSprite.Pivot = new Vector2(0, .5f);
        _tracerObject.Enabled = false;
    }

    private SpriteRenderer AddSprite(SceneObject sceneObject, Vector4 color, int order)
    {
        var sprite = sceneObject.AddComponent<SpriteRenderer>();
        sprite.SetTexture(_whiteTexture!);
        sprite.PixelsPerUnit = 1;
        sprite.Color = color;
        sprite.SortingLayer = order;
        return sprite;
    }

    private static Vector3 ToWorld(Vector2 position, float z) => new(position.X, Arena.Height - position.Y, z);

    private void SetStatus(string text) { if (_status is not null) _status.Text = text; }
    private static void SimulateMovement(ref Vector2 position, InputCommand input, float speed, float deltaTime)
    {
        var movement = SafeNormal(new Vector2(input.MoveX, input.MoveY));
        position += movement * speed * deltaTime;
        position.X = Math.Clamp(position.X, Arena.PlayerRadius, Arena.Width - Arena.PlayerRadius);
        position.Y = Math.Clamp(position.Y, 52 + Arena.PlayerRadius, Arena.Height - Arena.PlayerRadius);
    }
    private static Vector2 SafeNormal(Vector2 value) => value.LengthSquared() > .0001f ? Vector2.Normalize(value) : Vector2.Zero;
    private static float Smooth(float value) => value * value * (3 - 2 * value);

    private sealed class ServerEntity
    {
        public ulong Id; public bool IsBot; public Vector2 Position; public Vector2 Aim = Vector2.UnitX;
        public int Health = 100; public int Score; public float ShotCooldown; public float Respawn; public InputCommand LastInput;
    }
    private sealed class EntityView
    {
        public required SceneObject Root; public required SceneObject Gun; public required SceneObject Health; public required SpriteRenderer Body;
        public EntityState State; public Vector2 Previous; public Vector2 Target; public float Interpolation;
    }
}

public sealed class ArenaScene : IScene
{
    public void OnLoad(SceneInstance scene)
    {
        scene.Lighting.Skybox.Enabled = false;
        var cameraObject = scene.CreateObject("Arena camera");
        cameraObject.Transform.Position = new Vector3(Arena.Width / 2f, Arena.Height / 2f, 10);
        var camera = cameraObject.AddComponent<Camera>();
        camera.Projection = ECameraProjection.Orthographic;
        camera.OrthographicSize = Arena.Height / 2f;
        camera.NearPlane = .1f;
        camera.FarPlane = 100;
        camera.ClearColor = new Vector4(.02f, .035f, .05f, 1);
    }

    public void OnUnload(SceneInstance scene) { }
}
