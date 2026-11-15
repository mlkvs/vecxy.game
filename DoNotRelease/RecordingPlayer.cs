using DoNotRelease.Gameplay;
using Vecxy.UI;
namespace DoNotRelease;
public sealed class RecordingPlayer(GameContent content,DialogueController dialogue,RoomController room)
{
    public void Start(){foreach(var id in content.Recording)dialogue.Say(content.Voices[id]);}
    private static string Unit(int n,string one,string few,string many)=>n%100 is >=11 and <=14?many:n%10==1?one:n%10 is >=2 and <=4?few:many;
    public void Update(GameFlow flow)
    {
        var title="";var body="";var foot="";
        if(flow.State==GameState.RECORDING_AVAILABLE){title="ЛИЧНОЕ ОБРАЩЕНИЕ";body="ЗАПИСАНО ДО ПРОЦЕДУРЫ\n\nДОСТУП РЕЗИДЕНТУ РАЗРЕШЁН";foot="[E] ВОСПРОИЗВЕСТИ ЗАПИСЬ";}
        else if(flow.State==GameState.RECORDING_PLAYBACK){
            if(dialogue.Busy || flow.Age<2){title="ЛИЧНОЕ ОБРАЩЕНИЕ / ВОСПРОИЗВЕДЕНИЕ";body="АРХИВНАЯ АУДИОЗАПИСЬ\n\n\n"+(((int)(flow.Age*2)%2)==0?"__ | ||| |||||| || |||||| | __":"__ || |||||| | |||| || ||| __");foot=$"ЗАПИСАНО ДО ПРОЦЕДУРЫ     {TimeSpan.FromSeconds(flow.Age):mm\\:ss}";}
            else{title="АРХИВ / СОГЛАШЕНИЕ";body=content.AgreementDocument;foot="ДОСТУП РЕЗИДЕНТУ РАЗРЕШЁН";}
        }
        else if(flow.State>=GameState.RELEASE_COUNTDOWN){var time=TimeSpan.FromSeconds(Math.Max(0,content.ReleaseSeconds-flow.ReleaseElapsed));title="ДО РАЗРЕШЁННОГО ОСВОБОЖДЕНИЯ";body=$"{time.Days:000} {Unit(time.Days,"ДЕНЬ","ДНЯ","ДНЕЙ")}\n\n{time.Hours:00} {Unit(time.Hours,"ЧАС","ЧАСА","ЧАСОВ")}\n\n{time.Minutes:00} {Unit(time.Minutes,"МИНУТА","МИНУТЫ","МИНУТ")}  {time.Seconds:00} {Unit(time.Seconds,"СЕКУНДА","СЕКУНДЫ","СЕКУНД")}";foot="РАСПОРЯЖЕНИЕ: РЕЗИДЕНТ\nДОСРОЧНОЕ ОСВОБОЖДЕНИЕ ЗАПРЕЩЕНО";}
        room.Screen.GetElementById<UiText>("screen-title").Value=title;room.Screen.GetElementById<UiText>("screen-body").Value=body;room.Screen.GetElementById<UiText>("screen-foot").Value=foot;
        room.Screen.GetElementById<UiText>("screen-body").SetStyle("font-size",flow.State==GameState.RECORDING_PLAYBACK&&!dialogue.Busy?"18px":"20px");
        room.Screen.Root.SetStyle("opacity",flow.State>=GameState.RECORDING_AVAILABLE?"1":"0");
        room.Indicator.GetElementById<UiText>("indicator").Value=flow.State>=GameState.RELEASE_COUNTDOWN?"364 Д":"ЗАПЕРТО";
    }
}
