namespace Guardiao.Application.Services;

public sealed class CorrelationEngineOptions
{
    public TimeSpan CoPresenceWindow { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan DuplicateSuppressionWindow { get; init; } = TimeSpan.FromSeconds(30);
    public bool RequireSameSiteForCoPresence { get; init; } = true;
}
