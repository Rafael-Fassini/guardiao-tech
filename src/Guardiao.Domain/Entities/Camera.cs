using Guardiao.Domain.Exceptions;

namespace Guardiao.Domain.Entities;

public class Camera
{
    private Camera()
    {
    }

    public Camera(Guid siteId, string name, string streamEndpoint)
    {
        if (siteId == Guid.Empty)
        {
            throw new InvariantViolationException("Camera must belong to a site.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvariantViolationException("Camera name is required.");
        }

        if (string.IsNullOrWhiteSpace(streamEndpoint))
        {
            throw new InvariantViolationException("Camera stream endpoint is required.");
        }

        Id = Guid.NewGuid();
        SiteId = siteId;
        Name = name.Trim();
        StreamEndpoint = streamEndpoint.Trim();
        IsEnabled = true;
    }

    public Guid Id { get; private set; }
    public Guid SiteId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string StreamEndpoint { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    public void Disable() => IsEnabled = false;
    public void Enable() => IsEnabled = true;
}
