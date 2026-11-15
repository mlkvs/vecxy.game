using Vecxy.Assets;
namespace DoNotRelease.Gameplay;

public sealed class GameContent : IYamlConfig
{
    public Dictionary<string, StageDefinition> Stages { get; set; } = [];
    public Dictionary<string, VoiceLine> Voices { get; set; } = [];
    public List<string> Recording { get; set; } = [];
    public Dictionary<string,string> Notices { get; set; } = [];
    public float InteractionDistance { get; set; } = 2;
    public float FadeSeconds { get; set; } = 4;
    public float NightAt { get; set; } = 30;
    public long ReleaseSeconds { get; set; } = 31535940;
    public string StatusDocument { get; set; } = "";
    public string AgreementDocument { get; set; } = "";
    public void Validate()
    {
        foreach(var state in Enum.GetValues<GameState>().Where(x=>x!=GameState.MAIN_MENU))
            if(!Stages.TryGetValue(state.ToString(),out var def) || !double.IsFinite(def.Min) || !double.IsFinite(def.Max) || def.Min<=0 || def.Max<def.Min)
                throw new InvalidDataException($"Invalid stage: {state}");
        foreach(var key in Recording)
            if(!Voices.ContainsKey(key)) throw new InvalidDataException($"Missing recording: {key}");
        if(!float.IsFinite(InteractionDistance) || !float.IsFinite(FadeSeconds) || InteractionDistance<=0 || FadeSeconds<=0 || ReleaseSeconds<=0) throw new InvalidDataException("Invalid game content");
    }
}
public sealed class StageDefinition { public double Min { get; set; } public double Max { get; set; } }
public sealed class VoiceLine { public string Text { get; set; }=""; public string Audio { get; set; }=""; public double Duration { get; set; } public double Gap { get; set; }=.65; }
public sealed class InteractionDefinition { public string Id {get;set;}=""; public string Prompt {get;set;}=""; public float[] Min {get;set;}=[]; public float[] Max {get;set;}=[]; public string[] States {get;set;}=[]; }

public sealed class VoiceCatalog : IYamlConfig { public Dictionary<string,VoiceLine> Voices {get;set;}=[]; public List<string> Recording {get;set;}=[]; public void Validate() {if(Recording.Count!=18 || Voices.Values.Any(v=>!double.IsFinite(v.Duration) || !double.IsFinite(v.Gap) || v.Duration<=0 || v.Gap<0 || string.IsNullOrWhiteSpace(v.Audio))) throw new InvalidDataException("Invalid voice catalog");} }
