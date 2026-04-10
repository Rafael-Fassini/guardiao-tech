namespace Guardiao.Domain.Entities;

public class Institution
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; }
    public string Address { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public Institution(string name, string address)
    {
        Name = name;
        Address = address;
    }
}
