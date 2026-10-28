#!/usr/bin/env python3
"""Локальный мост между Unity и OpenAI-совместимым API.

Зачем: Unity-клиент не должен хранить API-ключ. Ключ живёт в переменной
окружения на этой машине, а Unity ходит на http://127.0.0.1:8787/v1.

Запуск (DeepSeek):
    set DEEPSEEK_API_KEY=sk-...
    python server.py

Запуск (Ollama, без ключа):
    python server.py --upstream http://127.0.0.1:11434/v1

Проверка:
    curl http://127.0.0.1:8787/health
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DEFAULT_UPSTREAM = "https://api.deepseek.com/v1"
DEFAULT_PORT = 8787
DEFAULT_MODEL = "deepseek-chat"
CHAT_PATHS = ("/v1/chat/completions", "/chat/completions")


class BridgeHandler(BaseHTTPRequestHandler):
    upstream = DEFAULT_UPSTREAM
    api_key = ""
    model_override = None
    timeout = 180
    server_version = "DSHBridge/1.0"

    # ------------------------------------------------------------------ utils

    def _send_json(self, status: int, payload: dict) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self._cors()
        self.end_headers()
        self.wfile.write(body)

    def _cors(self) -> None:
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")

    def _read_body(self) -> bytes:
        length = int(self.headers.get("Content-Length") or 0)
        return self.rfile.read(length) if length > 0 else b""

    # ------------------------------------------------------------------ HTTP

    def do_OPTIONS(self) -> None:  # noqa: N802
        self.send_response(204)
        self._cors()
        self.end_headers()

    def do_GET(self) -> None:  # noqa: N802
        if self.path.split("?")[0] == "/health":
            self._send_json(200, {
                "status": "ok",
                "upstream": self.upstream,
                "default_model": self.model_override or DEFAULT_MODEL,
                "has_api_key": bool(self.api_key),
            })
            return
        self._send_json(404, {"error": {"message": "Not found: " + self.path}})

    def do_POST(self) -> None:  # noqa: N802
        path = self.path.split("?")[0]
        if path not in CHAT_PATHS:
            self._send_json(404, {"error": {"message": "Not found: " + path}})
            return

        try:
            payload = json.loads(self._read_body() or b"{}")
        except json.JSONDecodeError as exc:
            self._send_json(400, {"error": {"message": "Invalid JSON: %s" % exc}})
            return

        # Мост не поддерживает SSE — всегда отвечаем цельным JSON.
        payload["stream"] = False
        if self.model_override:
            payload["model"] = self.model_override

        self._forward(payload)

    def _forward(self, payload: dict) -> None:
        url = self.upstream.rstrip("/") + "/chat/completions"
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")

        request = urllib.request.Request(url, data=data, method="POST")
        request.add_header("Content-Type", "application/json")
        request.add_header("Accept", "application/json")
        if self.api_key:
            request.add_header("Authorization", "Bearer " + self.api_key)

        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                body = response.read()
                self.send_response(response.status)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self._cors()
                self.end_headers()
                self.wfile.write(body)
        except urllib.error.HTTPError as exc:
            body = exc.read() or b"{}"
            self.send_response(exc.code)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self._cors()
            self.end_headers()
            self.wfile.write(body)
        except urllib.error.URLError as exc:
            self._send_json(502, {"error": {
                "message": "Upstream unavailable (%s): %s" % (url, exc.reason),
            }})
        except Exception as exc:  # noqa: BLE001
            self._send_json(500, {"error": {"message": "%s: %s" % (type(exc).__name__, exc)}})

    def log_message(self, fmt: str, *args) -> None:
        sys.stderr.write("[bridge] " + (fmt % args) + "\n")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="DSH Unity bridge (OpenAI-compatible)")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help="порт (по умолчанию 8787)")
    parser.add_argument("--upstream", default=os.environ.get("DSH_UPSTREAM", DEFAULT_UPSTREAM),
                        help="базовый URL апстрима, например https://api.deepseek.com/v1")
    parser.add_argument("--model", default=os.environ.get("DSH_MODEL"),
                        help="принудительно подменить модель в запросах")
    parser.add_argument("--timeout", type=int, default=180, help="таймаут апстрима, секунды")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)

    api_key = (os.environ.get("DEEPSEEK_API_KEY")
               or os.environ.get("DSH_API_KEY")
               or os.environ.get("OPENAI_API_KEY")
               or "")

    BridgeHandler.upstream = args.upstream.rstrip("/")
    BridgeHandler.api_key = api_key
    BridgeHandler.model_override = args.model
    BridgeHandler.timeout = args.timeout

    host, port = "127.0.0.1", args.port
    server = ThreadingHTTPServer((host, port), BridgeHandler)

    print("DSH bridge: http://%s:%d/v1" % (host, port))
    print("upstream:   %s" % BridgeHandler.upstream)
    print("api key:    %s" % ("да" if api_key else "нет (подойдёт для Ollama/LM Studio)"))
    print("Ctrl+C — остановить")

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nОстановлено")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
