namespace Guardiao.Domain.Entities;

public class ProtectedPerson
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; }
    public string Document { get; private set; }
    public DateTime RegisteredAt { get; private set; } = DateTime.UtcNow;

    public ProtectedPerson(string name, string document)
    {
        Name = name;
        Document = document;
    }
}
