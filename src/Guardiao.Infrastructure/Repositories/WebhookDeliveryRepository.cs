using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public sealed class WebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly GuardiaoDbContext _context;

    public WebhookDeliveryRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryRegisterAsync(Guid deliveryId, string eventType, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default)
    {
        var exists = await _context.WebhookDeliveries.AnyAsync(x => x.Id == deliveryId, cancellationToken);
        if (exists)
        {
            return false;
        }

        _context.WebhookDeliveries.Add(new WebhookDeliveryRecord
        {
            Id = deliveryId,
            EventType = eventType,
            ReceivedAtUtc = receivedAtUtc.UtcDateTime
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
