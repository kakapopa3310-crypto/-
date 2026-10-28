using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DSH.Assistant
{
    /// <summary>Ошибка запроса к API (сеть, HTTP-статус, неверный ответ).</summary>
    public class DSHRequestException : Exception
    {
        public DSHRequestException(string message) : base(message) { }
        public DSHRequestException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Минимальный клиент OpenAI-совместимого Chat Completions API.
    /// Работает и в редакторе, и в плеере (кроме WebGL — там нет System.Net.Http).
    /// </summary>
    public static class DSHApiClient
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        /// <summary>Достраивает полный адрес из базового: .../v1 -> .../v1/chat/completions.</summary>
        public static string ResolveEndpoint(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new DSHRequestException("Не задан адрес API (baseUrl).");

            string url = baseUrl.Trim().TrimEnd('/');
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                return url;

            return url + "/chat/completions";
        }

        /// <summary>Отправляет диалог и возвращает текст ответа ассистента.</summary>
        public static async Task<string> SendChatAsync(
            DSHAssistantSettings settings,
            DSHChatMessage[] messages,
            CancellationToken cancellationToken)
        {
            if (settings == null)
                throw new DSHRequestException("Настройки ассистента не назначены.");
            if (messages == null || messages.Length == 0)
                throw new DSHRequestException("Пустой диалог — нечего отправлять.");

            var payload = new DSHChatRequest
            {
                model = string.IsNullOrWhiteSpace(settings.model) ? "deepseek-chat" : settings.model.Trim(),
                messages = messages,
                temperature = settings.temperature,
                max_tokens = settings.maxTokens,
                stream = false
            };

            string json = JsonUtility.ToJson(payload);
            string endpoint = ResolveEndpoint(settings.baseUrl);

            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                request.Headers.TryAddWithoutValidation("Accept", "application/json");

                string key = settings.ResolvedApiKey;
                if (!string.IsNullOrWhiteSpace(key))
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key.Trim());

                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(Mathf.Max(5, settings.timeoutSeconds)));

                    try
                    {
                        using (HttpResponseMessage response = await HttpClient
                                   .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                                   .ConfigureAwait(false))
                        {
                            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode)
                                throw new DSHRequestException(BuildErrorMessage(response.StatusCode, body));
                            return ExtractContent(body);
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new DSHRequestException(
                            $"Превышен таймаут {settings.timeoutSeconds} с. Проверьте, запущен ли мост/API.");
                    }
                    catch (HttpRequestException e)
                    {
                        throw new DSHRequestException(
                            $"Не удалось подключиться к {endpoint}. {e.Message}", e);
                    }
                }
            }
        }

        private static string BuildErrorMessage(System.Net.HttpStatusCode code, string body)
        {
            string detail = null;
            try
            {
                var parsed = JsonUtility.FromJson<DSHChatResponse>(body);
                if (parsed != null && parsed.error != null && !string.IsNullOrEmpty(parsed.error.message))
                    detail = parsed.error.message;
            }
            catch
            {
                // тело не JSON — используем как есть ниже
            }

            if (string.IsNullOrEmpty(detail))
                detail = string.IsNullOrWhiteSpace(body) ? "(пустой ответ)" : body.Trim();
            if (detail.Length > 600)
                detail = detail.Substring(0, 600) + "…";

            return $"HTTP {(int)code}: {detail}";
        }

        private static string ExtractContent(string body)
        {
            DSHChatResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<DSHChatResponse>(body);
            }
            catch (Exception e)
            {
                throw new DSHRequestException("Ответ API не похож на JSON Chat Completions.", e);
            }

            if (parsed == null)
                throw new DSHRequestException("Пустой ответ API.");
            if (parsed.error != null && !string.IsNullOrEmpty(parsed.error.message))
                throw new DSHRequestException("API вернул ошибку: " + parsed.error.message);
            if (parsed.choices == null || parsed.choices.Length == 0 || parsed.choices[0] == null)
                throw new DSHRequestException("В ответе API нет choices[0].");
            if (parsed.choices[0].message == null)
                throw new DSHRequestException("В ответе API нет message.");

            string text = parsed.choices[0].message.content;
            if (string.IsNullOrEmpty(text))
                throw new DSHRequestException("Ассистент вернул пустой текст.");
            return text;
        }
    }
}
