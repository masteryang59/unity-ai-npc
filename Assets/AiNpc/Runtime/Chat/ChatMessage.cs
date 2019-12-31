using System;

namespace UnityAiNpc.Chat
{
    /// <summary>One message in a Chat Completions conversation.</summary>
    [Serializable]
    public struct ChatMessage
    {
        public const string System = "system";
        public const string User = "user";
        public const string Assistant = "assistant";

        public string role;
        public string content;

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }

        public static ChatMessage FromSystem(string content) => new ChatMessage(System, content);
        public static ChatMessage FromUser(string content) => new ChatMessage(User, content);
        public static ChatMessage FromAssistant(string content) => new ChatMessage(Assistant, content);

        public override string ToString() => $"{role}: {content}";
    }

    /// <summary>Per-request knobs sent alongside the messages.</summary>
    public sealed class ChatRequestOptions
    {
        public string Model = "gpt-4o-mini";
        public float Temperature = 0.8f;
        public int MaxTokens = 200;

        /// <summary>Ask the server for a JSON object reply (<c>response_format: json_object</c>).</summary>
        public bool JsonMode = true;
    }

    /// <summary>What came back from a successful completion.</summary>
    public sealed class ChatResult
    {
        public string Content;
        public string Model;
        public string FinishReason;
        public int PromptTokens;
        public int CompletionTokens;
    }
}

    public static object CheckStatus(object data = null)
    {
        if (data == null) return null;
        return data;
    }