# DSH Bridge — управление проектом из DeepSeek Harness

Мост между агентом и Unity-редактором: небольшой HTTP-сервер внутри редактора
плюс CLI-обёртка. Позволяет без ручного тыканья запускать компиляцию,
выполнять сценарии на C#, открывать сцены, снимать скриншоты, читать консоль
и собирать билд.

Проект: игра **Paper House** на Unity **6000.6.7f1** (URP 17, Input System).
Редактор: `D:\6000.6.7f1\Editor\Unity.exe`.

## Что внутри

| Путь | Назначение |
|---|---|
| `Assets/Editor/DshBridge/DshCommon.cs` | мини-JSON, кольцевой буфер логов, маршал на главный поток |
| `Assets/Editor/DshBridge/DshCommands.cs` | команды: menu, play, compile, exec, build, screenshot, scene, hierarchy… |
| `Assets/Editor/DshBridge/DshServer.cs` | HTTP-сервер на `127.0.0.1` (TcpListener, только локально) |
| `Assets/Editor/DshBridge/DshWindow.cs` | окно **Tools → DeepSeek Harness → Bridge Window** |
| `Assets/Editor/DshBridge/DshBatch.cs` | входные точки для `-batchmode -executeMethod` |
| `Assets/Editor/DshScripts/Examples.cs` | примеры сценариев агента |
| `Tools/dsh-unity.ps1` | CLI: `ping`, `state`, `console`, `exec`, `scene`, `build`, `batch`, … |

Всё лежит в папках `Editor`, поэтому в собранную игру не попадает.

## Быстрый старт

1. Открыть проект в Unity — мост поднимется сам, в консоли будет `[DSH] Мост запущен`.
   Если редактор ещё не открыт: `Tools\dsh-unity.ps1 open` (запускает с `-force-d3d11`).
2. Проверить:

```powershell
Tools\dsh-unity.ps1 ping
```

3. Дальше по вкусу:

```powershell
Tools\dsh-unity.ps1 state
Tools\dsh-unity.ps1 scene -Arg Assets/Scenes/SampleScene.unity
Tools\dsh-unity.ps1 hierarchy -Arg Assets/Scenes/SampleScene.unity
Tools\dsh-unity.ps1 play
Tools\dsh-unity.ps1 screenshot
```

## HTTP API

| Запрос | Что делает |
|---|---|
| `GET /ping` | жив ли мост, версия Unity, режим, счётчики лога |
| `GET /state` | + активная сцена, выделение, Build Settings |
| `GET /console?count=50&type=error` | последние записи консоли (`all`/`error`/`warning`/`log`) |
| `POST /command` | команда в JSON: `{"cmd":"...","arg":"...","args":"...","method":"..."}` |

Команды: `ping`, `state`, `console`, `menu`, `play`, `stop`, `pause`, `compile`,
`exec`, `build`, `screenshot`, `hierarchy` (с `arg` — сразу откроет сцену),
`scene`, `save`, `clear-log`.

Пример:

```powershell
Invoke-RestMethod http://127.0.0.1:8787/command -Method Post `
  -ContentType 'application/json' `
  -Body '{"cmd":"hierarchy","arg":"Assets/Scenes/SampleScene.unity"}'
```

Ответ всегда в виде `{"ok":true,"data":{…},"elapsedMs":12}`; при ошибке — `{"ok":false,"error":"…"}`.

## Свои сценарии

Файл `Assets/Editor/DshScripts/Мой.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace DshScripts
{
    public static class Мой
    {
        public static string Run(string args)
        {
            return "сделано: " + args;
        }
    }
}
```

Вызов: `Tools\dsh-unity.ps1 exec -Arg DshScripts.Мой.Run -Params "что-то"`.
Требования: `public static`, ноль параметров или один `string`.

## Batchmode

Когда редактор закрыт (или нужно проверить компиляцию «с нуля»):

```powershell
Tools\dsh-unity.ps1 batch -Arg Compile
Tools\dsh-unity.ps1 batch -Arg State
```

Запускается `Unity.exe -batchmode -quit -executeMethod DshBridge.DshBatch.<Метод>`,
лог пишется в `Logs/dsh-batch.log`, результат — в `Library/DshBridge/batch-result.json`.

## Сосуществование с ассистентом игры

В проекте есть отдельный ИИ-ассистент (`Assets/DSHAssistant` + `Tools/DSHBridge`,
Python). Его мост по умолчанию тоже занимает порт **8787**. Наш мост в этом случае
берёт следующий свободный порт и пишет его в `Library/DshBridge/bridge.json`;
CLI читает файл сам, так что команды работают в любом случае.

## Безопасность и мелочи

- Сервер слушает только `127.0.0.1` и живёт лишь пока открыт редактор.
- Из моста можно вызывать только типы из namespace `DshScripts` — случайный код не выполнится.
- Автозапуск моста отключается в окне моста (галочка) или через `EditorPrefs: DshBridge.AutoStart`.
- На этой машине Unity 6 стабильно стартует с `-force-d3d11`: при DX12 редактор
  один раз упал на импорте, а зависшие процессы потом держали `Library`.
  Если проект перестал открываться — снять процессы `Unity*` и `UnityCrashHandler64`,
  затем удалить `Library` (кэш пересоберётся).
- **Не коммитить** `Library/`, `Logs/`, `UserSettings/`, `Builds/`.
