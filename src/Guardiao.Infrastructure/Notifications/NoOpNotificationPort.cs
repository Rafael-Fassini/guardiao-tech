using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;

namespace Guardiao.Infrastructure.Notifications;

public sealed class NoOpNotificationPort : INotificationPort
{
    public Task NotifyIncidentCreatedAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
