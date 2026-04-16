using Guardiao.Domain.Entities;

namespace Guardiao.Domain.Ports;

public interface IDetectionEventAdapter
{
    DetectionEvent Adapt(object externalPayload);
}
