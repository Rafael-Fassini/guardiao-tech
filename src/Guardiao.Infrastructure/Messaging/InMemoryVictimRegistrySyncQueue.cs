using System.Threading.Channels;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Infrastructure.Messaging;

public sealed class InMemoryVictimRegistrySyncQueue : IVictimRegistrySyncQueue
{
    private readonly Channel<ExternalCaseId> _channel = Channel.CreateUnbounded<ExternalCaseId>();

    public ValueTask EnqueueAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(externalCaseId, cancellationToken);
    }

    public ValueTask<ExternalCaseId> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
