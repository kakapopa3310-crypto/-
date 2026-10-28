using System;

namespace DSH.Assistant
{
    /// <summary>Одно сообщение диалога в формате OpenAI Chat Completions.</summary>
    [Serializable]
    public class DSHChatMessage
    {
        public string role;
        public string content;

        public DSHChatMessage() { }

        public DSHChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [Serializable]
    internal class DSHChatRequest
    {
        public string model;
        public DSHChatMessage[] messages;
        public float temperature;
        public int max_tokens;
        public bool stream;
    }

    [Serializable]
    internal class DSHChoice
    {
        public DSHChatMessage message;
        public string finish_reason;
    }

    [Serializable]
    internal class DSHError
    {
        public string message;
        public string type;
        public string code;
    }

    [Serializable]
    internal class DSHChatResponse
    {
        public DSHChoice[] choices;
        public DSHError error;
    }
}
