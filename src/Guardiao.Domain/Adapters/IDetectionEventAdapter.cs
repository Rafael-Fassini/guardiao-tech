using Guardiao.Domain.Entities;

namespace Guardiao.Domain.Adapters;

public interface IDetectionEventAdapter
{
    DetectionEvent Adapt(object externalPayload);
}
