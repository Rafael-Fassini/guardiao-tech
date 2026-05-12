using Guardiao.Domain.Exceptions;

namespace Guardiao.Domain.Entities;

public class Site
{
    private readonly List<Camera> _cameras = [];

    private Site()
    {
    }

    public Site(Guid institutionId, string name, string addressLine)
    {
        if (institutionId == Guid.Empty)
        {
            throw new InvariantViolationException("Site must belong to an institution.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvariantViolationException("Site name is required.");
        }

        Id = Guid.NewGuid();
        InstitutionId = institutionId;
        Name = name.Trim();
        AddressLine = addressLine?.Trim() ?? string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid InstitutionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string AddressLine { get; private set; } = string.Empty;
    public IReadOnlyCollection<Camera> Cameras => _cameras;

    public Camera RegisterCamera(string name, string streamEndpoint)
    {
        var camera = new Camera(Id, name, streamEndpoint);
        _cameras.Add(camera);
        return camera;
    }
}
