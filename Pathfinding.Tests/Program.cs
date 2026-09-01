using MemoryPack;
using Vecxy.Assets;
using Vecxy.Diagnostics;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Networking;
using Vecxy.Pathfinding;
using Vecxy.Platforms;
using Vecxy.UI;

namespace Pathfinding.Tests;

[App]
public sealed class App : IVEntry
{
    public void OnConfigureEngine(PlatformContext context, Engine.Options options)
    {
        options.Headless = Launch.IsServer;
        options.ShowSplashScreen = false;
        options.Window = new IWindow.Options("Vecxy Pathfinding", 500, 500, 1);
    }

    public void OnConfigureLayers(PlatformContext context, List<AAppLayer.IDefinition> layers)
    {
        if (Launch.IsServer)
        {
            layers.Add(new GameLayer.Definition(true));
            return;
        }

        layers.Add(new EngineLayer.Definition(new AssetsModule.Options
        {
            AssetsDirectory = context.AssetsDirectory,
            HotReloadEnabled = !Launch.IsServer
        }));
        layers.Add(new GameLayer.Definition());
    }
}

internal static class Launch
{
    private static readonly string[] Args = Environment.GetCommandLineArgs();
    public static bool IsServer => Has("--server") || IsRole("Server");
    public static bool IsClient => Has("--client") || IsRole("Client");
    public static int Port => Read("--port") is { } value && int.TryParse(value, out var port) ? port : 7777;
    public static string Host => Read("--host") ?? "127.0.0.1";
    private static bool Has(string value) => Args.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
    private static bool IsRole(string role) => string.Equals(Environment.GetEnvironmentVariable("VECXY_ROLE"), role, StringComparison.OrdinalIgnoreCase);
    private static string? Read(string key)
    {
        var index = Array.FindIndex(Args, value => value.Equals(key, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < Args.Length ? Args[index + 1] : null;
    }
}

[MemoryPackable]
public partial struct PlayerState
{
    public ulong Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Color { get; set; }
}

[MemoryPackable]
public partial struct PlayerMovement
{
    public ulong Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class NetworkGame : NetworkBehaviour
{
    private readonly Dictionary<ulong, PlayerState> _players = [];

    [Networked]
    public int PlayerCount { get; private set; }

    public event Action<PlayerState>? Spawned;
    public event Action<PlayerMovement>? Moved;
    public event Action<ulong>? Despawned;

    [ServerRpc(RequireAuthority = false)]
    public void Move(int x, int y)
    {
        if (!_players.TryGetValue(RpcSender.Id, out var player)) return;
        if (x is < 0 or >= GameLayer.Columns || y is < 0 or >= GameLayer.Rows) return;
        player.X = x;
        player.Y = y;
        _players[player.Id] = player;
        BroadcastMove(new PlayerMovement { Id = player.Id, X = x, Y = y });
    }

    [ServerOnly]
    public void AddPlayer(NetworkConnection connection)
    {
        foreach (var player in _players.Values) SpawnFor(connection, player);

        var state = new PlayerState
        {
            Id = connection.Id,
            X = Random.Shared.Next(GameLayer.Columns),
            Y = Random.Shared.Next(GameLayer.Rows),
            Color = Random.Shared.Next(0x303030, 0xF0F0F0)
        };
        _players.Add(state.Id, state);
        PlayerCount = _players.Count;
        BroadcastSpawn(state);
    }

    [ServerOnly]
    public void RemovePlayer(NetworkConnection connection)
    {
        if (!_players.Remove(connection.Id)) return;
        PlayerCount = _players.Count;
        BroadcastDespawn(connection.Id);
    }

    public void AddLocalPlayer(ulong id)
    {
        var state = new PlayerState
        {
            Id = id,
            X = Random.Shared.Next(GameLayer.Columns),
            Y = Random.Shared.Next(GameLayer.Rows),
            Color = Random.Shared.Next(0x303030, 0xF0F0F0)
        };
        _players[id] = state;
        Spawned?.Invoke(state);
    }

    public void MoveLocalPlayer(ulong id, int x, int y)
    {
        if (!_players.TryGetValue(id, out var player)) return;
        player.X = x;
        player.Y = y;
        _players[id] = player;
        Moved?.Invoke(new PlayerMovement { Id = id, X = x, Y = y });
    }

    [ClientRpc]
    private void BroadcastSpawn(PlayerState player) => Spawned?.Invoke(player);

    [TargetRpc]
    private void SpawnFor(NetworkConnection target, PlayerState player) => Spawned?.Invoke(player);

    [ClientRpc]
    private void BroadcastMove(PlayerMovement movement) => Moved?.Invoke(movement);

    [ClientRpc]
    private void BroadcastDespawn(ulong playerId) => Despawned?.Invoke(playerId);
}

[Layer("pathfinding")]
public sealed class GameLayer(
    INetworking networking,
    IEnumerable<IModule> modules,
    IUiManager? ui = null,
    IPathfinding? pathfinding = null) : AAppLayer
{
    public sealed class Definition : ADefinition<GameLayer>
    {
        public override IReadOnlyList<Vecxy.Kernel.IDefinition> Children { get; }
        public Definition(bool server = false) => Children = server ? [new NetworkingModule.Definition()] : [];
    }

    public const int Columns = 32;
    public const int Rows = 20;
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(45);

    private readonly WeightedGrid _map = new(Columns, Rows);
    private readonly Dictionary<ulong, PlayerView> _players = [];
    private NetworkObject? _networkObject;
    private NetworkGame? _game;
    private UiDocument? _document;
    private WorldGrid? _grid;
    private ulong _localPlayerId;

    public override void OnInitialize()
    {
        _networkObject = networking.CreateObject(new NetworkObjectId(1));
        _game = new NetworkGame();
        _networkObject.AddBehaviour(_game);

        if (Launch.IsServer)
        {
            foreach (var module in modules) module.OnInitialize();
            networking.Connected += OnConnected;
            networking.Disconnected += OnDisconnected;
            _ = StartServerAsync();
            return;
        }

        _game.Spawned += Spawn;
        _game.Moved += Move;
        _game.Despawned += Despawn;
        _document = ui!.Load(Assets.UI.World);
        _document.Reloaded += BuildWorld;
        BuildWorld(_document);

        if (Launch.IsClient) _ = ConnectAsync();
        else
        {
            networking.Configure(NetworkRole.Host, new NetworkConnection(0));
            _localPlayerId = 1;
            _game.AddLocalPlayer(_localPlayerId);
        }
    }

    public override void OnUnload()
    {
        networking.Connected -= OnConnected;
        networking.Disconnected -= OnDisconnected;
        foreach (var player in _players.Values) player.Movement?.Cancel();
        if (_game is not null)
        {
            _game.Spawned -= Spawn;
            _game.Moved -= Move;
            _game.Despawned -= Despawn;
        }
        if (_document is not null)
        {
            _document.Reloaded -= BuildWorld;
            ui!.Unload(_document);
        }
        if (Launch.IsServer)
            foreach (var module in modules) module.OnShutdown();
    }

    public override void OnUpdate(float deltaTime)
    {
        if (!Launch.IsServer) return;
        foreach (var module in modules)
            if (module is IModule.IUpdatable updatable) updatable.OnUpdate(deltaTime);
    }

    private async Task StartServerAsync()
    {
        await networking.StartServerAsync(Launch.Port);
        Logger.Info($"[Networking] Server listening on port {Launch.Port}.");
    }

    private async Task ConnectAsync()
    {
        await networking.ConnectAsync(Launch.Host, Launch.Port);
        _localPlayerId = networking.LocalConnection!.Id;
    }

    private void OnConnected(NetworkConnection connection)
    {
        _networkObject!.AddObserver(connection);
        _game!.AddPlayer(connection);
        Logger.Info($"[Networking] Spawned player {connection.Id}.");
    }

    private void OnDisconnected(NetworkConnection connection)
    {
        _game!.RemovePlayer(connection);
        _networkObject!.RemoveObserver(connection);
    }

    private void BuildWorld(UiDocument document)
    {
        var root = document.Query<UiPanel>("#world") ?? throw new InvalidDataException("World UI root was not found.");
        root.Clear();
        _grid = new WorldGrid(document, Columns, Rows);
        _grid.CellClicked += Click;
        root.Add(_grid.Root);

        foreach (var player in _players.Values)
        {
            player.Element = CreatePlayer(document, player.Color);
            Place(player);
        }
    }

    private void Click(GridPoint target)
    {
        if (_game is null || !_players.ContainsKey(_localPlayerId)) return;
        if (Launch.IsClient) _game.Move(target.X, target.Y);
        else _game.MoveLocalPlayer(_localPlayerId, target.X, target.Y);
    }

    private void Spawn(PlayerState state)
    {
        if (_players.ContainsKey(state.Id)) return;
        var player = new PlayerView
        {
            Id = state.Id,
            Color = state.Color,
            Position = new GridPoint(state.X, state.Y),
            Element = CreatePlayer(_document!, state.Color)
        };
        _players.Add(player.Id, player);
        Place(player);
    }

    private void Move(PlayerMovement movement)
    {
        if (!_players.TryGetValue(movement.Id, out var player)) return;
        player.Movement?.Cancel();
        player.Movement?.Dispose();
        player.Movement = new CancellationTokenSource();
        _ = MoveAsync(player, new GridPoint(movement.X, movement.Y), player.Movement.Token);
    }

    private async Task MoveAsync(PlayerView player, GridPoint target, CancellationToken cancellationToken)
    {
        try
        {
            var result = pathfinding!.FindPath(_map, player.Position, target);
            foreach (var position in result.Path.Skip(1))
            {
                await Task.Delay(StepDelay, cancellationToken);
                player.Position = position;
                Place(player);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void Despawn(ulong id)
    {
        if (!_players.Remove(id, out var player)) return;
        player.Movement?.Cancel();
        player.Element?.RemoveFromParent();
    }

    private void Place(PlayerView player)
    {
        if (_grid is null || player.Element is null) return;
        player.Element.DetachFromParent();
        _grid.GetCell(player.Position).Add(player.Element);
    }

    private static UiPanel CreatePlayer(UiDocument document, int color)
    {
        var player = document.CreatePanel(new Dictionary<string, string> { ["class"] = "player" });
        player.Style.Set("background-color", $"#{color & 0xFFFFFF:X6}");
        return player;
    }

    private sealed class PlayerView
    {
        public required ulong Id { get; init; }
        public required int Color { get; init; }
        public required GridPoint Position { get; set; }
        public UiPanel? Element { get; set; }
        public CancellationTokenSource? Movement { get; set; }
    }
}
