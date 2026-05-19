using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Guardiao.Infrastructure.Persistence;

public class GuardiaoDbContext : DbContext
{
    public GuardiaoDbContext(DbContextOptions<GuardiaoDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<ProtectedCase> ProtectedCases => Set<ProtectedCase>();
    public DbSet<PersonProjection> PersonProjections => Set<PersonProjection>();
    public DbSet<MonitoringRule> MonitoringRules => Set<MonitoringRule>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<BiometricCandidateEvent> BiometricCandidateEvents => Set<BiometricCandidateEvent>();
    public DbSet<CorrelationDecision> CorrelationDecisions => Set<CorrelationDecision>();
    public DbSet<BiometricTemplate> BiometricTemplates => Set<BiometricTemplate>();
    public DbSet<EvidenceArtifact> EvidenceArtifacts => Set<EvidenceArtifact>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<WebhookDeliveryRecord> WebhookDeliveries => Set<WebhookDeliveryRecord>();
    public DbSet<SyncCursorRecord> SyncCursors => Set<SyncCursorRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Institution>(entity => entity.HasKey(x => x.Id));
        modelBuilder.Entity<Site>(entity => entity.HasKey(x => x.Id));
        modelBuilder.Entity<Camera>(entity => entity.HasKey(x => x.Id));

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

        modelBuilder.Entity<MonitoringRule>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.ComplexProperty(x => x.CameraScope, builder =>
            {
                builder.Property(x => x.SiteId).HasColumnName("CameraScopeSiteId");
                builder.Property(x => x.CameraId).HasColumnName("CameraScopeCameraId");
            });
            entity.ComplexProperty(x => x.ActiveWindow, builder =>
            {
                builder.Property(x => x.StartsAt).HasColumnName("ActiveWindowStartsAt");
                builder.Property(x => x.EndsAt).HasColumnName("ActiveWindowEndsAt");
            });
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<BiometricCandidateEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MatchScore)
                .HasConversion(x => x.Value, x => new MatchScore(x));
            entity.ComplexProperty(x => x.CameraScope, builder =>
            {
                builder.Property(x => x.SiteId).HasColumnName("CandidateCameraScopeSiteId");
                builder.Property(x => x.CameraId).HasColumnName("CandidateCameraScopeCameraId");
            });
        });

        modelBuilder.Entity<CorrelationDecision>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReasonCode)
                .HasConversion(x => x.Value, x => new CorrelationReasonCode(x));
        });

        modelBuilder.Entity<BiometricTemplate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalPersonId)
                .HasConversion(x => x.Value, x => new ExternalPersonId(x));
            entity.Property(x => x.RetentionMode)
                .HasConversion(x => x.Value, x => new RetentionMode(x));
            entity.Property(x => x.Embedding)
                .HasConversion(
                    x => string.Join(';', x.Select(v => v.ToString(CultureInfo.InvariantCulture))),
                    x => x.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => float.Parse(v, CultureInfo.InvariantCulture))
                        .ToArray());
        });

        modelBuilder.Entity<EvidenceArtifact>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ArtifactType).HasConversion<string>();
            entity.Property(x => x.RetentionMode)
                .HasConversion(x => x.Value, x => new RetentionMode(x));
        });

        modelBuilder.Entity<AuditLog>(entity => entity.HasKey(x => x.Id));
        modelBuilder.Entity<WebhookDeliveryRecord>(entity => entity.HasKey(x => x.Id));
        modelBuilder.Entity<SyncCursorRecord>(entity => entity.HasKey(x => x.Name));

        base.OnModelCreating(modelBuilder);
    }
}
