using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityAiNpc.Chat
{
    /// <summary>
    /// Calls an OpenAI-compatible <c>POST {baseUrl}/chat/completions</c> endpoint with UnityWebRequest.
    /// Works with api.openai.com, a key-holding proxy, or a local server such as Ollama.
    /// Must be used from the main thread (UnityWebRequest requirement).
    /// </summary>
    public sealed class OpenAIChatClient : IChatClient
    {
        private readonly ChatSettings _settings;
        private readonly RetryPolicy _retry;

        public OpenAIChatClient(ChatSettings settings, RetryPolicy retry = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _retry = retry ?? new RetryPolicy();
        }

        public string Endpoint => _settings.BaseUrl.TrimEnd('/') + "/chat/completions";

        public string Description => $"{_settings.Model} @ {_settings.Host}";

        public async Task<ChatResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            ChatRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            string body = ChatCompletionJson.BuildRequest(messages, options);
            int maxAttempts = Math.Max(1, _retry.MaxAttempts);

            for (int attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await SendOnceAsync(body, cancellationToken);

                if (response.IsSuccess)
                    return ChatCompletionJson.ParseResponse(response.Body);

                bool canRetry = RetryPolicy.IsRetryable(response.StatusCode) && attempt < maxAttempts;
                if (!canRetry)
                    throw new ChatClientException(DescribeFailure(response), response.StatusCode);

                var delay = _retry.GetDelay(attempt + 1, response.RetryAfter);
                Debug.LogWarning($"[AI NPC] Request failed ({DescribeFailure(response)}); retry {attempt}/{maxAttempts - 1} in {delay.TotalSeconds:0.0}s");
                await Task.Delay(delay, cancellationToken);
            }
        }

        private async Task<RawResponse> SendOnceAsync(string body, CancellationToken cancellationToken)
        {
            using var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.Max(1, _settings.TimeoutSeconds),
            };
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(_settings.ApiKey))
                request.SetRequestHeader("Authorization", "Bearer " + _settings.ApiKey);
            if (!string.IsNullOrEmpty(_settings.Organization))
                request.SetRequestHeader("OpenAI-Organization", _settings.Organization);

            var done = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => { request.Abort(); done.TrySetCanceled(); }))
            {
                request.SendWebRequest().completed += _ => done.TrySetResult(true);
                await done.Task;
            }

            return new RawResponse
            {
                StatusCode = request.responseCode,
                Body = request.downloadHandler?.text,
                Error = request.error,
                RetryAfter = request.GetResponseHeader("Retry-After"),
                IsSuccess = request.result == UnityWebRequest.Result.Success,
            };
        }

        private static string DescribeFailure(RawResponse r)
        {
            string detail = ChatCompletionJson.TryParseErrorMessage(r.Body) ?? r.Error ?? "unknown error";
            switch (r.StatusCode)
            {
                case 0: return $"Network error: {detail}";
                case 401: return $"The API key was rejected (401): {detail}";
                case 404: return $"Endpoint or model not found (404): {detail}";
                case 429: return $"Rate limited or out of quota (429): {detail}";
                default: return $"HTTP {r.StatusCode}: {detail}";
            }
        }

        private struct RawResponse
        {
            public long StatusCode;
            public string Body;
            public string Error;
            public string RetryAfter;
            public bool IsSuccess;
        }
    }
}

    public static object UpdateCache(object data = null)
    {
        if (data == null) return null;
        return data;
    }