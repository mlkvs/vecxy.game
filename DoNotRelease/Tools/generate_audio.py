"""Original deterministic sound design and two neural TTS voices. No runtime TTS/network."""
from pathlib import Path
import sys,json,wave,io,math,os,yaml
import numpy as np
from scipy.signal import butter,sosfilt
from piper import PiperVoice,SynthesisConfig
ROOT=Path(__file__).resolve().parents[1];A=Path(os.environ.get('DNR_AUDIO_OUTPUT',str(ROOT/'Assets/Audio')));CACHE=ROOT.parent/'.agents/local/donotrelease/voices';sr=22050;rng=np.random.default_rng(1401)
def write(path,x,rate=sr):
 path.parent.mkdir(parents=True,exist_ok=True)
 with wave.open(str(path),'wb') as w:w.setparams((1 if x.ndim==1 else x.shape[1],2,rate,0,'NONE','not compressed'));w.writeframes((np.clip(x,-.96,.96)*32767).astype('<i2').tobytes())
def noise(n,lo=70,hi=1800):return sosfilt(butter(2,[lo,hi],fs=sr,btype='band',output='sos'),rng.normal(0,1,n))
def tail(x):
 k=min(len(x)//3,int(sr*.02));x[:k]*=np.linspace(0,1,k);x[-k:]*=np.linspace(1,0,k);return x
# Long seamless stereo ventilation, power and water pipes, without music.
T=32;t=np.arange(sr*T)/sr;n=noise(len(t),60,850);n=np.concatenate([n[-sr:],n])[-len(t):]
# Periodic crossfade of noise gives click-free looping endpoints.
fade=sr; n[:fade]=n[:fade]*np.linspace(0,1,fade)+n[-fade:]*np.linspace(1,0,fade);n[-fade:]=n[:fade]
base=.038*n+.009*np.sin(2*np.pi*50*t)+.004*np.sin(2*np.pi*100*t)+.002*np.sin(2*np.pi*150*t)
write(A/'Room.wav',np.stack([base+.005*np.sin(2*np.pi*73*t),base+.005*np.sin(2*np.pi*79*t)],axis=1))
t=np.arange(sr*12)/sr;breath=(np.sin(2*np.pi*t/6)**4)*.016*noise(len(t),300,2100);write(A/'Breathing.wav',tail(breath))
t=np.arange(sr*8)/sr;write(A/'Water.wav',tail(.07*noise(len(t),400,5500)*(1+.2*np.sin(2*np.pi*3*t))))
for i in range(4):
 t=np.arange(int(sr*.36))/sr;x=.12*noise(len(t),80,2600)*np.exp(-t*19)+.045*np.sin(2*np.pi*(95-i*6)*t)*np.exp(-t*24);write(A/f'Step{i}.wav',tail(x))
for name,dur in [('Lock',.8),('Knock',.65),('Hatch',1.7),('Tray',1.0),('Tap',.3),('Cloth',1.2),('Light',.25),('Speaker',.18),('Equipment',3),('Distant',2.6)]:
 t=np.arange(int(sr*dur))/sr
 if name in ['Hatch','Equipment']:x=.055*noise(len(t),90,1900)*np.sin(np.pi*t/dur)**2+.016*np.sin(2*np.pi*117*t)*np.sin(np.pi*t/dur)**2
 elif name=='Cloth':x=.08*noise(len(t),900,6000)*np.sin(np.pi*t/dur)**3
 elif name=='Distant':
  x=np.zeros(len(t))
  for at in [.1,.75,1.4,2.05]:x+=.045*noise(len(t),80,600)*np.exp(-np.maximum(0,t-at)*20)*(t>=at)
 else:
  x=.11*noise(len(t),100,3200)*np.exp(-t*25)
  for f in [147,371,613]:x+=.02*np.sin(2*np.pi*f*t)*np.exp(-t*8)
 write(A/(name+'.wav'),tail(x))
# Restrained final drone: no musical bed during normal play.
t=np.arange(sr*32)/sr;x=sum(.009*np.sin(2*np.pi*f*t+p)*(1+.2*np.sin(2*np.pi*t/16)) for f,p in [(55,0),(82.5,.4),(110,1.1),(164.875,2)])+.006*noise(len(t),180,800);write(A/'Ending.wav',np.stack([x,np.roll(x,400)],axis=1))
lines={'calm': 'Пожалуйста, сохраняйте спокойствие. Вы в безопасности.', 'routine': 'Ваше состояние стабильно. Пожалуйста, соблюдайте установленный распорядок.', 'ack': 'Ваш запрос принят.', 'denied': 'Освобождение в настоящий момент не разрешено.', 'night': 'Дневной режим завершён. Пожалуйста, ложитесь в кровать. Вам рекомендуется отдохнуть.', 'conflict': 'Ваш текущий запрос противоречит вашим первоначальным указаниям.', 'agreement': 'Мы обязаны соблюдать первоначальное соглашение.', 'available': 'Вам предоставлен доступ к личному обращению.', 'autoplay': 'Личное обращение начнётся автоматически. Пожалуйста, обратите внимание на экран.', 'respect': 'Мы уважаем ваше решение.'}
monologue=['Если ты смотришь эту запись, значит, процедура сработала.', 'Скорее всего, ты не помнишь эту комнату.', 'И не помнишь, что сам на всё это согласился.', 'Я знаю, тебе страшно.', 'Наверное, ты уже пытаешься выбраться.', 'Я предупреждал их, что так и будет.', 'Никто не привозил меня сюда против моей воли.', 'Я сам попросил провести процедуру.', 'Сам попросил стереть мои воспоминания.', 'И сам попросил оставить меня здесь.', 'Что бы я ни говорил после этого.', 'Как бы отчаянно ни просил меня выпустить.', 'Я разрешил им отказывать мне.', 'Я знаю, это кажется несправедливым.', 'Ведь жить с моим решением придётся тебе.', 'Но, пожалуйста, пойми.', 'У меня была причина.', 'Надеюсь, тебе никогда не придётся её вспомнить.']
voices={};order=[]
for male,model in [(False,'ru_RU-denis-medium'),(True,'ru_RU-dmitri-medium')]:
 voice=PiperVoice.load(str(CACHE/(model+'.onnx')))
 todo={f'statement{i+1:02}':line for i,line in enumerate(monologue)} if male else lines
 for key,text in todo.items():
  buf=io.BytesIO()
  with wave.open(buf,'wb') as w:voice.synthesize_wav(text,w,SynthesisConfig(length_scale=1.25 if male else 1.07,noise_scale=.5,noise_w_scale=.65))
  buf.seek(0)
  with wave.open(buf,'rb') as w:rate=w.getframerate();x=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float)/32768
  if not male:
   x=sosfilt(butter(2,[260,3700],btype='band',fs=rate,output='sos'),x);x=np.tanh(x*1.4)/1.4
  x=tail(x);out=A/'Voice'/(key+'.wav');write(out,x,rate);voices[key]={'text':text,'audio':'Audio/Voice/'+key+'.wav','duration':round(len(x)/rate+.15,3),'gap':.85 if male else .65}
  if male:order.append(key)
  print(key,voices[key]['duration'],flush=True)
Path(os.environ.get('DNR_VOICE_CONFIG',str(ROOT/'Assets/Configs/Voices.yaml'))).write_text(yaml.safe_dump({'voices':voices,'recording':order},sort_keys=False,allow_unicode=True))
print('RECORDING_SECONDS',sum(voices[k]['duration']+voices[k]['gap'] for k in order))
