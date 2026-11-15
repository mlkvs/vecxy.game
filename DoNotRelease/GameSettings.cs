using System.Text.Json;
namespace DoNotRelease;
public sealed class GameSettings
{
    public float Master {get;set;}=.8f;
    public float Effects {get;set;}=.8f;
    public float Voice {get;set;}=1;
    public float Sensitivity {get;set;}=.0024f;
    public float FieldOfView {get;set;}=80;
    public float Brightness {get;set;}=1;
    public bool Subtitles {get;set;}=true;
    public bool HeadBobbing {get;set;}
    private static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DoNotRelease","settings.json");
    public static GameSettings Load()
    {
        try {var s=JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath)) ?? new();s.Clamp();return s;}
        catch(IOException){return new();} catch(JsonException){return new();} catch(UnauthorizedAccessException){return new();}
    }
    public void Clamp(){Master=Math.Clamp(float.IsFinite(Master)?Master:.8f,0,1);Effects=Math.Clamp(float.IsFinite(Effects)?Effects:.8f,0,1);Voice=Math.Clamp(float.IsFinite(Voice)?Voice:1,0,1);Sensitivity=Math.Clamp(float.IsFinite(Sensitivity)?Sensitivity:.0024f,.0006f,.006f);FieldOfView=Math.Clamp(float.IsFinite(FieldOfView)?FieldOfView:80,75,85);Brightness=Math.Clamp(float.IsFinite(Brightness)?Brightness:1,.65f,1.5f);}
    public void Save(){try{Clamp();Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.WriteAllText(FilePath,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));}catch(IOException){}catch(UnauthorizedAccessException){}}
}
