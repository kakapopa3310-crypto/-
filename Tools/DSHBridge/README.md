# DSH Bridge

Локальный мост между Unity и OpenAI-совместимым API. Unity ходит на
`http://127.0.0.1:8787/v1`, ключ остаётся в переменной окружения на этой машине.

## Быстрый запуск (DeepSeek)

В PowerShell:

```powershell
$env:DEEPSEEK_API_KEY = "sk-..."
.\run.bat
```

Или в командной строке:

```bat
set DEEPSEEK_API_KEY=sk-...
run.bat
```

Проверка: <http://127.0.0.1:8787/health>.

## Без ключа (Ollama)

```bat
run-ollama.bat
```

По умолчанию мост ждёт Ollama на `http://127.0.0.1:11434/v1` и модель `llama3.1`.
Можно указать другую модель:

```bat
run-ollama.bat --model qwen2.5-coder:7b
```

## Другие OpenAI-совместимые API

```bat
set OPENAI_API_KEY=sk-...
python server.py --upstream https://api.openai.com/v1
```

Мост понимает ключ из `DEEPSEEK_API_KEY`, `DSH_API_KEY` или `OPENAI_API_KEY`.
Адрес и модель можно поменять в окне `Tools > DSH Assistant` в Unity.

## Эндпоинты

- `GET /health` — статус моста.
- `POST /v1/chat/completions` — Chat Completions, без стриминга.

## Остановка

`Ctrl+C` в окне моста. Порт по умолчанию — `8787`; изменить:
`python server.py --port 8788`.
