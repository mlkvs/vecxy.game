using System.Numerics;
using Vecxy.Prototypes;
using Vecxy.Rendering;
using Vecxy.Scene;
using DoNotRelease.Gameplay;

namespace DoNotRelease;

public enum CellAction { Door, Bed, Chair, Sink, Vent, Camera, Toilet, Table, Tray, Document, Screen }

/// <summary>Placed on the actual object hierarchy in Blender; bounds follow its meshes.</summary>
[Authoring("Взаимодействие", Category="Камера", Key="interaction", Description="Действие по E. Область вычисляется по геометрии объекта и его дочерних объектов.")]
public sealed class CellInteractable : AComponent
{
    public CellAction Action {get;private set;}
    public string Id=>Action.ToString().ToLowerInvariant();
    public string Prompt {get;private set;}="";
    public float Padding {get;private set;}
    public sealed class Prototype : APrototype<CellInteractable,Prototype.Options>
    {
        public sealed class Options
        {
            [Authoring("Действие",Order=0)] public CellAction Action {get;set;}=CellAction.Door;
            [Authoring("Подсказка",Order=1)] public string Prompt {get;set;}="Осмотреть";
            [Authoring("Запас области",Min=.001,Max=.5,Unit="m",Order=2)] public float Padding {get;set;}=.035f;
        }
        protected override CellInteractable Instantiate(IPrototypeContext context)
        {
            var ctx=RequireContext<ScenePrototypeContext>(context);
            return (ctx.Object ?? ctx.CreateObject("Взаимодействие")).AddComponent<CellInteractable>();
        }
        protected override void Configure(CellInteractable target,Options options)
        {
            if(!float.IsFinite(options.Padding)||options.Padding<=0||string.IsNullOrWhiteSpace(options.Prompt))throw new InvalidDataException("Некорректная область взаимодействия");
            target.Action=options.Action;target.Prompt=options.Prompt;target.Padding=options.Padding;
        }
    }
    public InteractionDefinition Bounds()
    {
        var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);
        foreach(var node in SceneObject!.EnumerateHierarchy())
            foreach(var bounds in node.Components.OfType<ILocalBoundsProvider>())
                for(int i=0;i<8;i++){
                    var p=Vector3.Transform(new Vector3((i&1)==0?bounds.LocalBoundsMin.X:bounds.LocalBoundsMax.X,(i&2)==0?bounds.LocalBoundsMin.Y:bounds.LocalBoundsMax.Y,(i&4)==0?bounds.LocalBoundsMin.Z:bounds.LocalBoundsMax.Z),node.Transform.WorldMatrix);
                    min=Vector3.Min(min,p);max=Vector3.Max(max,p);
                }
        if(!float.IsFinite(min.X))throw new InvalidDataException($"У объекта взаимодействия {SceneObject.Name} отсутствует геометрия");
        min-=new Vector3(Padding);max+=new Vector3(Padding);
        return new InteractionDefinition{Id=Id,Prompt=Prompt,Min=[min.X,min.Y,min.Z],Max=[max.X,max.Y,max.Z]};
    }
}

[Authoring("Исходная видимость",Category="Камера",Key="initialState",Description="Видимость при начале и перезапуске игры; сюжет может менять её позже.")]
public sealed class CellInitialState : AComponent
{
    public bool Visible {get;private set;}
    public sealed class Prototype : APrototype<CellInitialState,Prototype.Options>
    {
        public sealed class Options { [Authoring("Видим в начале")] public bool Visible {get;set;} }
        protected override CellInitialState Instantiate(IPrototypeContext context){var c=RequireContext<ScenePrototypeContext>(context);return(c.Object??c.CreateObject()).AddComponent<CellInitialState>();}
        protected override void Configure(CellInitialState target,Options options)=>target.Visible=options.Visible;
    }
}

[Authoring("Параметры помещения",Category="Камера",Key="roomSettings",Description="Режим света и механика помещения. Расстановка, источники света и маркеры находятся в Blender.")]
public sealed class CellRoomSettings : AComponent
{
    public float NightLightMultiplier {get;private set;}=.125f;
    public float AmbientDay {get;private set;}=.58f;
    public float AmbientNight {get;private set;}=.18f;
    public Vector3 AmbientSky {get;private set;}=new(.40f,.45f,.41f);
    public Vector3 AmbientGround {get;private set;}=new(.16f,.18f,.17f);
    public float HatchSeconds {get;private set;}=1.25f;
    public float WaterSeconds {get;private set;}=14;
    public sealed class Prototype : APrototype<CellRoomSettings,Prototype.Options>
    {
        public sealed class Options
        {
            [Authoring("Свет ночью, доля",Min=.02,Max=1,Order=0)] public float NightLightMultiplier {get;set;}=.125f;
            [Authoring("Фоновый свет днём",Min=.01,Max=3,Order=1)] public float AmbientDay {get;set;}=.58f;
            [Authoring("Фоновый свет ночью",Min=.01,Max=3,Order=2)] public float AmbientNight {get;set;}=.18f;
            [Authoring("Фон сверху, RGB",Min=0,Max=1,Order=3)] public Vector3 AmbientSky {get;set;}=new(.40f,.45f,.41f);
            [Authoring("Фон снизу, RGB",Min=0,Max=1,Order=4)] public Vector3 AmbientGround {get;set;}=new(.16f,.18f,.17f);
            [Authoring("Открытие люка",Min=.1,Max=10,Unit="s",Order=5)] public float HatchSeconds {get;set;}=1.25f;
            [Authoring("Автовыключение воды",Min=1,Max=120,Unit="s",Order=6)] public float WaterSeconds {get;set;}=14;
        }
        protected override CellRoomSettings Instantiate(IPrototypeContext context){var c=RequireContext<ScenePrototypeContext>(context);return(c.Object??c.CreateObject()).AddComponent<CellRoomSettings>();}
        protected override void Configure(CellRoomSettings target,Options options)
        {
            if(options.HatchSeconds<=0||options.WaterSeconds<=0||options.NightLightMultiplier<=0)throw new InvalidDataException("Некорректные параметры помещения");
            target.NightLightMultiplier=options.NightLightMultiplier;target.AmbientDay=options.AmbientDay;target.AmbientNight=options.AmbientNight;target.AmbientSky=options.AmbientSky;target.AmbientGround=options.AmbientGround;target.HatchSeconds=options.HatchSeconds;target.WaterSeconds=options.WaterSeconds;
        }
    }
}

[Authoring("Дальность источника",Category="Камера",Key="lightRange",Description="Дальность игрового света; цвет и мощность редактируются у обычного источника Blender.")]
public sealed class CellLightRange : AComponent
{
    public sealed class Prototype : APrototype<CellLightRange,Prototype.Options>
    {
        public sealed class Options { [Authoring("Дальность",Min=.1,Max=100,Unit="m")] public float Range {get;set;}=7; }
        protected override CellLightRange Instantiate(IPrototypeContext context){var c=RequireContext<ScenePrototypeContext>(context);return(c.Object??c.CreateObject()).AddComponent<CellLightRange>();}
        protected override void Configure(CellLightRange target,Options options)
        {
            if(!float.IsFinite(options.Range)||options.Range<=0)throw new InvalidDataException("Некорректная дальность света");
            var lights=target.SceneObject!.EnumerateHierarchy().SelectMany(x=>x.Components.OfType<ALight>()).ToArray();
            if(lights.Length!=1)throw new InvalidDataException("Компонент дальности требует один источник света Blender");
            lights[0].Range=options.Range;
        }
    }
}
