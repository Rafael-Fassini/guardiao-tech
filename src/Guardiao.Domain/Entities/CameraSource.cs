namespace Guardiao.Domain.Entities;

public class CameraSource
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InstitutionId { get; private set; }
    public string Name { get; private set; }
    public string Location { get; private set; }
    public DateTime RegisteredAt { get; private set; } = DateTime.UtcNow;

    public CameraSource(Guid institutionId, string name, string location)
    {
        InstitutionId = institutionId;
        Name = name;
        Location = location;
    }
}
