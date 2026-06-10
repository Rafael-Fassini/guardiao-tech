using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class ApiCandidateEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldRetryTransientFailure_ThenSucceed()
    {
        var handler = new SequencedHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    candidateEventId = Guid.NewGuid(),
                    wasDuplicate = false,
                    createsIncident = true,
                    decisionReasonCode = "CO_PRESENCE_MATCH",
                    incidentId = Guid.NewGuid()
                })
            });

        var metrics = new EdgeMetricsCollector();
        var publisher = CreatePublisher(handler, metrics);
        var candidateEvent = CreateCandidateEvent();

        await publisher.PublishAsync(candidateEvent);

        Assert.Equal(2, handler.RequestCount);
        Assert.Contains(metrics.SnapshotCounters().Keys, key => key.StartsWith("candidate_event_publish_retries_total", StringComparison.Ordinal));
        Assert.Contains(metrics.SnapshotCounters().Keys, key => key.StartsWith("candidate_event_publish_success_total", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenTransientFailuresAreExhausted()
    {
        var handler = new SequencedHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            new HttpResponseMessage(HttpStatusCode.BadGateway));

        var metrics = new EdgeMetricsCollector();
        var publisher = CreatePublisher(handler, metrics);

        await Assert.ThrowsAsync<HttpRequestException>(() => publisher.PublishAsync(CreateCandidateEvent()));

        Assert.Equal(3, handler.RequestCount);
        Assert.Contains(metrics.SnapshotCounters().Keys, key => key.StartsWith("candidate_event_publish_failures_total", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeEvidencePayload_WhenProvided()
    {
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                candidateEventId = Guid.NewGuid(),
                wasDuplicate = false,
                createsIncident = true,
                decisionReasonCode = "CO_PRESENCE_MATCH",
                incidentId = Guid.NewGuid()
            })
        });

        var metrics = new EdgeMetricsCollector();
        var publisher = CreatePublisher(handler, metrics);

        await publisher.PublishAsync(
            CreateCandidateEvent(),
            [
                new CandidateEventEvidencePayload("Snapshot", "snapshot.jpg", "image/jpeg", [1, 2, 3, 4])
            ]);

        Assert.NotNull(handler.Payload);
        Assert.Equal(1, handler.Payload!.RootElement.GetProperty("evidences").GetArrayLength());
        Assert.Contains(metrics.SnapshotCounters().Keys, key => key.StartsWith("evidence_bytes_uploaded_total", StringComparison.Ordinal));
    }

    private static ApiCandidateEventPublisher CreatePublisher(HttpMessageHandler handler, EdgeMetricsCollector metrics)
    {
        var options = Options.Create(new EdgeWorkerOptions
        {
            ApiBaseUrl = "https://guardiao-api.test",
            ApiSharedSecret = "worker-secret",
            WorkerId = "edge-worker-01",
            PublishRetryAttempts = 3,
            PublishInitialRetryDelayMilliseconds = 1,
            PublishTimeoutSeconds = 5
        });

        return new ApiCandidateEventPublisher(
            new HttpClient(handler) { BaseAddress = new Uri(options.Value.ApiBaseUrl) },
            options,
            metrics,
            NullLogger<ApiCandidateEventPublisher>.Instance);
    }

    private static BiometricCandidateEvent CreateCandidateEvent()
    {
        return new BiometricCandidateEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CameraScope(Guid.NewGuid(), Guid.NewGuid()),
            new MatchScore(0.91),
            DateTime.UtcNow);
    }

    private sealed class SequencedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequencedHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public CapturingHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public JsonDocument? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return _response;
        }
    }
}
