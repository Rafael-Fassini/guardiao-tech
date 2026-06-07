using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Worker.Edge.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Services;

public sealed class ApiCandidateEventPublisher : ICandidateEventPublisher
{
    private readonly HttpClient _httpClient;
    private readonly EdgeWorkerOptions _options;
    private readonly EdgeMetricsCollector _metrics;
    private readonly ILogger<ApiCandidateEventPublisher> _logger;

    public ApiCandidateEventPublisher(
        HttpClient httpClient,
        IOptions<EdgeWorkerOptions> options,
        EdgeMetricsCollector metrics,
        ILogger<ApiCandidateEventPublisher> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task PublishAsync(BiometricCandidateEvent candidateEvent, CancellationToken cancellationToken = default)
    {
        var delay = TimeSpan.FromMilliseconds(_options.PublishInitialRetryDelayMilliseconds);

        for (var attempt = 1; attempt <= _options.PublishRetryAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/candidate-events")
                {
                    Content = JsonContent.Create(new CandidateEventIngestionRequest(
                        candidateEvent.Id,
                        candidateEvent.ProtectedCaseId,
                        candidateEvent.CameraScope.SiteId,
                        candidateEvent.CameraScope.CameraId,
                        candidateEvent.MatchScore.Value,
                        candidateEvent.OccurredAtUtc))
                };

                request.Headers.TryAddWithoutValidation("X-Worker-Id", _options.WorkerId);
                request.Headers.TryAddWithoutValidation("X-Worker-Auth", _options.ApiSharedSecret);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<CandidateEventIngestionResponse>(cancellationToken: cancellationToken);
                    if (payload?.WasDuplicate == true)
                    {
                        _metrics.IncrementCounter("candidate_event_publish_duplicates_total");
                    }
                    else
                    {
                        _metrics.IncrementCounter("candidate_event_publish_success_total");
                    }

                    return;
                }

                if (!IsTransient(response.StatusCode) || attempt == _options.PublishRetryAttempts)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _metrics.IncrementCounter("candidate_event_publish_failures_total");
                    throw new HttpRequestException(
                        $"Candidate event publish failed with status code {(int)response.StatusCode}: {responseBody}",
                        null,
                        response.StatusCode);
                }
            }
            catch (Exception ex) when (attempt < _options.PublishRetryAttempts && IsTransient(ex, cancellationToken))
            {
                _metrics.IncrementCounter("candidate_event_publish_retries_total");
                _logger.LogWarning(
                    ex,
                    "Candidate event publish attempt {Attempt} failed for {CandidateEventId}. Retrying in {DelayMs}ms.",
                    attempt,
                    candidateEvent.Id,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                continue;
            }
            catch (Exception ex)
            {
                _metrics.IncrementCounter("candidate_event_publish_failures_total");
                _logger.LogError(ex, "Candidate event publish failed for {CandidateEventId}.", candidateEvent.Id);
                throw;
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested &&
               (exception is HttpRequestException || exception is TaskCanceledException);
    }

    internal sealed record CandidateEventIngestionRequest(
        Guid EventId,
        Guid ProtectedCaseId,
        Guid SiteId,
        Guid CameraId,
        double MatchScore,
        DateTime OccurredAtUtc);

    internal sealed record CandidateEventIngestionResponse(
        Guid CandidateEventId,
        bool WasDuplicate,
        bool CreatesIncident,
        string DecisionReasonCode,
        Guid? IncidentId);
}
