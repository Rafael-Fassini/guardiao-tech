using System.Globalization;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Guardiao.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GuardiaoDbContext))]
partial class GuardiaoDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.0");

        modelBuilder.Entity<Institution>(entity =>
        {
            entity.ToTable("Institutions");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("Sites");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Camera>(entity =>
        {
            entity.ToTable("Cameras");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ProtectedCase>(entity =>
        {
            entity.ToTable("ProtectedCases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalCaseId)
                .HasConversion(x => x.Value, x => new ExternalCaseId(x));
            entity.Property(x => x.MonitoringStatus)
                .HasConversion(x => x.Value, x => new MonitoringStatus(x));
            entity.Property(x => x.ConsentStatus)
                .HasConversion(x => x.Value, x => new ConsentStatus(x));
            entity.Property(x => x.SubjectRole)
                .HasConversion<string>()
                .HasDefaultValue(MonitoredSubjectRole.ProtectedWoman);
        });

        modelBuilder.Entity<PersonProjection>(entity =>
        {
            entity.ToTable("PersonProjections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalPersonId)
                .HasConversion(x => x.Value, x => new ExternalPersonId(x));
        });

        modelBuilder.Entity<MonitoringRule>(entity =>
        {
            entity.ToTable("MonitoringRules");
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
            entity.ToTable("Incidents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<IncidentNotificationRecord>(entity =>
        {
            entity.ToTable("IncidentNotificationRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType);
            entity.Property(x => x.Channel);
            entity.Property(x => x.DeliveryStatus);
            entity.Property(x => x.Details);
        });

        modelBuilder.Entity<BiometricCandidateEvent>(entity =>
        {
            entity.ToTable("BiometricCandidateEvents");
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
            entity.ToTable("CorrelationDecisions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReasonCode)
                .HasConversion(x => x.Value, x => new CorrelationReasonCode(x));
        });

        modelBuilder.Entity<BiometricTemplate>(entity =>
        {
            entity.ToTable("BiometricTemplates");
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
            entity.Property(x => x.Source);
            entity.Property(x => x.DisplayName);
            entity.Property(x => x.ContentType);
            entity.Property(x => x.StoragePath);
            entity.Property(x => x.IsActive);
        });

        modelBuilder.Entity<EvidenceArtifact>(entity =>
        {
            entity.ToTable("EvidenceArtifacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ArtifactType).HasConversion<string>();
            entity.Property(x => x.ContentType);
            entity.Property(x => x.RetentionMode)
                .HasConversion(x => x.Value, x => new RetentionMode(x));
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<WebhookDeliveryRecord>(entity =>
        {
            entity.ToTable("WebhookDeliveries");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<SyncCursorRecord>(entity =>
        {
            entity.ToTable("SyncCursors");
            entity.HasKey(x => x.Name);
        });
    }
}
