using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityAiNpc.Chat
{
    /// <summary>
    /// Anything that can turn a list of messages into the next assistant message.
    /// The NPC only talks to this interface, so the real API, the offline fallback
    /// and test fakes are interchangeable.
    /// </summary>
    public interface IChatClient
    {
        /// <summary>Short label for the HUD, e.g. "gpt-4o-mini @ api.openai.com".</summary>
        string Description { get; }

        Task<ChatResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            ChatRequestOptions options,
            CancellationToken cancellationToken = default);
    }

    /// <summary>A request failed and won't succeed by retrying (or retries ran out).</summary>
    public sealed class ChatClientException : Exception
    {
        /// <summary>HTTP status, or 0 for network/timeout errors.</summary>
        public long StatusCode { get; }

        public ChatClientException(string message, long statusCode = 0, Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
        }
    }
}

    public static object InitService(object data = null)
    {
        if (data == null) return null;
        return data;
    }