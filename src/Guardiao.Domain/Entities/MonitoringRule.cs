using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Domain.Entities;

public class MonitoringRule
{
    private MonitoringRule()
    {
    }

    public MonitoringRule(Guid protectedCaseId, CameraScope cameraScope, TimeWindow activeWindow, bool isEnabled = true)
    {
        if (protectedCaseId == Guid.Empty)
        {
            throw new InvariantViolationException("Monitoring rule must belong to a protected case.");
        }

        Id = Guid.NewGuid();
        ProtectedCaseId = protectedCaseId;
        CameraScope = cameraScope;
        ActiveWindow = activeWindow;
        IsEnabled = isEnabled;
    }

    public Guid Id { get; private set; }
    public Guid ProtectedCaseId { get; private set; }
    public CameraScope CameraScope { get; private set; }
    public TimeWindow ActiveWindow { get; private set; }
    public bool IsEnabled { get; private set; }

    public bool AppliesTo(CameraScope scope, TimeOnly time) =>
        IsEnabled &&
        CameraScope.SiteId == scope.SiteId &&
        CameraScope.CameraId == scope.CameraId &&
        ActiveWindow.Contains(time);
}
