using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityAiNpc.Chat;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityAiNpc.Tests
{
    /// <summary>The real UnityWebRequest client against an in-process fake OpenAI server.</summary>
    public class OpenAIChatClientTests
    {
        private FakeOpenAIServer _server;

        private static readonly ChatMessage[] Messages =
        {
            ChatMessage.FromSystem("You are Brenna."),
            ChatMessage.FromUser("Can you fix my sword?"),
        };

        [SetUp] public void SetUp() => _server = new FakeOpenAIServer();
        [TearDown] public void TearDown() => _server.Dispose();

        private OpenAIChatClient Client(int maxAttempts = 3, int timeoutSeconds = 10, string key = "sk-test") =>
            new OpenAIChatClient(
                new ChatSettings { BaseUrl = _server.BaseUrl, ApiKey = key, Model = "fake-model", TimeoutSeconds = timeoutSeconds },
                new RetryPolicy { MaxAttempts = maxAttempts, BaseDelaySeconds = 0.05f, MaxDelaySeconds = 0.2f });

        private static IEnumerator Await<T>(Task<T> task, float timeoutSeconds = 15f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Task did not complete in time.");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Success_PostsJsonWithBearerKey_AndParsesReply()
        {
            _server.EnqueueCompletion("{\"say\":\"Fifteen silver.\",\"mood\":\"thinking\"}", 81, 14);

            var task = Client().CompleteAsync(Messages, new ChatRequestOptions { Model = "fake-model" });
            yield return Await(task);

            Assert.IsTrue(task.Status == TaskStatus.RanToCompletion, task.Exception?.ToString());
            Assert.AreEqual("{\"say\":\"Fifteen silver.\",\"mood\":\"thinking\"}", task.Result.Content);
            Assert.AreEqual(81, task.Result.PromptTokens);
            Assert.AreEqual(14, task.Result.CompletionTokens);

            Assert.AreEqual(1, _server.Received.Count);
            _server.Received.TryPeek(out var request);
            Assert.AreEqual("POST", request.Method);
            Assert.AreEqual("/v1/chat/completions", request.Path);
            Assert.AreEqual("Bearer sk-test", request.Headers["Authorization"]);
            StringAssert.StartsWith("application/json", request.Headers["Content-Type"]);
            StringAssert.Contains("\"model\":\"fake-model\"", request.Body);
            StringAssert.Contains("\"response_format\":{\"type\":\"json_object\"}", request.Body);
            StringAssert.Contains("Can you fix my sword?", request.Body);
        }

        [UnityTest]
        public IEnumerator NoKey_SendsNoAuthorizationHeader()
        {
            _server.EnqueueCompletion("{\"say\":\"Hi.\"}");
            var task = Client(key: null).CompleteAsync(Messages, new ChatRequestOptions());
            yield return Await(task);

            _server.Received.TryPeek(out var request);
            Assert.IsFalse(request.Headers.ContainsKey("Authorization"));
        }

        [UnityTest]
        public IEnumerator RateLimited_IsRetried_HonouringRetryAfter()
        {
            _server.Enqueue(429, "{\"error\":{\"message\":\"Slow down\"}}", headers: new Dictionary<string, string> { ["Retry-After"] = "0" });
            _server.EnqueueCompletion("{\"say\":\"There you are.\"}");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rate limited.*Slow down.*retry 1/2"));
            var task = Client().CompleteAsync(Messages, new ChatRequestOptions());
            yield return Await(task);

            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status, task.Exception?.ToString());
            Assert.AreEqual("{\"say\":\"There you are.\"}", task.Result.Content);
            Assert.AreEqual(2, _server.Received.Count);
        }

        [UnityTest]
        public IEnumerator Unauthorized_FailsFastWithTheServersMessage()
        {
            _server.Enqueue(401, "{\"error\":{\"message\":\"Incorrect API key provided.\",\"type\":\"invalid_request_error\"}}");
            _server.EnqueueCompletion("should never be requested");

            var task = Client().CompleteAsync(Messages, new ChatRequestOptions());
            yield return Await(task);

            Assert.IsTrue(task.IsFaulted);
            var error = (ChatClientException)task.Exception.InnerException;
            Assert.AreEqual(401, error.StatusCode);
            StringAssert.Contains("Incorrect API key provided.", error.Message);
            Assert.AreEqual(1, _server.Received.Count, "4xx must not be retried.");
        }

        [UnityTest]
        public IEnumerator ServerErrors_GiveUpAfterMaxAttempts()
        {
            for (int i = 0; i < 3; i++) _server.Enqueue(503, "{\"error\":{\"message\":\"overloaded\"}}");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("retry 1/2"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("retry 2/2"));

            var task = Client(maxAttempts: 3).CompleteAsync(Messages, new ChatRequestOptions());
            yield return Await(task);

            Assert.IsTrue(task.IsFaulted);
            Assert.AreEqual(503, ((ChatClientException)task.Exception.InnerException).StatusCode);
            Assert.AreEqual(3, _server.Received.Count);
        }

        [UnityTest]
        public IEnumerator SlowServer_TimesOutAsNetworkError()
        {
            _server.Enqueue(200, "{}", delayMs: 4000);

            var task = Client(maxAttempts: 1, timeoutSeconds: 1).CompleteAsync(Messages, new ChatRequestOptions());
            yield return Await(task, 10f);

            Assert.IsTrue(task.IsFaulted);
            Assert.AreEqual(0, ((ChatClientException)task.Exception.InnerException).StatusCode);
        }

        [UnityTest]
        public IEnumerator Cancellation_AbortsTheRequest()
        {
            _server.Enqueue(200, "{}", delayMs: 5000);
            var cts = new CancellationTokenSource();

            var task = Client().CompleteAsync(Messages, new ChatRequestOptions(), cts.Token);
            yield return new WaitForSecondsRealtime(0.3f);
            cts.Cancel();
            yield return Await(task, 3f);

            Assert.IsTrue(task.IsCanceled);
        }

        [UnityTest]
        public IEnumerator MalformedSuccessBody_IsAClientError()
        {
            _server.Enqueue(200, "<html>proxy login page</html>");
            var task = Client().CompleteAsync(Messages, new ChatRequestOptions());
            yield return Await(task);

            Assert.IsTrue(task.IsFaulted);
            Assert.IsInstanceOf<ChatClientException>(task.Exception.InnerException);
        }
    }
}
