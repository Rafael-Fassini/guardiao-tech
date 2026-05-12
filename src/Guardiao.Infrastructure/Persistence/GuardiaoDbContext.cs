using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Persistence;

public class GuardiaoDbContext : DbContext
{
    public GuardiaoDbContext(DbContextOptions<GuardiaoDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<ProtectedCase> ProtectedCases => Set<ProtectedCase>();
    public DbSet<PersonProjection> PersonProjections => Set<PersonProjection>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<WebhookDeliveryRecord> WebhookDeliveries => Set<WebhookDeliveryRecord>();
    public DbSet<SyncCursorRecord> SyncCursors => Set<SyncCursorRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProtectedCase>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalCaseId)
                .HasConversion(x => x.Value, x => new ExternalCaseId(x));
            entity.Property(x => x.MonitoringStatus)
                .HasConversion(x => x.Value, x => new MonitoringStatus(x));
            entity.Property(x => x.ConsentStatus)
                .HasConversion(x => x.Value, x => new ConsentStatus(x));
        });

        modelBuilder.Entity<PersonProjection>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalPersonId)
                .HasConversion(x => x.Value, x => new ExternalPersonId(x));
        });

        modelBuilder.Entity<AuditLog>(entity => entity.HasKey(x => x.Id));
        modelBuilder.Entity<WebhookDeliveryRecord>(entity => entity.HasKey(x => x.Id));
        modelBuilder.Entity<SyncCursorRecord>(entity => entity.HasKey(x => x.Name));

        base.OnModelCreating(modelBuilder);
    }
}
