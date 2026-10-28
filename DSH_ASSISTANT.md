# DSH — ИИ-ассистент проекта «бумага»

DSH встроен в Unity-проект как два инструмента:

- **окно редактора** — `Tools > DSH Assistant` (`Ctrl+Alt+A`), видит активную сцену,
  выделенные объекты и версию Unity;
- **бумажная записка в игре** — объект `DSH — бумажный ассистент` в `SampleScene`,
  открывается и закрывается по `F1`.

Ключ API не хранится в Unity. Клиент ходит на локальный мост
`http://127.0.0.1:8787/v1`, а мост — в DeepSeek, OpenAI или Ollama.

## Быстрый старт

1. Откройте проект `игра про бумагу/бумага` в Unity 6000.6.7f1.
2. Задайте ключ DeepSeek **до запуска Unity** (или запускайте мост из отдельного терминала):

   ```powershell
   setx DEEPSEEK_API_KEY "sk-..."
   ```

   После `setx` перезапустите Unity, чтобы редактор унаследовал переменную.
3. В Unity: `Tools > DSH Assistant > Запустить мост`.
   Откроется консоль моста; при успехе в ней будет `DSH bridge: http://127.0.0.1:8787/v1`.
4. Откройте окно ассистента: `Tools > DSH Assistant` (`Ctrl+Alt+A`).
5. Нажмите `Play`. Записка скрыта по умолчанию, чтобы не мешать игре; `F1` открывает и закрывает её.

Без ключа DeepSeek можно использовать локальную Ollama:

```bat
Tools\DSHBridge\run-ollama.bat
```

## Где что лежит

| Путь | Назначение |
| --- | --- |
| `Assets/DSHAssistant/Runtime/DSHChatOverlay.cs` | бумажная записка в игре, F1 |
| `Assets/DSHAssistant/Editor/DSHAssistantWindow.cs` | окно редактора |
| `Assets/DSHAssistant/Runtime/DSHApiClient.cs` | HTTP-клиент Chat Completions |
| `Assets/DSHAssistant/Runtime/DSHSceneContext.cs` | контекст сцены и выделения |
| `Assets/DSHAssistant/Runtime/DSHAssistantSettings.cs` | ScriptableObject с настройками |
| `Assets/DSHAssistant/Editor/DSHProjectInstaller.cs` | установка записки в сцену |
| `Assets/DSHAssistant/DSHAssistantSettings.asset` | настройки проекта |
| `Tools/DSHBridge/` | локальный мост и `.bat`-файлы запуска |
| `DSH_ASSISTANT.md` | этот файл |

## Настройки

Откройте `Assets/DSHAssistant/DSHAssistantSettings.asset` в Inspector:

- `Base Url` — по умолчанию `http://127.0.0.1:8787/v1`;
- `Model` — `deepseek-chat` (можно `deepseek-reasoner`, `gpt-4o-mini`, `llama3.1`);
- `System Prompt` — роль DSH и правила ответа;
- `Include Scene Context` — добавлять к запросу сцену, объекты и выделение;
- `Api Key` — оставьте пустым при работе через мост.

Окно редактора хранит свои адрес, модель и ключ в `EditorPrefs` этой машины.
Переменная окружения `DSH_API_KEY` имеет приоритет над полем `Api Key`.

## Мост

```bat
cd Tools\DSHBridge
set DEEPSEEK_API_KEY=sk-...
run.bat
```

Проверка: <http://127.0.0.1:8787/health>.

Мост можно запустить и из меню Unity: `Tools > DSH Assistant > Запустить мост`.
Он не поддерживает стриминг: ответ приходит целиком. WebGL не поддерживается
из-за `System.Net.Http`.

## Проверка и диагностика

- `Tools > DSH Assistant > Проверить подключение` — запрос к `/health`, результат в Console.
- `Мост недоступен` — не запущен `run.bat` или занят порт 8787.
- `HTTP 401` — не задан или неверен `DEEPSEEK_API_KEY`.
- `Не удалось подключиться` — проверьте `Base Url` в настройках и что мост слушает
  `127.0.0.1:8787`.
- Если записка не появилась в Play mode, проверьте объект
  `DSH — бумажный ассистент` в `SampleScene` и включён ли `DSHChatOverlay`.

## Удаление

1. Удалите объект `DSH — бумажный ассистент` из `SampleScene`.
2. Удалите папки `Assets/DSHAssistant` и `Tools/DSHBridge`.
3. Удалите `DSH_ASSISTANT.md`.

Ключ API в проекте не сохраняется, поэтому удалять из Unity больше нечего.
