using System;
using System.Collections.Generic;
using System.IO;

namespace UnityAiNpc.Chat
{
    /// <summary>Minimal <c>.env</c> reader: KEY=value lines, # comments, optional quotes and <c>export</c> prefix.</summary>
    public static class DotEnv
    {
        public static Dictionary<string, string> Parse(string text)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return values;

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line.StartsWith("export ", StringComparison.Ordinal)) line = line.Substring(7).TrimStart();

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();

                if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0])
                {
                    value = value.Substring(1, value.Length - 2);
                }
                else
                {
                    // Unquoted values may carry a trailing " # comment".
                    int hash = value.IndexOf(" #", StringComparison.Ordinal);
                    if (hash >= 0) value = value.Substring(0, hash).TrimEnd();
                }

                values[key] = value;
            }
            return values;
        }

        public static Dictionary<string, string> Load(string path)
        {
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllText(path)) : new Dictionary<string, string>();
            }
            catch (IOException)
            {
                return new Dictionary<string, string>();
            }
        }
    }
}

    public static object BuildQuery(object data = null)
    {
        if (data == null) return null;
        return data;
    }