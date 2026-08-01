using System.Numerics;
using Vecxy.Assets;
using Vecxy.Diagnostics;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Game.Elevator.InteractiveMap;

public sealed class InteractiveMap : AComponent, IInteractiveMap
{
    private const int RegionColorTolerance = 2;
    private const byte AlphaThreshold = 128;
    private const float ScreenPadding = 0f;
    private const float SecretMarkerHeightRatio = 0.06f;
    private const float MinimumSecretMarkerSize = 26.0f;
    private const float MaximumSecretMarkerSize = 52.0f;
    private const int LeftMouseButton = 0;

    private static readonly MapRegion[] ConfiguredRegions =
    [
        new() { Id = 1, Name = "Apartment", MaskColor = new Color32(255, 0, 0) },
        new() { Id = 2, Name = "BusStation", MaskColor = new Color32(0, 255, 0) },
        new() { Id = 3, Name = "Factory", MaskColor = new Color32(0, 0, 255) },
        new() { Id = 4, Name = "House", MaskColor = new Color32(255, 0, 255) },
        new() { Id = 5, Name = "Lake", MaskColor = new Color32(0, 255, 255) },
        new() { Id = 6, Name = "Bridge", MaskColor = new Color32(255, 255, 0) },
        new() { Id = 7, Name = "Park", MaskColor = new Color32(136, 0, 0) },
        new() { Id = 8, Name = "Warehouse", MaskColor = new Color32(114, 28, 94) },
        new() { Id = 9, Name = "Tower", MaskColor = new Color32(178, 80, 0) }
    ];

    private readonly IAssetsManager _assets;
    private readonly IRenderer _renderer;
    private readonly IInputManager _input;
    private readonly IWindow _window;
    private readonly Dictionary<int, MapRegion> _regionsByExactColor;

    private AssetRef<TextureAsset>? _mapTexture;
    private AssetRef<TextureAsset>? _outlineTexture;
    private AssetRef<TextureAsset>? _maskTexture;
    private AssetRef<TextureAsset>? _secretsTexture;
    private AssetRef<MaterialAsset>? _materialAsset;
    private AssetRef<MaterialAsset>? _markerMaterialAsset;
    private Material? _material;
    private Material? _markerMaterial;
    private Mesh? _quad;
    private GameView? _view;
    private RenderItem? _renderItem;
    private readonly List<RenderItem> _secretMarkerItems = [];
    private IReadOnlyList<MapPoint> _secretPoints = [];
    private MapRegion? _hoveredRegion;
    private Rect _mapRect;
    private MapDebugSample _debugSample;
    private bool _isVisible = true;
    private bool _mouseEventsSubscribed;
    private int _mapVersion;
    private int _outlineVersion;
    private int _maskVersion;
    private int _secretsVersion;

    public MapRegion? HoveredRegion => _hoveredRegion;
    public IReadOnlyList<MapRegion> Regions => ConfiguredRegions;
    public IReadOnlyList<MapPoint> SecretPoints => _secretPoints;
    public Rect MapRect => _mapRect;
    public MapDebugSample DebugSample => _debugSample;
    public bool DebugEnabled { get; set; } = true;
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;

            _isVisible = value;

            if (_view is not null)
                _view.Enabled = value;

            RefreshMouseSubscription();

            if (value)
                RefreshMapRect();
            else
                SetHoveredRegion(null);
        }
    }

    public event Action<MapRegion>? RegionEntered;
    public event Action<MapRegion>? RegionExited;
    public event Action<MapRegion>? RegionClicked;

    public InteractiveMap(
        IAssetsManager assets,
        IRenderer renderer,
        IInputManager input,
        IWindow window)
    {
        _assets = assets;
        _renderer = renderer;
        _input = input;
        _window = window;
        _regionsByExactColor = ConfiguredRegions.ToDictionary(
            region => region.MaskColor.PackedRgb,
            region => region);
    }

    public override void Awake()
    {
        LoadAssets();
        CreateRenderingResources();
        RefreshMapRect();
    }

    public override void OnEnable()
    {
        RefreshMouseSubscription();
    }

    public override void Update(float deltaTime)
    {
        if (!_isVisible)
            return;

        ReloadCpuDataIfNeeded();
        RefreshMapRect();
        UpdateHoveredRegion();
    }

    public override void OnDisable()
    {
        UnsubscribeMouseEvents();
        SetHoveredRegion(null);
    }

    public override void OnDestroy()
    {
        UnsubscribeMouseEvents();

        if (_view is not null)
        {
            _renderer.DestroyGameView(_view);
            _view = null;
        }

        _renderItem = null;
        _secretMarkerItems.Clear();
        _markerMaterial?.Dispose();
        _markerMaterial = null;
        _material?.Dispose();
        _material = null;
        _quad?.Dispose();
        _quad = null;
        _materialAsset?.Dispose();
        _materialAsset = null;
        _markerMaterialAsset?.Dispose();
        _markerMaterialAsset = null;
        _mapTexture?.Dispose();
        _mapTexture = null;
        _outlineTexture?.Dispose();
        _outlineTexture = null;
        _maskTexture?.Dispose();
        _maskTexture = null;
        _secretsTexture?.Dispose();
        _secretsTexture = null;
    }

    public Vector2 MapToScreen(Vector2 normalizedPosition) =>
        new(
            _mapRect.X + normalizedPosition.X * _mapRect.Width,
            _mapRect.Y + normalizedPosition.Y * _mapRect.Height);

    public Vector2 ScreenToMap(Vector2 screenPosition)
    {
        if (_mapRect.Width <= 0.0f || _mapRect.Height <= 0.0f)
            return new Vector2(float.NaN);

        return new Vector2(
            (screenPosition.X - _mapRect.X) / _mapRect.Width,
            (screenPosition.Y - _mapRect.Y) / _mapRect.Height);
    }

    private void LoadAssets()
    {
        _mapTexture = _assets.Load<TextureAsset>("Textures/Map.png");
        _outlineTexture = _assets.Load<TextureAsset>("Textures/Outlines.png");
        _maskTexture = _assets.Load<TextureAsset>("Textures/Masks.png");
        _secretsTexture = _assets.Load<TextureAsset>("Textures/Secrets.png");
        _materialAsset = _assets.Load<MaterialAsset>("Materials/InteractiveMap.material");
        _markerMaterialAsset = _assets.Load<MaterialAsset>("Materials/MapMarker.material");

        RefreshCpuData();
    }

    private void RefreshCpuData()
    {
        var map = Required(_mapTexture, "Map.png");
        var outline = Required(_outlineTexture, "Outlines.png");
        var mask = Required(_maskTexture, "Masks.png");
        var secrets = Required(_secretsTexture, "Secrets.png");

        InteractiveMapAnalyzer.ValidateIdenticalDimensions(
            map,
            outline,
            mask,
            secrets);

        var regionCounts = InteractiveMapAnalyzer.CountRegionPixels(
            mask,
            ConfiguredRegions,
            RegionColorTolerance,
            AlphaThreshold);

        foreach (var region in ConfiguredRegions)
        {
            if (regionCounts[region.Id] == 0)
            {
                Logger.Warning(
                    $"Map mask contains no pixels for region {region.Id} / {region.Name}.");
            }
        }

        _secretPoints = InteractiveMapAnalyzer.FindSecretPoints(
            secrets,
            warning: message => Logger.Warning(message));

        _mapVersion = _mapTexture!.Version;
        _outlineVersion = _outlineTexture!.Version;
        _maskVersion = _maskTexture!.Version;
        _secretsVersion = _secretsTexture!.Version;

        Logger.Info(
            $"Interactive map loaded: {map.Width}x{map.Height}, " +
            $"{ConfiguredRegions.Length} regions, {_secretPoints.Count} secret points.");

        SetMaterialStaticParameters();
        RebuildSecretMarkers();
    }

    private void ReloadCpuDataIfNeeded()
    {
        if (_mapTexture is null ||
            _outlineTexture is null ||
            _maskTexture is null ||
            _secretsTexture is null)
        {
            return;
        }

        if (_mapTexture.Version == _mapVersion &&
            _outlineTexture.Version == _outlineVersion &&
            _maskTexture.Version == _maskVersion &&
            _secretsTexture.Version == _secretsVersion)
        {
            return;
        }

        RefreshCpuData();
    }

    private void CreateRenderingResources()
    {
        _material = new Material(
            _materialAsset ??
            throw new InvalidOperationException("Interactive map material is not loaded."));
        _markerMaterial = new Material(
            _markerMaterialAsset ??
            throw new InvalidOperationException("Map marker material is not loaded."));
        _quad = _renderer.CreateQuad();
        _view = _renderer.CreateGameView();
        _view.Enabled = _isVisible;
        _view.ClearColor = new Vector4(1, 1f, 1f, 1.0f);
        _renderItem = _view.Submit(
            ERenderPhase.Background,
            _quad,
            _material,
            Matrix4x4.Identity);

        SetMaterialStaticParameters();
        SetHoveredRegion(null);
        RebuildSecretMarkers();
    }

    private void SetMaterialStaticParameters()
    {
        if (_material is null || _maskTexture is null)
            return;

        var mask = _maskTexture.Value;
        _material.SetVector(
            "uMaskTexelSize",
            new Vector4(
                1.0f / mask.Width,
                1.0f / mask.Height,
                mask.Width,
                mask.Height));
        _material.SetFloat("uMaskTolerance", RegionColorTolerance / 255.0f);
        _material.SetFloat("uOutlineMaskRadius", 3.0f);
    }

    private void RefreshMapRect()
    {
        if (_mapTexture is null)
            return;

        var framebufferWidth = _renderer.GameOutputWidth;
        var framebufferHeight = _renderer.GameOutputHeight;
        var horizontalPadding = Math.Min(ScreenPadding, framebufferWidth * 0.1f);
        var verticalPadding = Math.Min(ScreenPadding, framebufferHeight * 0.1f);
        var container = new Rect(
            horizontalPadding,
            verticalPadding,
            Math.Max(1.0f, framebufferWidth - horizontalPadding * 2.0f),
            Math.Max(1.0f, framebufferHeight - verticalPadding * 2.0f));

        var map = _mapTexture.Value;
        var scale = MathF.Min(
            container.Width / map.Width,
            container.Height / map.Height);
        var displayedWidth = map.Width * scale;
        var displayedHeight = map.Height * scale;
        _mapRect = new Rect(
            container.X + (container.Width - displayedWidth) * 0.5f,
            container.Y + (container.Height - displayedHeight) * 0.5f,
            displayedWidth,
            displayedHeight);

        var center = _mapRect.Center;
        if (_renderItem is not null)
        {
            _renderItem.Transform = CreateScreenTransform(
                center,
                new Vector2(_mapRect.Width, _mapRect.Height),
                framebufferWidth,
                framebufferHeight);
        }

        var markerSize = Math.Clamp(
            _mapRect.Height * SecretMarkerHeightRatio,
            MinimumSecretMarkerSize,
            MaximumSecretMarkerSize);
        var markerDimensions = new Vector2(markerSize);
        var markerCount = Math.Min(_secretPoints.Count, _secretMarkerItems.Count);

        for (var index = 0; index < markerCount; index++)
        {
            _secretMarkerItems[index].Transform = CreateScreenTransform(
                MapToScreen(_secretPoints[index].NormalizedPosition),
                markerDimensions,
                framebufferWidth,
                framebufferHeight);
        }
    }

    private void RebuildSecretMarkers()
    {
        if (_view is null || _quad is null || _markerMaterial is null)
            return;

        foreach (var item in _secretMarkerItems)
            _view.Remove(item);

        _secretMarkerItems.Clear();

        foreach (var point in _secretPoints)
        {
            _secretMarkerItems.Add(
                _view.Submit(
                    ERenderPhase.Overlay,
                    _quad,
                    _markerMaterial,
                    Matrix4x4.Identity));
        }

        RefreshMapRect();
    }

    private static Matrix4x4 CreateScreenTransform(
        Vector2 center,
        Vector2 size,
        int framebufferWidth,
        int framebufferHeight)
    {
        var ndcCenterX = center.X / framebufferWidth * 2.0f - 1.0f;
        var ndcCenterY = 1.0f - center.Y / framebufferHeight * 2.0f;
        var aspectCorrection = framebufferHeight / (float)framebufferWidth;
        var worldCenterX = ndcCenterX / aspectCorrection;
        var scaleX = 2.0f * size.X / framebufferHeight;
        var scaleY = 2.0f * size.Y / framebufferHeight;

        return
            Matrix4x4.CreateScale(scaleX, scaleY, 1.0f) *
            Matrix4x4.CreateTranslation(worldCenterX, ndcCenterY, 0.0f);
    }

    private void UpdateHoveredRegion()
    {
        var mousePosition = _renderer.ScreenToGameOutput(
            _window.ClientToFramebuffer(_input.MousePosition));
        var uv = ScreenToMap(mousePosition);
        var isInside =
            uv.X >= 0.0f && uv.X <= 1.0f &&
            uv.Y >= 0.0f && uv.Y <= 1.0f;

        if (!isInside || _maskTexture is null)
        {
            _debugSample = new MapDebugSample(
                uv,
                -1,
                -1,
                default,
                null,
                false);
            SetHoveredRegion(null);
            return;
        }

        var mask = _maskTexture.Value;
        var pixelX = Math.Clamp((int)(uv.X * mask.Width), 0, mask.Width - 1);
        var pixelY = Math.Clamp((int)(uv.Y * mask.Height), 0, mask.Height - 1);
        var color = mask.GetPixel(pixelX, pixelY);
        var region = FindRegion(color);

        _debugSample = new MapDebugSample(
            uv,
            pixelX,
            pixelY,
            color,
            region,
            true);
        SetHoveredRegion(region);
    }

    private MapRegion? FindRegion(Color32 color)
    {
        if (color.A < AlphaThreshold)
            return null;

        if (_regionsByExactColor.TryGetValue(color.PackedRgb, out var exact))
            return exact;

        return ConfiguredRegions.FirstOrDefault(region =>
            color.IsNearRgb(region.MaskColor, RegionColorTolerance));
    }

    private void SetHoveredRegion(MapRegion? region)
    {
        if (_hoveredRegion?.Id == region?.Id)
            return;

        var previous = _hoveredRegion;
        _hoveredRegion = region;

        if (_material is not null)
        {
            _material.SetVector(
                "uActiveMaskColor",
                region?.MaskColor.ToVector4() ?? Vector4.Zero);
        }

        if (previous is not null)
            RegionExited?.Invoke(previous);
        if (region is not null)
            RegionEntered?.Invoke(region);
    }

    private void OnMouseButtonChanged(IWindow.MouseButtonEvent eventData)
    {
        if (!eventData.IsPressed || eventData.Button != LeftMouseButton)
            return;

        UpdateHoveredRegion();
        if (_hoveredRegion is not null)
            RegionClicked?.Invoke(_hoveredRegion);
    }

    private void RefreshMouseSubscription()
    {
        var shouldSubscribe = IsActive && _isVisible;
        if (shouldSubscribe == _mouseEventsSubscribed)
            return;

        if (shouldSubscribe)
        {
            _window.MouseButtonChanged += OnMouseButtonChanged;
            _mouseEventsSubscribed = true;
        }
        else
        {
            UnsubscribeMouseEvents();
        }
    }

    private void UnsubscribeMouseEvents()
    {
        if (!_mouseEventsSubscribed)
            return;

        _window.MouseButtonChanged -= OnMouseButtonChanged;
        _mouseEventsSubscribed = false;
    }

    private static TextureAsset Required(
        AssetRef<TextureAsset>? texture,
        string displayName) =>
        texture?.Value ??
        throw new FileNotFoundException(
            $"Required interactive map texture was not loaded: {displayName}");
}
