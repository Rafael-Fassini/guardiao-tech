using Guardiao.Application.Ports.Outbound;

namespace Guardiao.Infrastructure.System;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
