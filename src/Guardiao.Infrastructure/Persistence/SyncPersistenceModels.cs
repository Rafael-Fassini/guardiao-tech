namespace Guardiao.Infrastructure.Persistence;

public class WebhookDeliveryRecord
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
}

public class SyncCursorRecord
{
    public string Name { get; set; } = string.Empty;
    public DateTime CursorUtc { get; set; }
}
