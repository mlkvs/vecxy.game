"""Editable narrative, collision and interaction data. Run after generate_audio.py."""
from pathlib import Path
import json,math,yaml
def literal(d,s):return d.represent_scalar('tag:yaml.org,2002:str',s,style='|' if '\n' in s else None)
yaml.SafeDumper.add_representer(str,literal)
R=Path(__file__).resolve().parents[1];v=yaml.safe_load((R/'Assets/Configs/Voices.yaml').read_text());recording=sum(v['voices'][k]['duration']+v['voices'][k]['gap'] for k in v['recording'])+14
stages={'INTRO':(10,10),'AWAKENING':(18,18),'EXPLORATION':(10,35),'FIRST_CONTACT':(8,10),'MEAL_DELIVERY':(40,55),'ROUTINE_CYCLE_01':(46,60),'ROUTINE_CYCLE_02':(46,60),'AGREEMENT_HINT':(40,55),'RECORDING_AVAILABLE':(8,25),'RECORDING_PLAYBACK':(recording,recording),'RELEASE_COUNTDOWN':(18,25),'ENDING':(8,8),'CREDITS':(18,18)}
c={'stages':{k:{'min':a,'max':b} for k,(a,b) in stages.items()},'interactionDistance':2,'fadeSeconds':4,'nightAt':30,'releaseSeconds':31535940,
'statusDocument':'СТАТУС РЕЗИДЕНТА\n\nФизическое состояние: стабильно\nВосстановление памяти: не разрешено\nЗапрос на освобождение: отклонён\nОснование: первоначальное соглашение\nСтатус пересмотра: ожидается',
'agreementDocument':'СОГЛАШЕНИЕ О ДОБРОВОЛЬНОЙ ИЗОЛЯЦИИ\n\nСтирание памяти: разрешено\nИзоляция: добровольная\nДосрочное освобождение: запрещено\nОтзыв согласия после процедуры:\nНе принимается\nРаспоряжение: резидент',
'notices':{'door':'Заперто. С этой стороны нет ручки.','bed':'Тонкий матрас. Под ним ничего нет.','chair':'Стул прикручен к полу. [E] Встать.','sink':'Холодная вода.','drink':'Слабый привкус металла.','vent':'За решёткой ровно шумит воздух. Она прочно закреплена.','camera':'Маленький красный огонёк. Объектив неподвижен.','toilet':'Чисто. Здесь недавно убирались.','table':'Ни личных вещей, ни имени.','screen':'Нет сигнала.','meal':'Тёплая еда. За дверью по-прежнему никого не видно.','sleep':'Свет тускнеет. [E] Лечь на кровать.','taken':'Вы ставите поднос на стол.','awake':'В комнате всё по-прежнему.'}}
(R/'Assets/Configs/Story.yaml').write_text(yaml.safe_dump(c,sort_keys=False,allow_unicode=True))
print('passive seconds',sum(x[1] for x in stages.values()),'active seconds',sum(x[0] for x in stages.values()))
