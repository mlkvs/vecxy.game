using System.Numerics;
using HardCore.Cultivation.Game.Application;
using HardCore.Cultivation.Game.Domain;
using HardCore.Cultivation.Game.Infrastructure;
using Vecxy.Assets;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation.Game.Presentation;

public sealed class CombatScenePresenter(
    ISceneManager scenes,
    IAssetsManager assets,
    IRenderer renderer,
    GameDatabase database) : IDisposable
{
    private const float SceneCenterX = 10000f;
    private readonly List<SceneObject> _objects = [];
    private readonly List<SpriteRenderer> _backgroundLayers = [];
    private SceneObject? _cameraObject;
    private AnimatedFighter? _hero;
    private AnimatedFighter? _enemy;
    private string? _monsterId;
    private string? _backgroundId;

    public SceneRenderTarget? RenderTarget { get; private set; }
    public bool IsVisible => _cameraObject?.Enabled == true;

    public void Initialize()
    {
        if (_cameraObject is not null)
            return;
        var scene = scenes.ActiveScene ?? throw new InvalidOperationException("The main scene is not loaded.");
        var config = database.Combat;
        RenderTarget = renderer.CreateRenderTexture(config.RenderWidth, config.RenderHeight);

        _cameraObject = scene.CreateObject("Combat render camera");
        _cameraObject.Transform.Position = new Vector3(SceneCenterX, 0f, 10f);
        var camera = _cameraObject.AddComponent<Camera>();
        camera.Projection = ECameraProjection.Orthographic;
        camera.OrthographicSize = config.RenderHeight * 0.5f;
        camera.NearPlane = 0.1f;
        camera.FarPlane = 100f;
        camera.ClearColor = new Vector4(0.035f, 0.055f, 0.045f, 1f);
        camera.TargetTexture = RenderTarget;
        camera.Priority = 100;
        _objects.Add(_cameraObject);

        var heroObject = CreateFighter(scene, "Combat hero", SceneCenterX - 142f, false, 100);
        var enemyObject = CreateFighter(scene, "Combat enemy", SceneCenterX + 142f, true, 101);
        _hero = new AnimatedFighter(heroObject.GetComponent<SpriteRenderer>()!, assets, config.HeroSpriteSet);
        _enemy = new AnimatedFighter(enemyObject.GetComponent<SpriteRenderer>()!, assets, config.HeroSpriteSet);
        Hide();
    }

    public void Show(ActiveCombat combat)
    {
        Initialize();
        if (_monsterId != combat.MonsterConfigId)
        {
            _monsterId = combat.MonsterConfigId;
            _enemy!.SetSpriteSet(database.GetMonster(_monsterId).SpriteSet);
        }
        if (_backgroundId != combat.BackgroundId)
        {
            _backgroundId = combat.BackgroundId;
            RebuildBackground(database.GetCombatBackground(_backgroundId));
        }
        foreach (var sceneObject in _objects)
            sceneObject.Enabled = true;
        var heroDead = combat.Phase == CombatPhase.Defeat;
        var enemyDead = combat.Phase == CombatPhase.Victory;
        _hero!.Play(heroDead ? "death" : "idle", !heroDead, locked: heroDead, force: true);
        _enemy!.Play(enemyDead ? "death" : "idle", !enemyDead, locked: enemyDead, force: true);
    }

    public void Handle(IReadOnlyList<CombatEvent> events)
    {
        foreach (var combatEvent in events)
        {
            switch (combatEvent.Type)
            {
                case CombatEventType.Started:
                    _hero?.Play("idle", true);
                    _enemy?.Play("idle", true);
                    break;
                case CombatEventType.HeroAttack:
                    _hero?.Play("attack", false);
                    break;
                case CombatEventType.EnemyAttack:
                    _enemy?.Play("attack", false);
                    break;
                case CombatEventType.HeroHurt:
                    _hero?.Play("hurt", false);
                    break;
                case CombatEventType.EnemyHurt:
                    _enemy?.Play("hurt", false);
                    break;
                case CombatEventType.HeroDied:
                    _hero?.Play("death", false, true);
                    break;
                case CombatEventType.EnemyDied:
                    _enemy?.Play("death", false, true);
                    break;
                case CombatEventType.Closed:
                    Hide();
                    break;
            }
        }
    }

    public void Update(float deltaTime)
    {
        if (!IsVisible)
            return;
        _hero?.Update(deltaTime);
        _enemy?.Update(deltaTime);
    }

    public void Hide()
    {
        foreach (var sceneObject in _objects)
            if (!sceneObject.IsDestroyed)
                sceneObject.Enabled = false;
    }

    private SceneObject CreateFighter(SceneInstance scene, string name, float x, bool flipX, int order)
    {
        var sceneObject = scene.CreateObject(name);
        sceneObject.Transform.Position = new Vector3(x, -154f, 0f);
        sceneObject.Transform.Scale = new Vector3(3f, 3f, 1f);
        var sprite = sceneObject.AddComponent<SpriteRenderer>();
        using var texture = assets.Load<TextureAsset>($"{database.Combat.HeroSpriteSet}_idle.png");
        sprite.SetTexture(texture);
        sprite.PixelsPerUnit = 1f;
        sprite.Pivot = new Vector2(0.5f, 0f);
        sprite.SortingLayer = 10;
        sprite.OrderInLayer = order;
        sprite.FlipX = flipX;
        sprite.Sampler = TextureSamplerState.PointClamp;
        sprite.SetFrame(0, 48, 48);
        _objects.Add(sceneObject);
        return sceneObject;
    }

    private void RebuildBackground(CombatBackgroundConfig background)
    {
        var scene = scenes.ActiveScene!;
        foreach (var layer in _backgroundLayers)
        {
            var owner = layer.SceneObject;
            _objects.Remove(owner!);
            owner?.Destroy();
        }
        _backgroundLayers.Clear();
        for (var index = 0; index < background.Layers.Count; index++)
        {
            var sceneObject = scene.CreateObject($"Combat background {index + 1}");
            sceneObject.Transform.Position = new Vector3(SceneCenterX, 0f, 1f);
            using var texture = assets.Load<TextureAsset>(background.Layers[index]);
            var sprite = sceneObject.AddComponent<SpriteRenderer>();
            sprite.SetTexture(texture);
            sprite.PixelsPerUnit = 1f;
            sprite.SortingLayer = 5;
            sprite.OrderInLayer = index;
            sprite.Sampler = TextureSamplerState.PointClamp;
            _backgroundLayers.Add(sprite);
            _objects.Add(sceneObject);
        }
    }

    public void Dispose()
    {
        foreach (var sceneObject in _objects.ToArray())
            if (!sceneObject.IsDestroyed)
                sceneObject.Destroy();
        _objects.Clear();
        _backgroundLayers.Clear();
        RenderTarget?.Dispose();
        RenderTarget = null;
        _cameraObject = null;
    }

    private sealed class AnimatedFighter(SpriteRenderer sprite, IAssetsManager assets, string spriteSet)
    {
        private string _spriteSet = spriteSet;
        private string _animation = string.Empty;
        private int _frameCount;
        private int _frame;
        private float _elapsed;
        private bool _loop;
        private bool _locked;

        public void SetSpriteSet(string value)
        {
            _spriteSet = value;
            _locked = false;
            Play("idle", true, force: true);
        }

        public void Play(string animation, bool loop, bool locked = false, bool force = false)
        {
            if (_locked && !force)
                return;
            if (_animation == animation && _loop == loop && !force)
                return;
            _animation = animation;
            _loop = loop;
            _locked = locked;
            _frameCount = animation == "hurt" ? 2 : 4;
            _frame = 0;
            _elapsed = 0f;
            using var texture = assets.Load<TextureAsset>($"{_spriteSet}_{animation}.png");
            sprite.SetTexture(texture);
            sprite.SetFrame(0, 48, 48);
        }

        public void Update(float deltaTime)
        {
            _elapsed += deltaTime;
            var frameDuration = _animation == "attack" ? 0.09f : 0.14f;
            while (_elapsed >= frameDuration)
            {
                _elapsed -= frameDuration;
                _frame++;
                if (_frame >= _frameCount)
                {
                    if (_loop)
                        _frame = 0;
                    else if (_locked)
                        _frame = _frameCount - 1;
                    else
                    {
                        Play("idle", true, force: true);
                        return;
                    }
                }
                sprite.SetFrame(_frame, 48, 48);
            }
        }
    }
}
