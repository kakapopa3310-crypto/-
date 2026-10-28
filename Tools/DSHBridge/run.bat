@echo off
setlocal
cd /d "%~dp0"

if "%DEEPSEEK_API_KEY%"=="" if "%DSH_API_KEY%"=="" (
    echo [!] Переменная DEEPSEEK_API_KEY не задана.
    echo     Для DeepSeek:  set DEEPSEEK_API_KEY=sk-...
    echo     Для Ollama:    запустите  run-ollama.bat
    echo.
)

where python >nul 2>nul
if errorlevel 1 (
    echo [!] Python не найден в PATH.
    pause
    exit /b 1
)

python "%~dp0server.py" %*
pause
