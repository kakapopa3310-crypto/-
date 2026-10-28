using System;
using UnityEngine;

namespace DSH.Assistant
{
    /// <summary>
    /// Настройки подключения и поведения ассистента.
    /// Создаётся через Assets > Create > DSH > Assistant Settings
    /// или через меню Tools > DSH Assistant > Создать настройки.
    /// </summary>
    [CreateAssetMenu(fileName = "DSHAssistantSettings", menuName = "DSH/Assistant Settings", order = 0)]
    public class DSHAssistantSettings : ScriptableObject
    {
        [Header("Подключение")]
        [Tooltip("OpenAI-совместимый адрес API, без /chat/completions.\n" +
                 "По умолчанию — локальный мост из tools/bridge/server.py.")]
        public string baseUrl = "http://127.0.0.1:8787/v1";

        [Tooltip("Ключ API. При работе через локальный мост можно оставить пустым: " +
                 "ключ хранится в переменной окружения на машине, где запущен мост. " +
                 "Переменная окружения DSH_API_KEY имеет приоритет над этим полем.")]
        public string apiKey = "";

        [Tooltip("Имя модели, например deepseek-chat, gpt-4o-mini, llama3.1.")]
        public string model = "deepseek-chat";

        [Header("Поведение")]
        [TextArea(4, 12)]
        [Tooltip("Системная инструкция — задаёт роль и стиль ответов.")]
        public string systemPrompt =
            "Ты — DSH, встроенный ИИ-ассистент Unity-проекта «бумага». Отвечай по-русски, кратко и по делу. " +
            "Если просят код — давай готовый C# для Unity 6 (6000.6), без псевдокода. " +
            "Учитывай контекст сцены, если он передан. Не выдумывай имена объектов, которых нет в контексте. " +
            "Помогай с геймплеем, механиками, шейдерами и архитектурой игры про бумагу.";

        [Range(0f, 2f)]
        public float temperature = 0.7f;

        [Tooltip("Ограничение длины ответа в токенах.")]
        public int maxTokens = 2048;

        [Tooltip("Таймаут одного запроса, секунды.")]
        public int timeoutSeconds = 120;

        [Header("Контекст")]
        [Tooltip("Добавлять к системному сообщению описание сцены, выделенных объектов и версии Unity.")]
        public bool includeSceneContext = true;

        /// <summary>Ключ из переменной окружения DSH_API_KEY, иначе — из поля apiKey.</summary>
        public string ResolvedApiKey
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("DSH_API_KEY");
                return string.IsNullOrWhiteSpace(env) ? apiKey : env;
            }
        }
    }
}
