namespace Guardiao.Application.Services;

public sealed class CorrelationEngineOptions
{
    public TimeSpan CooldownWindow { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan DuplicateSuppressionWindow { get; init; } = TimeSpan.FromSeconds(30);
}
