using System.Numerics;
using Vecxy.Assets;
using Vecxy.Rendering;
using Vecxy.Scene;
using Vecxy.UI;
using Vecxy.Usd;
using DoNotRelease.Gameplay;
namespace DoNotRelease;

/// <summary>Only animates authored USD objects. It creates no location geometry, lights or colliders.</summary>
public sealed class RoomController
{
    private readonly UsdSceneInstance _location;
    private readonly SceneObject _hatch,_hatchOpen,_meal,_tableMeal,_paper,_water;
    private readonly Matrix4x4 _hatchClosed,_mealStart;
    private readonly PointLight _ceiling,_screenLight;
    private readonly float _dayLight,_terminalLight;
    private readonly (Material Material,Vector4 Emission)[] _diffusers;
    private readonly CellRoomSettings _parameters;
    private readonly Soundscape _sound;
    private readonly SceneInstance _scene;
    private readonly GameContent _content;
    private readonly GameSettings _settings;
    private double _deliveryAge=100;
    private float _hatchProgress,_waterAge;
    public UiDocument Screen {get;}
    public UiDocument Indicator {get;}
    public bool HasMeal {get;private set;}
    public bool HasDocument {get;private set;}
    public bool WaterOn {get;private set;}
    public IReadOnlyList<CellInteractable> Interactables {get;}
    public SceneObject Spawn=>_location.RequireRole("spawn");
    public (Vector3 Position,Quaternion Rotation) Pose(string role)
    {
        var transform=_location.RequireRole(role).Transform;
        return(transform.WorldPosition,transform.WorldRotation);
    }
    public RoomController(SceneInstance scene,UsdSceneInstance location,Soundscape sound,GameContent content,GameSettings settings)
    {
        _scene=scene;_location=location;_sound=sound;_content=content;_settings=settings;
        T Component<T>(string role) where T:AComponent=>location.RequireRole(role).EnumerateHierarchy().SelectMany(x=>x.Components.OfType<T>()).SingleOrDefault()??throw new InvalidDataException($"USD-роль '{role}' требует компонент {typeof(T).Name}");
        _parameters=Component<CellRoomSettings>("room-settings");
        _hatch=location.RequireRole("hatch");_hatchOpen=location.RequireRole("hatch-open");_meal=location.RequireRole("meal");_tableMeal=location.RequireRole("meal-on-table");_paper=location.RequireRole("document");_water=location.RequireRole("water");
        _hatchClosed=_hatch.Transform.LocalMatrix;_mealStart=_meal.Transform.LocalMatrix;
        _ceiling=Component<PointLight>("main-light");_screenLight=Component<PointLight>("screen-light");_dayLight=_ceiling.Intensity;_terminalLight=_screenLight.Intensity;
        Screen=Component<UiCanvas>("archive-ui").Document??throw new InvalidDataException("Нет UI документа архива в USD");
        Indicator=Component<UiCanvas>("indicator-ui").Document??throw new InvalidDataException("Нет UI документа индикатора в USD");
        _diffusers=location.RequireRole("light-diffuser").EnumerateHierarchy().SelectMany(x=>x.Components.OfType<MeshRenderer>()).Select(x=>(x.Material,(x.Material.Parameters.FirstOrDefault(p=>p.Key=="uEmissiveColor").Value as VectorMaterialParameter)?.Value??Vector4.Zero)).ToArray();
        Interactables=location.Root.EnumerateHierarchy().SelectMany(x=>x.Components.OfType<CellInteractable>()).ToArray();
        if(Interactables.Select(x=>x.Action).Distinct().Count()!=Enum.GetValues<CellAction>().Length)throw new InvalidDataException("В сцене USD отсутствуют обязательные взаимодействия");
        scene.Lighting.Skybox.Enabled=false;scene.Lighting.Exposure=1;scene.Lighting.SpecularStrength=.12f;
        scene.Lighting.AmbientSkyColor=_parameters.AmbientSky;scene.Lighting.AmbientGroundColor=_parameters.AmbientGround;
        Reset();
    }
    private static bool Initial(SceneObject node)=>node.GetComponent<CellInitialState>()?.Visible??throw new InvalidDataException($"Нет исходного состояния у {node.Name}");
    public void Reset()
    {
        HasMeal=Initial(_meal);HasDocument=Initial(_paper);WaterOn=Initial(_water);_deliveryAge=100;_hatchProgress=0;_waterAge=0;
        _hatch.Transform.LocalMatrix=_hatchClosed;_meal.Transform.LocalMatrix=_mealStart;
        _meal.Enabled=HasMeal;_paper.Enabled=HasDocument;_water.Enabled=WaterOn;_screenLight.Intensity=0;
        if(WaterOn)_sound.Play("Water",.4f,true);
    }
    public void Deliver(bool document=false)
    {
        HasMeal=true;HasDocument=document;_deliveryAge=0;_meal.Transform.LocalMatrix=_mealStart;_meal.Enabled=true;_paper.Enabled=document;_sound.Play("Hatch",.55f);_sound.Play("Tray",.33f);
    }
    public void TakeTray(){if(!HasMeal)return;HasMeal=false;_meal.Transform.WorldMatrix=_tableMeal.Transform.WorldMatrix;_sound.Play("Tray",.45f);}
    public void SetWater(bool value){WaterOn=value;_waterAge=0;_water.Enabled=value;_sound.Play("Tap",.35f);if(value)_sound.Play("Water",.4f,true);else _sound.Stop("Water");}
    public void Update(float dt,GameFlow flow)
    {
        _deliveryAge+=dt;var target=_deliveryAge<4?1f:0f;_hatchProgress+=Math.Clamp(target-_hatchProgress,-dt/_parameters.HatchSeconds,dt/_parameters.HatchSeconds);
        // Closed and open anchors share the authored parent, so moving the entire door works.
        _hatch.Transform.LocalMatrix=_hatchClosed;
        _hatch.Transform.WorldPosition=Vector3.Lerp(_hatch.Transform.WorldPosition,_hatchOpen.Transform.WorldPosition,_hatchProgress);
        if(WaterOn && (_waterAge+=dt)>_parameters.WaterSeconds)SetWater(false);
        var night=flow.IsRoutine?Math.Clamp((float)(flow.Age-_content.NightAt)/6,0,1):0;
        var multiplier=1-night*(1-_parameters.NightLightMultiplier);
        foreach(var item in _diffusers)item.Material.SetVector("uEmissiveColor",new Vector4(new Vector3(item.Emission.X,item.Emission.Y,item.Emission.Z)*multiplier,item.Emission.W));
        var desired=_dayLight*multiplier*_settings.Brightness;_ceiling.Intensity+=(desired-_ceiling.Intensity)*Math.Min(1,dt*1.2f);
        _scene.Lighting.AmbientIntensity=(_parameters.AmbientDay*(1-night)+_parameters.AmbientNight*night)*_settings.Brightness;
        _screenLight.Intensity=flow.State>=GameState.RECORDING_AVAILABLE?_terminalLight:0;
    }
}
