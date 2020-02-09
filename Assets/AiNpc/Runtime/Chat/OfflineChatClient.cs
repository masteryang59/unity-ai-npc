using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityAiNpc.Chat
{
    /// <summary>A canned line the offline client says when the player's message contains one of the keywords.</summary>
    [Serializable]
    public class OfflineReply
    {
        [UnityEngine.Tooltip("Comma-separated, case-insensitive. Matches if any keyword appears in the player's message.")]
        public string keywords;
        [UnityEngine.TextArea(2, 4)]
        public string reply;
        public string mood = "neutral";
        public bool endsConversation;

        public OfflineReply() { }

        public OfflineReply(string keywords, string reply, string mood, bool endsConversation = false)
        {
            this.keywords = keywords;
            this.reply = reply;
            this.mood = mood;
            this.endsConversation = endsConversation;
        }
    }

    /// <summary>
    /// Keyword-matching stand-in used when there is no API key, so the scene is playable
    /// out of the box. Replies in the same JSON shape the model is asked for, so the rest
    /// of the pipeline runs unchanged.
    /// </summary>
    public sealed class OfflineChatClient : IChatClient
    {
        private readonly IReadOnlyList<OfflineReply> _replies;
        private readonly string _fallback;

        public OfflineChatClient(IReadOnlyList<OfflineReply> replies = null, string fallback = null)
        {
            _replies = replies ?? Array.Empty<OfflineReply>();
            _fallback = fallback ?? "I'm only running on canned lines right now. Add an OPENAI_API_KEY to the .env file and I'll actually listen.";
        }

        public string Description => "offline (scripted replies)";

        public Task<ChatResult> CompleteAsync(IReadOnlyList<ChatMessage> messages, ChatRequestOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string said = LastUserMessage(messages) ?? string.Empty;

            string reply = _fallback;
            string mood = "thinking";
            bool ends = false;
            foreach (var candidate in _replies)
            {
                if (Matches(said, candidate.keywords))
                {
                    reply = candidate.reply;
                    mood = string.IsNullOrEmpty(candidate.mood) ? "neutral" : candidate.mood;
                    ends = candidate.endsConversation;
                    break;
                }
            }

            string content = "{\"say\":" + ChatCompletionJson.Quote(reply) + ",\"mood\":" + ChatCompletionJson.Quote(mood) + ",\"end_conversation\":" + (ends ? "true" : "false") + "}";
            return Task.FromResult(new ChatResult { Content = content, Model = "offline", FinishReason = "stop" });
        }

        private static string LastUserMessage(IReadOnlyList<ChatMessage> messages)
        {
            if (messages == null) return null;
            for (int i = messages.Count - 1; i >= 0; i--)
                if (messages[i].role == ChatMessage.User) return messages[i].content;
            return null;
        }

        private static bool Matches(string text, string keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords)) return false;
            // Whole-word match, so "hi" doesn't fire on "this" or "chipped".
            string haystack = " " + Normalize(text) + " ";
            foreach (var keyword in keywords.Split(','))
            {
                var k = Normalize(keyword);
                if (k.Length > 0 && haystack.Contains(" " + k + " ")) return true;
            }
            return false;
        }

        private static string Normalize(string text)
        {
            var chars = (text ?? string.Empty).ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = ' ';
            return string.Join(" ", new string(chars).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
