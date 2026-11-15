namespace DoNotRelease.Gameplay;

public enum GameState { MAIN_MENU, INTRO, AWAKENING, EXPLORATION, FIRST_CONTACT, MEAL_DELIVERY, ROUTINE_CYCLE_01, ROUTINE_CYCLE_02, AGREEMENT_HINT, RECORDING_AVAILABLE, RECORDING_PLAYBACK, RELEASE_COUNTDOWN, ENDING, CREDITS }

/// <summary>Deterministic narrative clock. No engine, rendering, audio or input dependencies.</summary>
public sealed class GameFlow(GameContent content)
{
    private readonly HashSet<string> _events=[];
    private double? _sleepDeadline;
    public GameState State {get;private set;}=GameState.MAIN_MENU;
    public double Age {get;private set;}
    public double Elapsed {get;private set;}
    public double? ReleaseStartedAt {get;private set;}
    public double ReleaseElapsed=>ReleaseStartedAt is {} started?Math.Max(0,Elapsed-started):0;
    public bool Paused {get;set;}
    public int Knocks {get;private set;}
    public bool FoodTaken {get;private set;}
    public bool DocumentRead {get;private set;}
    public bool RecordingRequested {get;private set;}
    public bool FinalKnock {get;private set;}
    public event Action<GameState>? Changed;
    public event Action<string>? Cue;
    public bool IsRoutine => State is GameState.ROUTINE_CYCLE_01 or GameState.ROUTINE_CYCLE_02;
    public bool Sleeping => _sleepDeadline is not null;
    public bool IsNight => IsRoutine && Age>=content.NightAt;
    public double Deadline => _sleepDeadline ?? Definition.Max;
    public StageDefinition Definition => content.Stages.GetValueOrDefault(State.ToString()) ?? new(){Min=1,Max=1};
    public void Start() { Reset(); Enter(GameState.INTRO); }
    public void Reset()
    {
        _events.Clear(); _sleepDeadline=null; ReleaseStartedAt=null; Age=Elapsed=0; Paused=false; Knocks=0;
        FoodTaken=DocumentRead=RecordingRequested=FinalKnock=false; State=GameState.MAIN_MENU;
        Changed?.Invoke(State);
    }
    public void Tick(double dt,bool dialogueBusy=false)
    {
        if(!double.IsFinite(dt)||dt<0) throw new ArgumentOutOfRangeException(nameof(dt));
        if(Paused || State==GameState.MAIN_MENU) return;
        Age+=dt;Elapsed+=dt;
        if(IsRoutine && Age>=content.NightAt) Emit($"night-{State}","night");
        if(State==GameState.AGREEMENT_HINT && Age>=9) Emit("conflict","conflict");
        if(State==GameState.AGREEMENT_HINT && Age>=23) Emit("agreement","agreement");
        if(State==GameState.RECORDING_AVAILABLE && Age>=15) Emit("autoplay","autoplay");
        bool early=State switch {
            GameState.EXPLORATION=>Knocks>=2,
            GameState.MEAL_DELIVERY=>FoodTaken,
            GameState.AGREEMENT_HINT=>DocumentRead && _events.Contains("agreement"),
            GameState.RECORDING_AVAILABLE=>RecordingRequested,
            GameState.RELEASE_COUNTDOWN=>FinalKnock,
            _=>false };
        bool ready=Age>=Deadline || early && Age>=Definition.Min;
        if(IsRoutine && _sleepDeadline is {} deadline) ready=Age>=deadline;
        if(ready && !dialogueBusy) Enter(State==GameState.CREDITS ? GameState.MAIN_MENU : State+1);
    }
    private void Enter(GameState next)
    {
        State=next;Age=0;_sleepDeadline=null;
        if(next==GameState.RELEASE_COUNTDOWN)ReleaseStartedAt=Elapsed;
        Changed?.Invoke(next);
        switch(next) {
            case GameState.FIRST_CONTACT:Emit("calm","calm");break;
            case GameState.MEAL_DELIVERY:Emit("routine","routine");break;
            case GameState.RECORDING_AVAILABLE:Emit("available","available");break;
            case GameState.ENDING:Emit("respect","respect");break;
        }
    }
    private void Emit(string unique,string cue) {if(_events.Add(unique)) Cue?.Invoke(cue);}
    public void Knock()
    {
        if(Paused || State is GameState.MAIN_MENU or GameState.INTRO or GameState.CREDITS or GameState.ENDING) return;
        Knocks++;
        if(State is GameState.MEAL_DELIVERY or GameState.ROUTINE_CYCLE_01 or GameState.ROUTINE_CYCLE_02) {
            Emit("ack","ack");if(Knocks>=4) Emit("denied","denied");
        }
        if(State==GameState.AGREEMENT_HINT) Emit("conflict","conflict");
        if(State==GameState.RELEASE_COUNTDOWN) FinalKnock=true;
    }
    public void TakeFood() {if(!Paused && State>=GameState.MEAL_DELIVERY) FoodTaken=true;}
    public void ReadDocument() {if(!Paused && State>=GameState.AGREEMENT_HINT) DocumentRead=true;}
    public void PlayRecording() {if(!Paused && State==GameState.RECORDING_AVAILABLE) RecordingRequested=true;}
    public void Sleep() {if(!Paused && IsNight && _sleepDeadline is null) _sleepDeadline=Math.Min(Definition.Max,Math.Max(Definition.Min,Age+content.FadeSeconds));}
}
