using DoNotRelease;
using DoNotRelease.Gameplay;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Numerics;

var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Assets/Configs"));
if(!Directory.Exists(root))root=Path.GetFullPath("DoNotRelease/Assets/Configs");
var yaml=new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
var content=yaml.Deserialize<GameContent>(File.ReadAllText(Path.Combine(root,"Story.yaml")));var voices=yaml.Deserialize<VoiceCatalog>(File.ReadAllText(Path.Combine(root,"Voices.yaml")));content.Voices=voices.Voices;content.Recording=voices.Recording;content.Validate();voices.Validate();
int checks=0;
void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
var f=new GameFlow(content);var visited=new List<GameState>();var cues=new List<string>();f.Changed+=visited.Add;f.Cue+=cues.Add;f.Start();double duration=0;
while(!(f.State==GameState.MAIN_MENU && duration>1) && duration<700){f.Tick(.1);duration+=.1;}
Check(duration is >=300 and <=600,$"Passive duration {duration}");
Check(visited.Where(x=>x!=GameState.MAIN_MENU).SequenceEqual(Enum.GetValues<GameState>().Where(x=>x!=GameState.MAIN_MENU)),"Every stage in order");
Check(cues.Count(x=>x=="calm")==1 && cues.Count(x=>x=="respect")==1,"Automatic first and final voice exactly once");
Check(cues.Count(x=>x=="night")==2,"Two night cycles");
Console.WriteLine($"PASS passive full run: {duration:F1}s, all stages, no interaction");

f.Start();double active=0;
while(!(f.State==GameState.MAIN_MENU&&active>1) && active<700){for(int i=0;i<50;i++){f.Knock();f.TakeFood();f.ReadDocument();f.PlayRecording();f.Sleep();}f.Tick(.1);active+=.1;}
Check(active is >=300 and <=600,$"Active duration {active}");Check(cues.Count(x=>x=="ack")==1,"Spam cannot duplicate ack");Check(cues.Count(x=>x=="denied")==1,"Spam cannot duplicate denial");
Console.WriteLine($"PASS active / 50 interactions per tick: {active:F1}s, no duplicate cues or lock");

foreach(var state in Enum.GetValues<GameState>().Where(x=>x!=GameState.MAIN_MENU)){
 f.Start();int guard=0;while(f.State!=state && guard++<10000)f.Tick(.1);
 Check(f.State==state,"Reach pause state "+state);var age=f.Age;var elapsed=f.Elapsed;var knocks=f.Knocks;f.Paused=true;
 f.Tick(1000);f.Knock();f.TakeFood();f.ReadDocument();f.PlayRecording();f.Sleep();
 Check(f.State==state&&f.Age==age&&f.Elapsed==elapsed&&f.Knocks==knocks,"Pause freezes "+state);
 f.Paused=false;f.Reset();Check(f.State==GameState.MAIN_MENU&&f.Age==0&&f.Elapsed==0&&f.Knocks==0&&!f.FoodTaken&&!f.DocumentRead,"Reset from "+state);
}
Console.WriteLine("PASS pause and restart from every narrative state");

var q=new DialogueQueue();var starts=0;q.LineChanged+=line=>{if(line is not null)starts++;};q.Enqueue(new(){Duration=1,Gap=.5});q.Enqueue(new(){Duration=2});q.Tick(0);Check(starts==1,"First voice starts");q.Tick(.5);Check(q.Current is not null&&starts==1,"No overlap");q.Tick(.5);Check(q.Current is null&&q.Busy,"Gap retains lane");q.Tick(.5);q.Tick(0);Check(starts==2,"Second voice after first and gap");q.Clear();Check(!q.Busy,"Queue reset");
f.Start();while(f.State!=GameState.EXPLORATION)f.Tick(.1);for(int i=0;i<1000;i++)f.Knock();f.Tick(10000,true);Check(f.State==GameState.EXPLORATION,"Narrative waits for active dialogue");f.Tick(0,false);Check(f.State==GameState.FIRST_CONTACT,"Progress when lane frees");
Check(InteractionSystem.RayBox(Vector3.Zero,-Vector3.UnitZ,new(-1,-1,-3),new(1,1,-2),out var hit)&&Math.Abs(hit-2)<.001,"Ray AABB target");Check(!InteractionSystem.RayBox(Vector3.Zero,Vector3.UnitX,new(-1,-1,-3),new(1,1,-2),out _),"Parallel ray rejection");
var settings=new GameSettings{Master=50,Sensitivity=-1,FieldOfView=900,Brightness=float.NaN};settings.Clamp();Check(settings.Master==1&&settings.Sensitivity>0&&settings.FieldOfView==85&&float.IsFinite(settings.Brightness),"Settings clamp");
Check(content.ReleaseSeconds==364L*86400+23*3600+59*60,"Final countdown exactly as specified");
Check(content.Recording.Select(x=>content.Voices[x].Text).Last()=="Надеюсь, тебе никогда не придётся её вспомнить.","Final statement preserved");
Console.WriteLine($"PASS {checks} assertions. Queue, collisions, settings and countdown.");

// Regression: duplicate game/engine paths must not crash the packaged bootstrap.
var packageDir=Path.GetFullPath("DoNotRelease/Build/Linux-x64");
if(File.Exists(Path.Combine(packageDir,"packages.manifest"))){
 var type=typeof(Vecxy.Platforms.PlatformContext).Assembly.GetType("Vecxy.Platforms.DesktopBootstrapAssets")!;
 var method=type.GetMethod("CreateReader",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)!;
 var reader=(Func<string,Stream>)method.Invoke(null,[packageDir])!;
 using var stream=reader("Configs/UI.yaml");using var text=new StreamReader(stream);
 Check(text.ReadToEnd().Contains("1280"),"Packaged game config overrides engine defaults with duplicate paths");
 Console.WriteLine("PASS packaged bootstrap with duplicate game/engine asset paths");
}

f.Start();while(f.State!=GameState.RELEASE_COUNTDOWN)f.Tick(.1);f.Tick(25.1);var beforeEnding=f.ReleaseElapsed;Check(f.State==GameState.ENDING,"Countdown enters ending");f.Tick(1);Check(f.ReleaseElapsed>beforeEnding,"Release countdown stays monotonic across ending transition");
Console.WriteLine($"PASS total: {checks} assertions");

f.Start();while(!f.IsNight)f.Tick(.1);f.Sleep();var sleepDeadline=f.Deadline;f.Tick(1);f.Sleep();Check(f.Sleeping&&f.Deadline==sleepDeadline,"Sleep request is idempotent");f.Reset();Check(!f.Sleeping,"Restart clears lying/sleep state");
Console.WriteLine($"PASS final total: {checks} assertions");

using(var modelAssets=new Vecxy.Assets.AssetsModule(new Vecxy.Assets.AssetsModule.Options{AssetsDirectory=Path.GetDirectoryName(root)!,HotReloadEnabled=false})){
 modelAssets.OnInitialize();
 using var model=modelAssets.Load<Vecxy.Assets.ModelAsset>("Scenes/Compiled/Cell.glb");
 var textured=model.Value.Materials.Where(x=>x.BaseColorTexture is not null).ToArray();
 Check(textured.Length==10,"Ten textured materials imported from actual USD render cache");
 Check(textured.All(x=>x.BaseColorSampler.MinFilter==Vecxy.Assets.ETextureFilter.Nearest && x.BaseColorSampler.MagFilter==Vecxy.Assets.ETextureFilter.Nearest),"Blender Closest survives GLB importer without linear filtering or mipmap filtering");
}
Console.WriteLine($"PASS sampling total: {checks} assertions");
