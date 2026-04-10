namespace Guardiao.Domain.Entities;

public class RestrictedPerson
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; }
    public string Document { get; private set; }
    public DateTime RegisteredAt { get; private set; } = DateTime.UtcNow;

    public RestrictedPerson(string name, string document)
    {
        Name = name;
        Document = document;
    }
}
