namespace DoNotRelease.Gameplay;

/// <summary>Single voice lane; holds the subtitle for exactly the clip duration, then a silent gap.</summary>
public sealed class DialogueQueue
{
    private readonly Queue<VoiceLine> _queue=[];
    private double _remaining;
    private double _gap;
    public VoiceLine? Current {get;private set;}
    public bool Busy => Current is not null || _queue.Count>0 || _gap>0;
    public event Action<VoiceLine?>? LineChanged;
    public void Enqueue(VoiceLine line) {if(line.Duration<=0) throw new ArgumentOutOfRangeException(nameof(line));_queue.Enqueue(line);}
    public void Tick(double dt)
    {
        if(dt<0) throw new ArgumentOutOfRangeException(nameof(dt));
        if(Current is not null) {
            _remaining-=dt;
            if(_remaining<=0){_gap=Current.Gap;Current=null;LineChanged?.Invoke(null);}
            return;
        }
        if(_gap>0){_gap=Math.Max(0,_gap-dt);return;}
        if(_queue.TryDequeue(out var line)){Current=line;_remaining=line.Duration;LineChanged?.Invoke(line);}
    }
    public void ReplayCurrent(){if(Current is not null){_remaining=Current.Duration;LineChanged?.Invoke(Current);}}
    public void Clear(){_queue.Clear();Current=null;_remaining=_gap=0;LineChanged?.Invoke(null);}
}
