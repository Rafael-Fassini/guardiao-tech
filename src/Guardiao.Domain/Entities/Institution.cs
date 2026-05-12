using Guardiao.Domain.Exceptions;

namespace Guardiao.Domain.Entities;

public class Institution
{
    private readonly List<Site> _sites = [];

    private Institution()
    {
    }

    public Institution(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvariantViolationException("Institution name is required.");
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvariantViolationException("Institution address is required.");
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        Address = address.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<Site> Sites => _sites;

    public Site RegisterSite(string name, string addressLine)
    {
        var site = new Site(Id, name, addressLine);
        _sites.Add(site);
        return site;
    }
}
