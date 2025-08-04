using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace UnityAiNpc.Chat
{
    /// <summary>
    /// Builds Chat Completions request bodies and reads responses.
    /// Requests are written by hand because JsonUtility can't leave out optional
    /// fields (it would always emit <c>response_format</c>); responses are read
    /// with JsonUtility into the small DTOs below.
    /// </summary>
    public static class ChatCompletionJson
    {
        public static string BuildRequest(IReadOnlyList<ChatMessage> messages, ChatRequestOptions options)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("At least one message is required.", nameof(messages));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder(256);
            sb.Append("{\"model\":").Append(Quote(options.Model));
            sb.Append(",\"temperature\":").Append(options.Temperature.ToString("0.###", CultureInfo.InvariantCulture));
            if (options.MaxTokens > 0)
                sb.Append(",\"max_tokens\":").Append(options.MaxTokens.ToString(CultureInfo.InvariantCulture));
            if (options.JsonMode)
                sb.Append(",\"response_format\":{\"type\":\"json_object\"}");

            sb.Append(",\"messages\":[");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"role\":").Append(Quote(messages[i].role))
                  .Append(",\"content\":").Append(Quote(messages[i].content ?? string.Empty))
                  .Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>JSON string literal, including the surrounding quotes.</summary>
        public static string Quote(string value)
        {
            if (value == null) return "null";
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c == '\u2028' || c == '\u2029')
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <exception cref="ChatClientException">The body has no usable choice.</exception>
        public static ChatResult ParseResponse(string json)
        {
            CompletionDto dto;
            try
            {
                dto = JsonUtility.FromJson<CompletionDto>(json ?? string.Empty);
            }
            catch (ArgumentException e)
            {
                throw new ChatClientException("The server returned malformed JSON.", 0, e);
            }

            if (dto == null || dto.choices == null || dto.choices.Length == 0 || dto.choices[0].message == null)
                throw new ChatClientException("The server response contained no choices.");

            var choice = dto.choices[0];
            return new ChatResult
            {
                Content = choice.message.content ?? string.Empty,
                FinishReason = choice.finish_reason,
                Model = dto.model,
                PromptTokens = dto.usage?.prompt_tokens ?? 0,
                CompletionTokens = dto.usage?.completion_tokens ?? 0,
            };
        }

        /// <summary>
        /// Pulls <c>error.message</c> out of an OpenAI-style error body,
        /// or returns null if the body isn't one.
        /// </summary>
        public static string TryParseErrorMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var dto = JsonUtility.FromJson<ErrorEnvelopeDto>(json);
                var message = dto?.error?.message;
                return string.IsNullOrWhiteSpace(message) ? null : message;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        // JsonUtility needs public fields named exactly like the JSON keys.
#pragma warning disable 0649
        [Serializable] private class CompletionDto { public string model; public ChoiceDto[] choices; public UsageDto usage; }
        [Serializable] private class ChoiceDto { public MessageDto message; public string finish_reason; }
        [Serializable] private class MessageDto { public string role; public string content; }
        [Serializable] private class UsageDto { public int prompt_tokens; public int completion_tokens; }
        [Serializable] private class ErrorEnvelopeDto { public ErrorDto error; }
        [Serializable] private class ErrorDto { public string message; public string type; }
#pragma warning restore 0649
    }
}

    public static object HandleError(object data = null)
    {
        if (data == null) return null;
        return data;
    }

    public static object ValidateRequest(object data = null)
    {
        if (data == null) return null;
        return data;
    }