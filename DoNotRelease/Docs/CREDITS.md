# Ресурсы и зависимости

Геометрия камеры, текстуры, шумы, эффекты и заключительный эмбиент создаются исходниками в `Tools/`. Онлайн-ресурсы при запуске игры не нужны.

Русская речь сгенерирована Piper 1.8.0. Голос персонала — `ru_RU-denis-medium`, голос личного обращения — `ru_RU-dmitri-medium`. Карточки моделей указывают наборы данных CC0 и сохранены вместе с ресурсами:

- https://huggingface.co/rhasspy/piper-voices/blob/main/ru/ru_RU/denis/medium/MODEL_CARD
- https://huggingface.co/rhasspy/piper-voices/blob/main/ru/ru_RU/dmitri/medium/MODEL_CARD
- https://github.com/OHF-Voice/voice-datasets

В билд входят готовые WAV, а не модели синтеза. Шрифт Noto Sans поставляется с `Assets/Fonts/NotoSans.OFL.txt`.

Игра использует существующий движок Vecxy и его зависимости, включая FMOD, Silk.NET, Yoga и .NET. Их исходные лицензии и условия сохраняются; FMOD предоставлен существующим проектом движка.
