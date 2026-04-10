namespace Guardiao.Domain.Entities;

public class ProtectiveCase
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InstitutionId { get; private set; }
    public Guid ProtectedPersonId { get; private set; }
    public Guid RestrictedPersonId { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public ProtectiveCase(Guid institutionId, Guid protectedPersonId, Guid restrictedPersonId, string status)
    {
        InstitutionId = institutionId;
        ProtectedPersonId = protectedPersonId;
        RestrictedPersonId = restrictedPersonId;
        Status = status;
    }
}
