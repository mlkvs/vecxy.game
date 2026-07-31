using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Autofac;
using ImGuiNET;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Editor;
using Vecxy.Engine;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Sandbox;

[UsedImplicitly]
public sealed class MultiplayerLayer(
    IAssetsManager assets,
    IInputManager input,
    IPhysicsSystem physics,
    ISceneManager scenes,
    ISceneFactory sceneFactory,
    ISceneInstantiator sceneInstantiator,
    IWindow window,
    IEditorGui editorGui) : AAppLayer
{
    public sealed class Definition : ADefinition<MultiplayerLayer>;

    private readonly string _localPlayerId =
        $"{Environment.MachineName}-{Environment.ProcessId}";
    private readonly string _localPlayerName =
        $"{Environment.UserName}@{Environment.MachineName}";

    private DiscoveryBrowser? _discovery;
    private MultiplayerHostRuntime? _host;
    private MultiplayerClientConnection? _connection;
    private MultiplayerClientWorld? _world;
    private string? _status;
    private bool _windowRegistered;

    public override void OnInitialize()
    {
        _discovery = new DiscoveryBrowser();
        _discovery.Start();
        editorGui.RegisterWindow("Multiplayer", DrawWindow);
        _windowRegistered = true;
    }

    public override void OnUpdate(float deltaTime)
    {
        _connection?.Pump();

        if (_connection?.LatestSnapshot is { } snapshot)
        {
            _world ??= new MultiplayerClientWorld(
                assets,
                input,
                physics,
                scenes,
                sceneFactory,
                sceneInstantiator,
                window);
            _world.EnsureLoaded(_localPlayerId);
            _world.ApplySnapshot(snapshot, _localPlayerId);
            _connection.SendLocalPlayerState(_world.CreateLocalPlayerState(_localPlayerId, _localPlayerName));
        }

        if (_connection is not null &&
            _connection.Failure is not null)
        {
            _status = $"Disconnected: {_connection.Failure.Message}";
            DisconnectClientOnly();
        }
    }

    public override void OnUnload()
    {
        if (_windowRegistered)
            editorGui.UnregisterWindow(DrawWindow);

        Disconnect();
        _discovery?.Dispose();
        _discovery = null;
    }

    private void DrawWindow()
    {
        var open = true;
        if (!ImGui.Begin("Multiplayer", ref open, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        ImGui.Text($"Local Player: {_localPlayerName}");
        ImGui.TextWrapped("F12 toggles the editor overlay. Close it after joining to control the local first-person player.");

        if (_status is not null)
            ImGui.TextWrapped(_status);

        if (_host is null)
        {
            if (ImGui.Button("Create Host"))
                CreateHost();
        }
        else
        {
            ImGui.Text($"Hosting: {_host.SessionName} on tcp:{_host.TcpPort}");
        }

        ImGui.Separator();
        ImGui.Text("Available Worlds");

        if (_discovery is not null)
        {
            foreach (var session in _discovery.Sessions)
            {
                ImGui.PushID(session.SessionId);
                ImGui.Text($"{session.Name}  [{session.PlayerCount}/2]  {session.Address}:{session.TcpPort}");
                ImGui.SameLine();

                var canJoin =
                    _connection is null &&
                    session.PlayerCount < 2;

                if (!canJoin)
                    ImGui.BeginDisabled();

                if (ImGui.Button("Join"))
                    Join(session);

                if (!canJoin)
                    ImGui.EndDisabled();

                ImGui.PopID();
            }
        }

        ImGui.Separator();

        if (_connection is not null)
        {
            if (ImGui.Button("Disconnect"))
                Disconnect();
        }

        ImGui.End();
    }

    private void CreateHost()
    {
        Disconnect();

        var sessionName =
            $"Vecxy World {DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";
        _host = new MultiplayerHostRuntime(sessionName);
        _host.Start();
        _status = $"Host started: {sessionName}";
        Join(
            new DiscoverySession(
                _host.SessionId,
                _host.SessionName,
                "127.0.0.1",
                _host.TcpPort,
                _host.PlayerCount,
                DateTime.UtcNow));
    }

    private void Join(DiscoverySession session)
    {
        try
        {
            DisconnectClientOnly();

            _connection = new MultiplayerClientConnection(
                session.Address,
                session.TcpPort,
                _localPlayerId,
                _localPlayerName);
            _connection.Connect();
            _status = $"Connected to {session.Name}";
        }
        catch (Exception exception)
        {
            _status = $"Join failed: {exception.Message}";
            DisconnectClientOnly();
        }
    }

    private void Disconnect()
    {
        DisconnectClientOnly();

        _host?.Dispose();
        _host = null;
    }

    private void DisconnectClientOnly()
    {
        _connection?.Dispose();
        _connection = null;
        scenes.UnloadActiveScene();
        _world?.Dispose();
        _world = null;
    }
}

internal sealed class MultiplayerClientWorld(
    IAssetsManager assets,
    IInputManager input,
    IPhysicsSystem physics,
    ISceneManager scenes,
    ISceneFactory sceneFactory,
    ISceneInstantiator sceneInstantiator,
    IWindow window) : IDisposable
{
    private SceneInstance? _scene;
    private AssetRef<InputAsset>? _inputAsset;
    private AssetRef<Model>? _planeModel;
    private AssetRef<Model>? _boxModel;
    private AssetRef<Material>? _material;
    private Player? _localPlayer;
    private SceneObject? _cubeVisual;
    private readonly Dictionary<string, SceneObject> _remotePlayers = [];

    public void EnsureLoaded(string localPlayerId)
    {
        if (_scene is not null)
            return;

        _inputAsset = assets.Load<InputAsset>("Controls.input");
        _planeModel = assets.Load<Model>("Models/Plane.glb");
        _boxModel = assets.Load<Model>("Models/BoxTextured.glb");
        _material = assets.Load<Material>("Materials/Default.material");
        _scene = sceneFactory.Create();

        var floor = sceneInstantiator.InstantiateModel(
            _scene,
            _planeModel.Value,
            "NetworkFloor",
            _material.Value);
        floor.Transform.Scale = new Vector3(20.0f, 1.0f, 20.0f);
        floor.Transform.Position = Vector3.Zero;
        floor.IsStatic = true;
        var floorCollider = floor.AddComponent<BoxCollider>();
        floorCollider.Size = new Vector3(20.0f, 0.25f, 20.0f);
        floorCollider.Center = new Vector3(0.0f, -0.125f, 0.0f);
        floorCollider.CollisionLayer = "world";

        _cubeVisual = sceneInstantiator.InstantiateModel(
            _scene,
            _boxModel.Value,
            "SyncedCube",
            _material.Value);
        _cubeVisual.Transform.Scale = new Vector3(0.75f);

        var light = _scene.CreateObject("Sun");
        var sun = light.AddComponent<DirectionalLight>();
        sun.Color = Vector3.One;
        sun.Intensity = 500.0f;
        light.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.4f, -0.9f, 0.0f);

        var playerObject = _scene.CreateObject("LocalPlayer");
        playerObject.Transform.Position = new Vector3(-2.0f, 1.25f, 0.0f);
        _localPlayer = playerObject.AddComponent(
            new Player(
                window,
                input,
                physics,
                _inputAsset));
        _localPlayer.WalkSpeed = 4.0f;
        _localPlayer.SprintMultiplier = 1.6f;
        _localPlayer.EyeHeight = 1.62f;

        scenes.SetActiveScene(_scene);
    }

    public void ApplySnapshot(ServerSnapshot snapshot, string localPlayerId)
    {
        if (_scene is null || _cubeVisual is null)
            return;

        _cubeVisual.Transform.Position = snapshot.Cube.Position.ToVector3();
        _cubeVisual.Transform.Rotation = snapshot.Cube.Rotation.ToQuaternion();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var player in snapshot.Players)
        {
            if (player.PlayerId == localPlayerId)
                continue;

            seen.Add(player.PlayerId);

            if (!_remotePlayers.TryGetValue(player.PlayerId, out var avatar))
            {
                avatar = sceneInstantiator.InstantiateModel(
                    _scene,
                    _boxModel!.Value,
                    $"Remote-{player.DisplayName}",
                    _material!.Value);
                avatar.Transform.Scale = new Vector3(0.5f, 1.8f, 0.5f);
                _remotePlayers.Add(player.PlayerId, avatar);
            }

            avatar.Transform.Position = player.Position.ToVector3() + new Vector3(0.0f, 0.9f, 0.0f);
            avatar.Transform.Rotation = player.Rotation.ToQuaternion();
        }

        foreach (var stale in _remotePlayers.Keys.Except(seen).ToArray())
        {
            _remotePlayers[stale].Destroy();
            _remotePlayers.Remove(stale);
        }
    }

    public PlayerStateMessage CreateLocalPlayerState(
        string playerId,
        string displayName)
    {
        if (_localPlayer?.SceneObject is not { } playerObject)
        {
            return new PlayerStateMessage(
                playerId,
                displayName,
                NetVector3.Zero,
                NetQuaternion.Identity);
        }

        _localPlayer.SyncView();

        return new PlayerStateMessage(
            playerId,
            displayName,
            NetVector3.From(playerObject.Transform.WorldPosition),
            NetQuaternion.From(playerObject.Transform.WorldRotation));
    }

    public void Dispose()
    {
        foreach (var avatar in _remotePlayers.Values)
        {
            if (!avatar.IsDestroyed)
                avatar.Destroy();
        }

        _remotePlayers.Clear();
        _scene = null;
        _localPlayer = null;
        _cubeVisual = null;
        _inputAsset?.Dispose();
        _planeModel?.Dispose();
        _boxModel?.Dispose();
        _material?.Dispose();
        _inputAsset = null;
        _planeModel = null;
        _boxModel = null;
        _material = null;
    }
}

internal sealed class MultiplayerHostRuntime(string sessionName) : IDisposable
{
    private readonly MultiplayerServerBridge _bridge = new(sessionName);
    private Thread? _thread;
    private Engine? _engine;

    public string SessionId => _bridge.SessionId;
    public string SessionName => _bridge.SessionName;
    public int TcpPort => _bridge.TcpPort;
    public int PlayerCount => _bridge.PlayerCount;

    public void Start()
    {
        _bridge.Start();
        _thread = new Thread(ThreadMain)
        {
            Name = "Vecxy Multiplayer Host",
            IsBackground = true
        };
        _thread.Start();
    }

    private void ThreadMain()
    {
        var options = new Engine.Options
        {
            Headless = true,
            TargetFrameRate = 60,
            Window = new IWindow.Options("Vecxy Headless Host", 1, 1),
            ConfigureServices = builder =>
            {
                builder.RegisterInstance(_bridge)
                    .AsSelf()
                    .SingleInstance();
            }
        };

        var assetsDirectory =
            Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "Assets"));

        var layers = new List<AAppLayer.IDefinition>
        {
            new HeadlessEngineLayer.Definition(
                new AssetsModule.Options
                {
                    AssetsDirectory = assetsDirectory,
                    HotReloadEnabled = false
                }),
            new MultiplayerServerLayer.Definition()
        };

        using var engine = new Engine(options, layers);
        _engine = engine;
        engine.Run();
        _engine = null;
    }

    public void Dispose()
    {
        _engine?.Stop();

        if (_thread is { IsAlive: true })
            _thread.Join(TimeSpan.FromSeconds(2));

        _bridge.Dispose();
    }
}

[UsedImplicitly]
internal sealed class HeadlessEngineLayer(IEnumerable<IModule> modules) : AAppLayer
{
    public sealed class Definition : ADefinition<HeadlessEngineLayer>
    {
        public override IReadOnlyList<Vecxy.Kernel.IDefinition> Children { get; }

        public Definition(AssetsModule.Options? assets = null)
        {
            Children =
            [
                new AssetsModule.Definition(assets),
                new ScenesModule.Definition(),
                new PhysicsModule.Definition()
            ];
        }
    }

    public override void OnInitialize()
    {
        foreach (var module in modules)
            module.OnInitialize();
    }

    public override void OnUpdate(float deltaTime)
    {
        foreach (var module in modules)
        {
            if (module is IModule.IUpdatable updatable)
                updatable.OnUpdate(deltaTime);
        }
    }

    public override void OnUnload()
    {
        foreach (var module in modules.Reverse())
        {
            try
            {
                module.OnShutdown();
            }
            catch
            {
            }
        }
    }
}

[UsedImplicitly]
internal sealed class MultiplayerServerLayer(
    ISceneFactory sceneFactory,
    ISceneManager scenes,
    IPhysicsSystem physics,
    MultiplayerServerBridge bridge) : AAppLayer
{
    public sealed class Definition : ADefinition<MultiplayerServerLayer>;

    private SceneInstance? _scene;
    private SceneObject? _cube;
    private RigidBody? _cubeBody;
    private readonly Dictionary<string, PlayerStateMessage> _players =
        new(StringComparer.Ordinal);
    private float _snapshotTimer;

    public override void OnInitialize()
    {
        _scene = sceneFactory.Create();

        var floor = _scene.CreateObject("ServerFloor", isStatic: true);
        var floorCollider = floor.AddComponent<BoxCollider>();
        floorCollider.Size = new Vector3(20.0f, 0.25f, 20.0f);
        floorCollider.Center = new Vector3(0.0f, -0.125f, 0.0f);
        floorCollider.CollisionLayer = "world";

        _cube = _scene.CreateObject("AuthoritativeCube");
        _cube.Transform.Position = new Vector3(0.0f, 2.0f, 0.0f);
        var cubeCollider = _cube.AddComponent<BoxCollider>();
        cubeCollider.Size = new Vector3(0.75f);
        cubeCollider.CollisionLayer = "box";
        _cubeBody = _cube.AddComponent<RigidBody>();
        _cubeBody.MotionType = EPhysicsMotionType.Dynamic;
        _cubeBody.Mass = 20.0f;
        _cubeBody.EnableSpeculativeContacts = true;
        _cubeBody.AffectedByGravity = true;

        scenes.SetActiveScene(_scene);
    }

    public override void OnUpdate(float deltaTime)
    {
        if (_cube is null || _cubeBody is null)
            return;

        var activeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var player in bridge.GetPlayerStates())
        {
            _players[player.PlayerId] = player;
            activeIds.Add(player.PlayerId);
        }

        foreach (var stalePlayerId in _players.Keys.Except(activeIds).ToArray())
            _players.Remove(stalePlayerId);

        if (_cube.Transform.WorldPosition.Y <= 0.45f &&
            _cubeBody.Velocity.Y <= 0.05f)
        {
            physics.AddImpulse(_cubeBody, new Vector3(0.0f, 7.5f, 0.0f));
        }

        _snapshotTimer += deltaTime;
        if (_snapshotTimer < 1.0f / 20.0f)
            return;

        _snapshotTimer = 0.0f;

        bridge.PublishSnapshot(
            new ServerSnapshot(
                new NetworkBodyState(
                    NetVector3.From(_cube.Transform.WorldPosition),
                    NetQuaternion.From(_cube.Transform.WorldRotation)),
                _players.Values
                    .OrderBy(player => player.PlayerId, StringComparer.Ordinal)
                    .ToArray()));
    }

    public override void OnUnload()
    {
        scenes.UnloadActiveScene();
    }
}

internal sealed class MultiplayerClientConnection(
    string address,
    int tcpPort,
    string localPlayerId,
    string localPlayerName) : IDisposable
{
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly object _sync = new();
    private ServerSnapshot? _latestSnapshot;
    private Exception? _failure;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private DateTime _lastSendUtc = DateTime.MinValue;

    public ServerSnapshot? LatestSnapshot
    {
        get
        {
            lock (_sync)
                return _latestSnapshot;
        }
    }

    public Exception? Failure => _failure;

    public void Connect()
    {
        _client.Connect(address, tcpPort);
        _stream = _client.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        _writer = new StreamWriter(_stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true)
        {
            AutoFlush = true
        };
        _cts = new CancellationTokenSource();
        WriteMessage(
            new Envelope<HelloMessage>(
                "hello",
                new HelloMessage(localPlayerId, localPlayerName)));
        _readTask = Task.Run(ReadLoop);
    }

    public void Pump()
    {
    }

    public void SendLocalPlayerState(PlayerStateMessage state)
    {
        if (_writer is null)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastSendUtc).TotalMilliseconds < 50.0)
            return;

        _lastSendUtc = now;
        WriteMessage(new Envelope<PlayerStateMessage>("player_state", state));
    }

    private async Task ReadLoop()
    {
        try
        {
            while (_reader is not null &&
                   _cts is not null &&
                   !_cts.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(_cts.Token);
                if (string.IsNullOrWhiteSpace(line))
                    break;

                var packet = JsonSerializer.Deserialize<IncomingEnvelope>(line, NetJson.Options);
                if (packet?.Type != "snapshot" || packet.Payload.ValueKind != JsonValueKind.Object)
                    continue;

                var snapshot =
                    packet.Payload.Deserialize<ServerSnapshot>(NetJson.Options);
                if (snapshot is null)
                    continue;

                lock (_sync)
                    _latestSnapshot = snapshot;
            }

            _failure ??= new IOException("Connection closed.");
        }
        catch (Exception exception)
        {
            _failure = exception;
        }
    }

    private void WriteMessage<T>(Envelope<T> envelope)
    {
        _writer?.WriteLine(JsonSerializer.Serialize(envelope, NetJson.Options));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _writer?.Dispose();
        _reader?.Dispose();
        _stream?.Dispose();
        _client.Dispose();
        _cts?.Dispose();
    }
}

internal sealed class DiscoveryBrowser : IDisposable
{
    private readonly ConcurrentDictionary<string, DiscoverySession> _sessions =
        new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    public IReadOnlyList<DiscoverySession> Sessions =>
        _sessions.Values
            .Where(session => DateTime.UtcNow - session.LastSeenUtc < TimeSpan.FromSeconds(3))
            .OrderBy(session => session.Name, StringComparer.Ordinal)
            .ToArray();

    public void Start()
    {
        _loopTask = Task.Run(QueryLoopAsync);
    }

    private async Task QueryLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await QueryOnceAsync(_cts.Token);
            }
            catch
            {
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task QueryOnceAsync(CancellationToken cancellationToken)
    {
        using var client = new UdpClient(0);
        client.EnableBroadcast = true;
        var queryBytes = Encoding.UTF8.GetBytes("vecxy_query");
        await client.SendAsync(
            queryBytes,
            new IPEndPoint(IPAddress.Broadcast, NetPorts.DiscoveryPort),
            cancellationToken);
        await client.SendAsync(
            queryBytes,
            new IPEndPoint(IPAddress.Loopback, NetPorts.DiscoveryPort),
            cancellationToken);

        var timeoutAt = DateTime.UtcNow + TimeSpan.FromMilliseconds(250);
        while (DateTime.UtcNow < timeoutAt && !cancellationToken.IsCancellationRequested)
        {
            var remaining = timeoutAt - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            var receiveTask = client.ReceiveAsync(cancellationToken).AsTask();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(remaining, cancellationToken));
            if (completed != receiveTask)
                break;

            var result = await receiveTask;
            var json = Encoding.UTF8.GetString(result.Buffer);
            var beacon = JsonSerializer.Deserialize<DiscoveryBeacon>(json, NetJson.Options);
            if (beacon is null)
                continue;

            _sessions[beacon.SessionId] = new DiscoverySession(
                beacon.SessionId,
                beacon.Name,
                result.RemoteEndPoint.Address.ToString(),
                beacon.TcpPort,
                beacon.PlayerCount,
                DateTime.UtcNow);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _cts.Dispose();
    }
}

internal sealed class MultiplayerServerBridge(string sessionName) : IDisposable
{
    private readonly ConcurrentDictionary<string, ConnectedClient> _clients =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PlayerStateMessage> _latestPlayers =
        new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();
    private readonly TcpListener _listener =
        new(IPAddress.Any, NetPorts.GetFreeTcpPort());
    private readonly JsonSerializerOptions _json = NetJson.Options;
    private Task? _acceptTask;
    private Task? _discoveryTask;

    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public string SessionName { get; } = sessionName;
    public int TcpPort => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public int PlayerCount => _clients.Count;

    public void Start()
    {
        _listener.Start();
        _acceptTask = Task.Run(AcceptLoopAsync);
        _discoveryTask = Task.Run(DiscoveryLoopAsync);
    }

    public IReadOnlyList<PlayerStateMessage> GetPlayerStates() =>
        _latestPlayers.Values.ToArray();

    public void PublishSnapshot(ServerSnapshot snapshot)
    {
        var packet = JsonSerializer.Serialize(
            new Envelope<ServerSnapshot>("snapshot", snapshot),
            _json);

        foreach (var client in _clients.Values)
            client.TryWrite(packet);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(() => HandleClientAsync(tcpClient));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        await using var stream = tcpClient.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true)
        {
            AutoFlush = true
        };

        ConnectedClient? registered = null;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_cts.Token);
                if (string.IsNullOrWhiteSpace(line))
                    break;

                var packet = JsonSerializer.Deserialize<IncomingEnvelope>(line, _json);
                if (packet is null)
                    continue;

                switch (packet.Type)
                {
                    case "hello":
                    {
                        var hello = packet.Payload.Deserialize<HelloMessage>(_json);
                        if (hello is null)
                            continue;

                        registered = new ConnectedClient(hello.PlayerId, hello.DisplayName, writer);
                        _clients[hello.PlayerId] = registered;
                        break;
                    }
                    case "player_state":
                    {
                        var state = packet.Payload.Deserialize<PlayerStateMessage>(_json);
                        if (state is null)
                            continue;

                        _latestPlayers[state.PlayerId] = state;
                        break;
                    }
                }
            }
        }
        catch
        {
        }
        finally
        {
            if (registered is not null)
            {
                _clients.TryRemove(registered.PlayerId, out _);
                _latestPlayers.TryRemove(registered.PlayerId, out _);
            }

            tcpClient.Dispose();
        }
    }

    private async Task DiscoveryLoopAsync()
    {
        using var udp = new UdpClient(NetPorts.DiscoveryPort);
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(_cts.Token);
                var text = Encoding.UTF8.GetString(result.Buffer);
                if (!string.Equals(text, "vecxy_query", StringComparison.Ordinal))
                    continue;

                var beacon = new DiscoveryBeacon(
                    SessionId,
                    SessionName,
                    TcpPort,
                    Math.Min(2, Math.Max(1, PlayerCount)));
                var bytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(beacon, _json));
                await udp.SendAsync(bytes, result.RemoteEndPoint, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        foreach (var client in _clients.Values)
            client.Dispose();
        _cts.Dispose();
    }

    private sealed class ConnectedClient(
        string playerId,
        string displayName,
        StreamWriter writer) : IDisposable
    {
        private readonly SemaphoreSlim _lock = new(1, 1);

        public string PlayerId { get; } = playerId;
        public string DisplayName { get; } = displayName;

        public void TryWrite(string packet)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _lock.WaitAsync();
                    await writer.WriteLineAsync(packet);
                }
                catch
                {
                }
                finally
                {
                    _lock.Release();
                }
            });
        }

        public void Dispose()
        {
            writer.Dispose();
            _lock.Dispose();
        }
    }
}

internal static class NetPorts
{
    public const int DiscoveryPort = 42070;

    public static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal static class NetJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

internal sealed record DiscoverySession(
    string SessionId,
    string Name,
    string Address,
    int TcpPort,
    int PlayerCount,
    DateTime LastSeenUtc);

internal sealed record DiscoveryBeacon(
    string SessionId,
    string Name,
    int TcpPort,
    int PlayerCount);

internal sealed record HelloMessage(
    string PlayerId,
    string DisplayName);

internal sealed record PlayerStateMessage(
    string PlayerId,
    string DisplayName,
    NetVector3 Position,
    NetQuaternion Rotation);

internal sealed record ServerSnapshot(
    NetworkBodyState Cube,
    IReadOnlyList<PlayerStateMessage> Players);

internal sealed record NetworkBodyState(
    NetVector3 Position,
    NetQuaternion Rotation);

internal sealed record Envelope<T>(
    string Type,
    T Payload);

internal sealed record IncomingEnvelope(
    string Type,
    JsonElement Payload);

internal readonly record struct NetVector3(
    float X,
    float Y,
    float Z)
{
    public static NetVector3 Zero => new(0.0f, 0.0f, 0.0f);

    public static NetVector3 From(Vector3 value) =>
        new(value.X, value.Y, value.Z);

    public Vector3 ToVector3() => new(X, Y, Z);
}

internal readonly record struct NetQuaternion(
    float X,
    float Y,
    float Z,
    float W)
{
    public static NetQuaternion Identity => new(0.0f, 0.0f, 0.0f, 1.0f);

    public static NetQuaternion From(Quaternion value) =>
        new(value.X, value.Y, value.Z, value.W);

    public Quaternion ToQuaternion() => Quaternion.Normalize(new Quaternion(X, Y, Z, W));
}
