using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CaseZeroApi.Services
{
    #region LLM Client (Azure Foundry GPT-5)
    public sealed class LlmClient
    {
        private readonly HttpClient _http;
        private readonly LlmOptions _opt;

        public LlmClient(HttpClient http, LlmOptions opt)
        {
            _http = http;
            _opt = opt;
        }

        public async Task<string> ChatAsync(IEnumerable<ChatMsg> messages, ChatParams p, CancellationToken ct = default)
        {
            // Monta payload compatível com Azure OpenAI Chat Completions
            var msgs = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray();

            var payload = new Dictionary<string, object?>
            {
                ["messages"] = msgs,
                ["temperature"] = p.Temperature,
                ["max_tokens"] = p.MaxTokens,
                ["top_p"] = p.TopP,
                ["frequency_penalty"] = p.FrequencyPenalty,
                ["presence_penalty"] = p.PresencePenalty,
                ["n"] = 1
            };

            if (p.ResponseFormat == ChatResponseFormat.JsonObject)
            {
                payload["response_format"] = new { type = "json_object" };
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, _opt.BuildChatUrl());
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Add(_opt.ApiKeyHeaderName, _opt.ApiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"LLM error {(int)res.StatusCode}: {body}");
            }

            // Resultado: adapta para respostas no formato Chat Completions
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // Azure/Foundry costuma retornar choices[0].message.content
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return content ?? string.Empty;
        }
    }

    public sealed class LlmOptions
    {
        /// <summary>Ex.: https://{recurso}.openai.azure.com</summary>
        public string Endpoint { get; init; } = string.Empty;
        /// <summary>Deployment do modelo (ex.: gpt-5)</summary>
        public string Deployment { get; init; } = "gpt-5";
        /// <summary>Versão da API do Azure OpenAI/Foundry (ex.: 2024-02-15-preview)</summary>
        public string ApiVersion { get; init; } = "2024-02-15-preview";
        /// <summary>Chave de API</summary>
        public string ApiKey { get; init; } = string.Empty;
        /// <summary>Nome do header da chave: normalmente "api-key"</summary>
        public string ApiKeyHeaderName { get; init; } = "api-key";

        public Uri BuildChatUrl()
        {
            // Rota típica: /openai/deployments/{deployment}/chat/completions?api-version=...
            var baseUri = Endpoint.TrimEnd('/');
            var path = $"{baseUri}/openai/deployments/{Deployment}/chat/completions?api-version={ApiVersion}";
            return new Uri(path, UriKind.Absolute);
        }
    }

    public sealed record ChatMsg(string Role, string Content)
    {
        public static ChatMsg System(string c) => new("system", c);
        public static ChatMsg User(string c) => new("user", c);
        public static ChatMsg Assistant(string c) => new("assistant", c);
    }

    public sealed class ChatParams
    {
        public double Temperature { get; init; } = 0.2;
        public int MaxTokens { get; init; } = 2048;
        public double TopP { get; init; } = 1.0;
        public double FrequencyPenalty { get; init; } = 0.0;
        public double PresencePenalty { get; init; } = 0.0;
        public ChatResponseFormat ResponseFormat { get; init; } = ChatResponseFormat.Text;
    }

    public enum ChatResponseFormat { Text, JsonObject }
    #endregion
}