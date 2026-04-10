using Guardiao.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Persistence;

public class GuardiaoDbContext : DbContext
{
    public GuardiaoDbContext(DbContextOptions<GuardiaoDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<CameraSource> CameraSources => Set<CameraSource>();
    public DbSet<ProtectedPerson> ProtectedPeople => Set<ProtectedPerson>();
    public DbSet<RestrictedPerson> RestrictedPeople => Set<RestrictedPerson>();
    public DbSet<ProtectiveCase> ProtectiveCases => Set<ProtectiveCase>();
    public DbSet<DetectionEvent> DetectionEvents => Set<DetectionEvent>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurações adicionais podem ser feitas aqui
        base.OnModelCreating(modelBuilder);
    }
}
