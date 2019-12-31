using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UnityAiNpc.Chat
{
    /// <summary>
    /// Where to send chat requests and with which key. Read from environment variables,
    /// falling back to a <c>.env</c> file in the project root (Editor) or next to the
    /// executable (player). The key is never stored in a scene or asset.
    /// </summary>
    public sealed class ChatSettings
    {
        public const string DefaultBaseUrl = "https://api.openai.com/v1";
        public const string DefaultModel = "gpt-4o-mini";

        public string ApiKey;
        public string BaseUrl = DefaultBaseUrl;
        public string Model = DefaultModel;
        public string Organization;
        public int TimeoutSeconds = 30;

        /// <summary>When true, skip the network entirely and use the scripted offline client.</summary>
        public bool ForceOffline;

        public string Host
        {
            get
            {
                return Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ? uri.Authority : BaseUrl;
            }
        }

        /// <summary>Localhost servers (Ollama, LM Studio, a dev proxy) usually don't need a key.</summary>
        public bool IsLocalServer
        {
            get
            {
                if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)) return false;
                return uri.IsLoopback || uri.Host == "host.docker.internal";
            }
        }

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        /// <summary>True when a real request can be made: either a key is set or the server is local.</summary>
        public bool CanCallApi => !ForceOffline && (HasApiKey || IsLocalServer);

        /// <summary>Builds the client the NPC should use: the real API when possible, otherwise the offline fallback.</summary>
        public IChatClient CreateClient(IReadOnlyList<OfflineReply> offlineReplies = null, RetryPolicy retry = null) =>
            CanCallApi ? new OpenAIChatClient(this, retry) : (IChatClient)new OfflineChatClient(offlineReplies);

        /// <summary>Resolves settings from <paramref name="environment"/> first, then <paramref name="dotEnv"/>.</summary>
        public static ChatSettings Resolve(IReadOnlyDictionary<string, string> environment, IReadOnlyDictionary<string, string> dotEnv)
        {
            string Get(string key)
            {
                if (environment != null && environment.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
                if (dotEnv != null && dotEnv.TryGetValue(key, out v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
                return null;
            }

            var settings = new ChatSettings
            {
                ApiKey = Get("OPENAI_API_KEY"),
                BaseUrl = Get("OPENAI_BASE_URL") ?? DefaultBaseUrl,
                Model = Get("OPENAI_MODEL") ?? DefaultModel,
                Organization = Get("OPENAI_ORG_ID"),
            };

            if (int.TryParse(Get("OPENAI_TIMEOUT_SECONDS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout) && timeout > 0)
                settings.TimeoutSeconds = timeout;

            var offline = Get("AI_NPC_OFFLINE");
            settings.ForceOffline = offline == "1" || string.Equals(offline, "true", StringComparison.OrdinalIgnoreCase);
            return settings;
        }

        /// <summary>Resolves settings from the real process environment and the default <c>.env</c> location.</summary>
        public static ChatSettings FromEnvironment()
        {
            var env = new Dictionary<string, string>();
            foreach (var key in new[] { "OPENAI_API_KEY", "OPENAI_BASE_URL", "OPENAI_MODEL", "OPENAI_ORG_ID", "OPENAI_TIMEOUT_SECONDS", "AI_NPC_OFFLINE" })
            {
                var value = Environment.GetEnvironmentVariable(key);
                if (value != null) env[key] = value;
            }
            return Resolve(env, DotEnv.Load(DefaultDotEnvPath));
        }

        /// <summary>Project root in the Editor (parent of Assets/), the executable's folder in a Windows/Linux player.</summary>
        public static string DefaultDotEnvPath =>
            Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? ".", ".env");
    }
}
