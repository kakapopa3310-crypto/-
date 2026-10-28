@echo off
setlocal
cd /d "%~dp0"
echo Запуск моста в режиме Ollama (http://127.0.0.1:11434/v1), ключ не нужен.
python "%~dp0server.py" --upstream http://127.0.0.1:11434/v1 --model llama3.1 %*
pause
