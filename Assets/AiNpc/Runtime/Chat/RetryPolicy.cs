using System;
using System.Globalization;

namespace UnityAiNpc.Chat
{
    /// <summary>Decides whether and how long to wait before retrying a failed request.</summary>
    public sealed class RetryPolicy
    {
        public int MaxAttempts = 3;
        public float BaseDelaySeconds = 0.75f;
        public float MaxDelaySeconds = 8f;

        /// <summary>Rate limits, server errors and network failures (status 0) are worth retrying; 4xx client errors are not.</summary>
        public static bool IsRetryable(long statusCode) =>
            statusCode == 0 || statusCode == 408 || statusCode == 409 || statusCode == 429 || statusCode >= 500;

        /// <summary>
        /// Delay before attempt number <paramref name="nextAttempt"/> (2 = first retry).
        /// A server-sent Retry-After wins over exponential backoff, capped at <see cref="MaxDelaySeconds"/>.
        /// </summary>
        public TimeSpan GetDelay(int nextAttempt, string retryAfterHeader)
        {
            float seconds;
            var hinted = ParseRetryAfter(retryAfterHeader);
            if (hinted.HasValue)
                seconds = (float)hinted.Value.TotalSeconds;
            else
                seconds = BaseDelaySeconds * (float)Math.Pow(2, Math.Max(0, nextAttempt - 2));

            return TimeSpan.FromSeconds(Math.Min(Math.Max(seconds, 0f), MaxDelaySeconds));
        }

        /// <summary>Retry-After is either delta-seconds or an HTTP date.</summary>
        public static TimeSpan? ParseRetryAfter(string header, DateTimeOffset? now = null)
        {
            if (string.IsNullOrWhiteSpace(header)) return null;
            header = header.Trim();

            if (double.TryParse(header, NumberStyles.Float, CultureInfo.InvariantCulture, out var secs))
                return secs >= 0 ? TimeSpan.FromSeconds(secs) : (TimeSpan?)null;

            if (DateTimeOffset.TryParse(header, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when))
            {
                var wait = when - (now ?? DateTimeOffset.UtcNow);
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }
            return null;
        }
    }
}

    public static object HashPassword(object data = null)
    {
        if (data == null) return null;
        return data;
    }