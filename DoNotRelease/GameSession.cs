using System.Globalization;
using System.Numerics;
using DoNotRelease.Gameplay;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Physics;
using Vecxy.Scene;
using Vecxy.UI;
namespace DoNotRelease;

public sealed class GameSession : AComponent
{
    private readonly GameContent _content;
    private readonly GameSettings _settings;
    private readonly PlayerController _player;
    private readonly RoomController _room;
    private readonly Soundscape _sound;
    private readonly IUiManager _ui;
    private readonly IWindow _window;
    private readonly InputMap _input;
    private readonly IPhysicsSystem _physics;
    private readonly GameFlow _flow;
    private readonly DialogueController _dialogue;
    private readonly RecordingPlayer _recording;
    private UiDocument _view=null!;
    private bool _paused,_settingsOpen,_paper,_lastE,_lastEsc,_settingsFromPause;
#if DEBUG
    private bool _finished;
#endif
    private bool _volumesChanged;
    private double _noticeTime,_interactionCooldown,_viewClock;
    private string _notice="";
    private int _waterUses;
    private readonly HashSet<string> _localEvents=[];
#if DEBUG
    private readonly double _testSpeed=double.TryParse(Environment.GetEnvironmentVariable("DNR_TEST_SPEED"),out var speed)?Math.Clamp(speed,1,120):1;
#else
    private readonly double _testSpeed=1;
#endif
    public GameSession(GameContent content,GameSettings settings,PlayerController player,RoomController room,Soundscape sound,IUiManager ui,IWindow window,InputMap input,IPhysicsSystem physics)
    {
        _content=content;_settings=settings;_player=player;_room=room;_sound=sound;_ui=ui;_window=window;_input=input;_physics=physics;
        _flow=new GameFlow(content);_dialogue=new DialogueController(sound);_recording=new RecordingPlayer(content,_dialogue,room);_flow.Changed+=StateChanged;_flow.Cue+=Cue;
    }
    public override void Start()
    {
        _view=_ui.Load("UI/Game.xml");_view.Reloaded+=Bind;Bind(_view);_flow.Reset();
#if DEBUG
        if(Environment.GetEnvironmentVariable("DNR_TEST_AUTOSTART")=="1")StartGame();
#endif
    }
    private UiElement E(string id)=>_view.GetElementById<UiElement>(id);
    private void Text(string id,string text)=>_view.GetElementById<UiText>(id).Value=text;
    private void Button(string id,Action action)=>_view.GetElementById<UiButton>(id).Clicked+=_=>action();
    private void Bind(UiDocument view)
    {
        Button("start",StartGame);Button("exit",()=>_window.Close());Button("main-settings",()=>OpenSettings(false));Button("pause-settings",()=>OpenSettings(true));Button("resume",()=>SetPause(false));Button("restart",StartGame);Button("main",()=>_flow.Reset());Button("settings-back",CloseSettings);
        void Pair(string id,Action<float> change){Button(id+"-minus",()=>{change(-1);if(id is "master" or "effects" or "voice")_volumesChanged=true;_settings.Clamp();UpdateSettings();});Button(id+"-plus",()=>{change(1);if(id is "master" or "effects" or "voice")_volumesChanged=true;_settings.Clamp();UpdateSettings();});}
        Pair("master",x=>_settings.Master+=x*.1f);Pair("effects",x=>_settings.Effects+=x*.1f);Pair("voice",x=>_settings.Voice+=x*.1f);Pair("sensitivity",x=>_settings.Sensitivity+=x*.0003f);Pair("fov",x=>_settings.FieldOfView+=x);Pair("brightness",x=>_settings.Brightness+=x*.1f);
        Button("subtitles-toggle",()=>{_settings.Subtitles=!_settings.Subtitles;UpdateSettings();});Button("bob-toggle",()=>{_settings.HeadBobbing=!_settings.HeadBobbing;UpdateSettings();});Button("fullscreen-toggle",()=>{_window.ToggleFullscreen();UpdateSettings();});UpdateSettings();UpdateView();
    }
    private void StartGame(){
#if DEBUG
_finished=false;
#endif
_paused=_settingsOpen=_paper=false;_flow.Start();SyncCapture();}
    private void StateChanged(GameState state)
    {
        Console.WriteLine($"DNR state={state} elapsed={_flow.Elapsed:F1}");
        _paper=false;
        switch(state){
            case GameState.MAIN_MENU:
                _paused=_settingsOpen=false;_localEvents.Clear();_noticeTime=0;_waterUses=0;_dialogue.Clear();_sound.StopAll();_room.Reset();_player.Reset();_sound.Play("Room",.55f,true);
#if DEBUG
                if(_finished && Environment.GetEnvironmentVariable("DNR_TEST_EXIT")=="1")_window.Close();
#endif
                break;
            case GameState.INTRO:_dialogue.Clear();_sound.StopAll();_room.Reset();_player.Reset();_sound.Ambience();break;
            case GameState.MEAL_DELIVERY:_room.Deliver();Notice("meal");break;
            case GameState.ROUTINE_CYCLE_02:_room.Deliver();_player.Reset();Notice("awake");break;
            case GameState.AGREEMENT_HINT:_room.Deliver(true);_player.Reset();Notice("awake");break;
            case GameState.RECORDING_AVAILABLE:_player.Seated=false;_player.SeatView=null;_sound.Play("Light",.2f);break;
            case GameState.RECORDING_PLAYBACK:_recording.Start();break;
            case GameState.RELEASE_COUNTDOWN:_sound.Play("Ending",.32f,true);_notice="До разрешённого освобождения: 364 дня 23 часа 59 минут.";_noticeTime=12;break;
            case GameState.CREDITS:
#if DEBUG
_finished=true;
#endif
_sound.Stop("Breathing");_sound.Stop("Water");break;
        }
        SyncCapture();if(_view is not null)UpdateView();
    }
    private void Cue(string id){if(_content.Voices.TryGetValue(id,out var line))_dialogue.Say(line);if(id=="night"){_sound.Play("Light",.25f);Notice("sleep");}}
    private void Notice(string id){_notice=_content.Notices.GetValueOrDefault(id,id);_noticeTime=5;}
    private void SetPause(bool value){_paused=value;_flow.Paused=value||_settingsOpen;_sound.Pause(_flow.Paused);SyncCapture();UpdateView();}
    private void OpenSettings(bool fromPause){_settingsFromPause=fromPause;_settingsOpen=true;_flow.Paused=true;_sound.Pause(true);UpdateSettings();SyncCapture();UpdateView();}
    private void CloseSettings(){if(_volumesChanged){_sound.RefreshVolumes();_dialogue.ReplayCurrent();_volumesChanged=false;}_settingsOpen=false;_settings.Save();_paused=_settingsFromPause;_flow.Paused=_paused;_sound.Pause(_paused);SyncCapture();UpdateView();}
    private void SyncCapture()
    {
        var play=_flow.State is not GameState.MAIN_MENU and not GameState.CREDITS && !_paused && !_settingsOpen;
        _window.SetCursorCaptured(play);_player.CanControl=play&&!_paper&&_flow.State!=GameState.INTRO&&_flow.State!=GameState.ENDING;
    }
    private void UpdateSettings()
    {
        Text("master-value",$"{_settings.Master*100:F0}%");Text("effects-value",$"{_settings.Effects*100:F0}%");Text("voice-value",$"{_settings.Voice*100:F0}%");Text("sensitivity-value",$"{_settings.Sensitivity*1000:F1}");Text("fov-value",$"{_settings.FieldOfView:F0}");Text("brightness-value",$"{_settings.Brightness*100:F0}%");
        _view.GetElementById<UiButton>("subtitles-toggle").Label="СУБТИТРЫ: "+(_settings.Subtitles?"ВКЛ.":"ВЫКЛ.");_view.GetElementById<UiButton>("bob-toggle").Label="ПОКАЧИВАНИЕ: "+(_settings.HeadBobbing?"ВКЛ.":"ВЫКЛ.");_view.GetElementById<UiButton>("fullscreen-toggle").Label="ПОЛНЫЙ ЭКРАН: "+(_window.IsFullscreen?"ВКЛ.":"ВЫКЛ.");
    }
    private void UpdateView()
    {
        E("main-menu").IsVisible=_flow.State==GameState.MAIN_MENU&&!_settingsOpen;E("pause-menu").IsVisible=_paused&&!_settingsOpen;E("settings").IsVisible=_settingsOpen;E("document").IsVisible=_paper&&!_paused&&!_settingsOpen;E("credits").IsVisible=_flow.State==GameState.CREDITS&&!_paused&&!_settingsOpen;
        E("hud").IsVisible=_flow.State is not GameState.MAIN_MENU and not GameState.CREDITS && !_paused&&!_settingsOpen&&!_paper;
        E("subtitles").IsVisible=_settings.Subtitles&&_dialogue.Subtitle.Length>0;Text("subtitle",_dialogue.Subtitle);Text("speaker",_flow.State==GameState.RECORDING_PLAYBACK?"ЛИЧНОЕ ОБРАЩЕНИЕ":"НАСТЕННЫЙ ДИНАМИК");Text("notice",_noticeTime>0?_notice:"");
        double fade=0;
        if(_flow.State==GameState.INTRO)fade=1-Math.Clamp((_flow.Age-1)/6,0,1);
        if(_flow.IsRoutine)fade=Math.Clamp((_flow.Age-(_flow.Deadline-_content.FadeSeconds))/_content.FadeSeconds,0,1);
        if(_flow.State is GameState.ROUTINE_CYCLE_02 or GameState.AGREEMENT_HINT)fade=Math.Max(fade,1-Math.Clamp(_flow.Age/_content.FadeSeconds,0,1));
        if(_flow.State==GameState.ENDING)fade=Math.Clamp((_flow.Age-4)/4,0,1);
        E("fade").SetStyle("opacity",fade.ToString("F2",CultureInfo.InvariantCulture));
        _recording.Update(_flow);
    }
    private bool Available(InteractionDefinition def)
    {
        if(def.States.Length>0&&!def.States.Contains(_flow.State.ToString()))return false;
        return def.Id switch {"tray"=>_room.HasMeal,"document"=>_room.HasDocument,_=>true};
    }
    private InteractionDefinition? Target()
    {
        var cam=_player.Camera;var p=cam.Transform.WorldPosition;var d=cam.Transform.Forward;
        float occlusion=_physics.Raycast(p,d,_content.InteractionDistance,_player.SceneObject,out var hit)?hit.Distance:float.MaxValue;
        return InteractionSystem.Find(_room.Interactables,_content.InteractionDistance,p,d,Available,occlusion);
    }
    private void Interact(InteractionDefinition target)
    {
        switch(target.Id){
            case "door":_sound.Play(_flow.Knocks==0?"Lock":"Knock",.65f);_flow.Knock();Notice("door");break;
            case "tray":_room.TakeTray();_flow.TakeFood();Notice("taken");break;
            case "document":_flow.ReadDocument();Text("document-text",_content.StatusDocument);_paper=true;_sound.Play("Cloth",.2f);SyncCapture();break;
            case "screen":
                if(_flow.State==GameState.RECORDING_AVAILABLE)_flow.PlayRecording();
                else if(_flow.State>=GameState.RELEASE_COUNTDOWN || _flow.State==GameState.RECORDING_PLAYBACK&&!_dialogue.Busy){Text("document-text",_content.AgreementDocument);_paper=true;SyncCapture();}else if(_flow.State==GameState.RECORDING_PLAYBACK){_notice="Воспроизводится личное обращение.";_noticeTime=3;}else Notice("screen");break;
            case "bed":if(_flow.IsNight){_flow.Sleep();_sound.Play("Cloth",.4f);}else{_player.Sit(_room.Pose("bed-seat"));Notice("bed");_sound.Play("Cloth",.3f);}break;
            case "chair":_player.Sit(_room.Pose("chair-seat"));Notice("chair");_sound.Play("Cloth",.25f);break;
            case "sink":if(!_room.WaterOn){_room.SetWater(true);_waterUses=0;Notice("sink");}else if(_waterUses++==0)Notice("drink");else _room.SetWater(false);break;
            default:Notice(target.Id);break;
        }
    }
    public override void Update(float dt)
    {
        bool e=_input.GetAction("Interact").IsPressed,esc=_input.GetAction("Pause").IsPressed;bool pressed=e&&!_lastE,escape=esc&&!_lastEsc;_lastE=e;_lastEsc=esc;
        if(escape){if(_settingsOpen)CloseSettings();else if(_paper){_paper=false;SyncCapture();}else if(_flow.State!=GameState.MAIN_MENU)SetPause(!_paused);}
        if(_paused||_settingsOpen)return;
        var step=dt*_testSpeed;_interactionCooldown=Math.Max(0,_interactionCooldown-dt);_noticeTime=Math.Max(0,_noticeTime-dt);
        _dialogue.Tick(step);_flow.Tick(step,_dialogue.Busy);_room.Update((float)step,_flow);
        if(_flow.State==GameState.MAIN_MENU){_player.OverrideView=_room.Pose("menu-view");return;}
        _player.OverrideView=null;
        if(_flow.State==GameState.INTRO){float t=Math.Clamp((float)(_flow.Age-6)/4,0,1);_player.OverrideView=(Vector3.Lerp(_room.Pose("bed-view").Position,_player.Transform.WorldPosition+Vector3.UnitY*1.7f,t),Quaternion.Slerp(_room.Pose("bed-view").Rotation,_player.ViewRotation,t));}
        if(_flow.IsRoutine&&_flow.Sleeping){_player.CanControl=false;_player.OverrideView=_room.Pose("bed-view");}
        var target=_player.CanControl&&!_paper?Target():null;
        if(pressed && _interactionCooldown<=0){_interactionCooldown=.65;
            if(_flow.State==GameState.CREDITS&&_flow.Age>6)_flow.Reset();
            else if(_paper){_paper=false;SyncCapture();}
            else if(_player.Seated){_player.Seated=false;_player.SeatView=null;}
            else if(target is not null)Interact(target);
        }
        var prompt=target is null?"":"[E] "+target.Prompt;
        if(target?.Id=="bed"&&_flow.IsNight)prompt="[E] Лечь";
        if(target?.Id=="sink"&&_room.WaterOn)prompt=_waterUses==0?"[E] Выпить воды":"[E] Закрыть кран";
        if(target?.Id=="screen"&&_flow.State==GameState.RECORDING_AVAILABLE)prompt="[E] Воспроизвести запись";
        if(_player.Seated)prompt="[E / WASD] Встать";
        Text("prompt",prompt);
        if(_flow.Age>18 && _flow.State is GameState.MEAL_DELIVERY or GameState.ROUTINE_CYCLE_02 && _localEvents.Add("hall-"+_flow.State))_sound.Play("Distant",.23f);
        if(_flow.State==GameState.ROUTINE_CYCLE_01&&_flow.Age>15&&_localEvents.Add("equipment"))_sound.Play("Equipment",.2f);
        if((_viewClock+=dt)>.08){_viewClock=0;UpdateView();}
    }
    public override void OnDestroy(){_settings.Save();_dialogue.Clear();_window.SetCursorCaptured(false);_view.Reloaded-=Bind;_ui.Unload(_view);}
}
