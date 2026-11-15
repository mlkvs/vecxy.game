using System.Numerics;
using Vecxy.Assets;
using Vecxy.Audio;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;
using Vecxy.UI;
using Vecxy.Usd;
using Vecxy.Prototypes;
using DoNotRelease.Gameplay;
namespace DoNotRelease;

public sealed class CellScene(IAssetsManager assets,IConfigProvider configs,IInputManager input,IWindow window,IPhysicsSystem physics,IPrototypes prototypes,IUiManager ui,IAudioManager audio) : IScene
{
    private AssetRef<InputAsset>? _controls;
    private InputMap? _map;
    private ConfigRef<GameContent>? _story;
    private ConfigRef<VoiceCatalog>? _voices;
    private RoomController? _room;
    private Soundscape? _sound;
    public void OnLoad(SceneInstance scene)
    {
        _story=configs.LoadConfig<GameContent>("Configs/Story.yaml");_voices=configs.LoadConfig<VoiceCatalog>("Configs/Voices.yaml");var content=_story.Value;content.Voices=_voices.Value.Voices;content.Recording=_voices.Value.Recording;content.Validate();
        var settings=GameSettings.Load();_sound=new Soundscape(audio,assets,settings);
        var location=prototypes.Instantiate<UsdScene>("Prototypes/Cell.prototype",new ScenePrototypeContext{Scene=scene}).Instance;
        _room=new RoomController(scene,location,_sound,content,settings);
        var cam=scene.CreateObject("First person view").AddComponent<Camera>();cam.NearPlane=.045f;cam.FarPlane=25;cam.FieldOfView=settings.FieldOfView;cam.UsePostProcessing=false;cam.ClearColor=new Vector4(.02f,.03f,.025f,1);cam.SceneObject!.AddComponent(new PostProcessing(configs));
        _controls=assets.Load<InputAsset>("Controls.input");_map=input.Create(_controls,"Player");_map.Enable();
        var po=scene.CreateObject("Resident");po.Transform.WorldPosition=_room.Spawn.Transform.WorldPosition;po.Transform.WorldRotation=_room.Spawn.Transform.WorldRotation;var collider=po.AddComponent<CapsuleCollider>();collider.Radius=.25f;collider.Height=1.24f;collider.Center=new Vector3(0,.87f,0);collider.Material.Friction=0;collider.Material.Restitution=0;
        var body=po.AddComponent<RigidBody>();body.MotionType=EPhysicsMotionType.Dynamic;body.Mass=80;body.EnableSpeculativeContacts=true;
        var player=po.AddComponent(new PlayerController(window,input,_map,physics,cam,body,settings,_sound,_room.Spawn));
        scene.CreateObject("Game flow").AddComponent(new GameSession(content,settings,player,_room,_sound,ui,window,_map,physics));
    }
    public void OnUnload(SceneInstance scene){_sound?.Dispose();_map?.Dispose();_controls?.Dispose();_story?.Dispose();_voices?.Dispose();}
}
