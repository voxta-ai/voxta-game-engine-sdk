using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Voxta.Unity.Tests
{
    public sealed class VoxtaAuthTests
    {
        [Test]
        public void RequestDeviceCodeUsesPinnedContract()
        {
            var transport = new FakeTransport(new VoxtaAuthHttpResponse(200, "{\"user_code\":\"ABCD-EFGH\",\"verification_url\":\"http://localhost:5384/device\",\"device_code\":\"secret\"}"));
            var auth = Create(transport, new MemoryStore());

            var code = auth.RequestDeviceCodeAsync("Unity \"test\"").GetAwaiter().GetResult();

            Assert.That(code.UserCode, Is.EqualTo("ABCD-EFGH"));
            Assert.That(code.VerificationUrl.AbsoluteUri, Is.EqualTo("http://localhost:5384/device"));
            Assert.That(transport.Requests[0].Uri.AbsolutePath, Is.EqualTo("/base/api/device/code"));
            Assert.That(transport.Requests[0].Body, Is.EqualTo("{\"client_id\":\"voxta\",\"scope\":\"role:app\",\"label\":\"Unity \\\"test\\\"\"}"));
            Assert.That(transport.Requests[0].Bearer, Is.Null);
        }

        [Test]
        public void PollWaitsForApprovalValidatesThenStoresTheToken()
        {
            var transport = new FakeTransport(
                new VoxtaAuthHttpResponse(204, string.Empty),
                new VoxtaAuthHttpResponse(200, "{\"token\":\"approved-key\"}"),
                new VoxtaAuthHttpResponse(200, "{}"));
            var store = new MemoryStore();
            var delays = 0;
            var auth = Create(transport, store, (interval, token) => { delays++; return Task.CompletedTask; });
            var code = new VoxtaDeviceCode { UserCode = "ABCD-EFGH", DeviceCode = "device-secret", VerificationUrl = new Uri("http://localhost/device") };

            var token = auth.WaitForTokenAsync(code).GetAwaiter().GetResult();

            Assert.That(token, Is.EqualTo("approved-key"));
            Assert.That(store.Token, Is.EqualTo("approved-key"));
            Assert.That(delays, Is.EqualTo(2));
            Assert.That(transport.Requests[0].Uri.AbsolutePath, Is.EqualTo("/base/api/device/poll"));
            Assert.That(transport.Requests[0].Body, Is.EqualTo("{\"device_code\":\"device-secret\"}"));
            Assert.That(transport.Requests[1].Bearer, Is.Null);
            Assert.That(transport.Requests[2].Uri.AbsolutePath, Is.EqualTo("/base/api/auth/test"));
            Assert.That(transport.Requests[2].Bearer, Is.EqualTo("approved-key"));
        }

        [Test]
        public void CancellationStopsPollingBeforeARequest()
        {
            var transport = new FakeTransport();
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var auth = Create(transport, new MemoryStore(), (interval, token) => Task.FromCanceled(token));
            var code = new VoxtaDeviceCode { DeviceCode = "device-secret" };

            Assert.That(() => auth.WaitForTokenAsync(code, cancellation.Token).GetAwaiter().GetResult(), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(transport.Requests, Is.Empty);
        }

        [Test]
        public void InvalidStoredTokenIsCleared()
        {
            var store = new MemoryStore { Token = "old-key" };
            var transport = new FakeTransport(new VoxtaAuthHttpResponse(401, "unauthorized"));
            var auth = Create(transport, store);

            Assert.That(auth.ValidateStoredTokenAsync().GetAwaiter().GetResult(), Is.False);
            Assert.That(store.Token, Is.Empty);
        }

        [Test]
        public void ValidationDoesNotRequestForAnEmptyToken()
        {
            var transport = new FakeTransport();
            var auth = Create(transport, new MemoryStore());

            Assert.That(auth.ValidateTokenAsync(" ").GetAwaiter().GetResult(), Is.False);
            Assert.That(transport.Requests, Is.Empty);
        }

        private static VoxtaAuth Create(FakeTransport transport, MemoryStore store, Func<TimeSpan, CancellationToken, Task> delay = null) =>
            new VoxtaAuth(new Uri("http://localhost:5384/base/"), store, transport, delay ?? ((interval, token) => Task.CompletedTask));

        private sealed class MemoryStore : IVoxtaTokenStore
        {
            public string Token = string.Empty;
            public string LoadToken() => Token;
            public void SaveToken(string token) => Token = token;
            public void ClearToken() => Token = string.Empty;
        }

        private sealed class FakeTransport : IVoxtaAuthTransport
        {
            private readonly Queue<VoxtaAuthHttpResponse> responses;
            public readonly List<Request> Requests = new List<Request>();

            public FakeTransport(params VoxtaAuthHttpResponse[] responses) => this.responses = new Queue<VoxtaAuthHttpResponse>(responses);
            public Task<VoxtaAuthHttpResponse> PostAsync(Uri uri, string jsonBody, string bearerToken, CancellationToken cancellationToken)
            {
                Requests.Add(new Request(uri, jsonBody, bearerToken));
                return Task.FromResult(responses.Dequeue());
            }
        }

        private readonly struct Request
        {
            public readonly Uri Uri;
            public readonly string Body;
            public readonly string Bearer;
            public Request(Uri uri, string body, string bearer) { Uri = uri; Body = body; Bearer = bearer; }
        }
    }
}
