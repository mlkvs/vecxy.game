using Vecxy.Assets;
using Vecxy.Audio;
using DoNotRelease.Gameplay;
namespace DoNotRelease;

public sealed class Soundscape(IAudioManager audio,IAssetsManager assets,GameSettings settings) : IDisposable
{
    private readonly Dictionary<string,(SoundHandle Handle,bool Loop,float Volume,bool Voice)> _active=[];
    private readonly Dictionary<string,SoundHandle> _handles=[];
    private bool _paused;
    private SoundHandle Handle(string path) => _handles.TryGetValue(path,out var h)?h:_handles[path]=new SoundHandle(assets.Find(path));
    public void Play(string name,float volume=.65f,bool loop=false,bool voice=false)
    {
        var path=name.StartsWith("Audio/")?name:"Audio/"+name+".wav";var h=Handle(path);
        if(loop && _active.ContainsKey(path)) return;
        audio.Play(h,loop,volume*settings.Master*(voice?settings.Voice:settings.Effects));_active[path]=(h,loop,volume,voice);
        if(_paused) audio.Pause(h,loop);
    }
    public void Stop(string name)
    {
        var path=name.StartsWith("Audio/")?name:"Audio/"+name+".wav";
        if(_active.Remove(path,out var p)) audio.Stop(p.Handle,p.Loop);
    }
    public void Pause(bool paused)
    {
        if(_paused==paused)return;_paused=paused;
        foreach(var p in _active.Values) if(paused)audio.Pause(p.Handle,p.Loop);else audio.Resume(p.Handle,p.Loop);
    }
    public void RefreshVolumes(){foreach(var item in _active.Where(x=>x.Value.Loop).ToArray()){Stop(item.Key);Play(item.Key,item.Value.Volume,true,item.Value.Voice);}}
    public void StopAll(){foreach(var p in _active.Values)audio.Stop(p.Handle,p.Loop);_active.Clear();_paused=false;}
    public void Ambience(){Play("Room",.65f,true);Play("Breathing",.55f,true);}
    public void Dispose()=>StopAll();
}

public sealed class DialogueController
{
    private readonly DialogueQueue _queue=new();
    private readonly Soundscape _sound;
    private string? _playing;
    public bool Busy=>_queue.Busy;
    public string Subtitle=>_queue.Current?.Text ?? "";
    public string CurrentAudio=>_queue.Current?.Audio ?? "";
    public DialogueController(Soundscape sound)
    {
        _sound=sound;_queue.LineChanged+=line=>{
            if(_playing is not null)_sound.Stop(_playing);
            _playing=line?.Audio;
            if(line is not null){_sound.Play("Speaker",.19f);_sound.Play(line.Audio,.90f,voice:true);}
        };
    }
    public void Say(VoiceLine line)=>_queue.Enqueue(line);
    public void Tick(double dt)=>_queue.Tick(dt);
    public void ReplayCurrent()=>_queue.ReplayCurrent();
    public void Clear()=>_queue.Clear();
}
