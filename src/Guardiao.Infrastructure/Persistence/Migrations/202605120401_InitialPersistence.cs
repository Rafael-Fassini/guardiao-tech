using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardiao.Infrastructure.Persistence.Migrations;

public partial class InitialPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "public");

        migrationBuilder.CreateTable(
            name: "Institutions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Address = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Institutions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActorType = table.Column<int>(type: "integer", nullable: false),
                Action = table.Column<string>(type: "text", nullable: false),
                EntityName = table.Column<string>(type: "text", nullable: false),
                EntityId = table.Column<string>(type: "text", nullable: false),
                Details = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AuditLogs", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ProtectedCases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExternalCaseId = table.Column<string>(type: "text", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                PersonProjectionId = table.Column<Guid>(type: "uuid", nullable: false),
                MonitoringStatus = table.Column<string>(type: "text", nullable: false),
                ConsentStatus = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastSynchronizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastSyncStatus = table.Column<string>(type: "text", nullable: false),
                LastSyncFailureReason = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ProtectedCases", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Sites",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                AddressLine = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Sites", x => x.Id));

        migrationBuilder.CreateTable(
            name: "BiometricTemplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PersonProjectionId = table.Column<Guid>(type: "uuid", nullable: false),
                ExternalPersonId = table.Column<string>(type: "text", nullable: false),
                Embedding = table.Column<string>(type: "text", nullable: false),
                RetentionMode = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BiometricTemplates", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Incidents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProtectedCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                CandidateEventId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EscalatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReviewNotes = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Incidents", x => x.Id));

        migrationBuilder.CreateTable(
            name: "MonitoringRules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProtectedCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                CameraScopeSiteId = table.Column<Guid>(type: "uuid", nullable: false),
                CameraScopeCameraId = table.Column<Guid>(type: "uuid", nullable: false),
                ActiveWindowStartsAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                ActiveWindowEndsAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_MonitoringRules", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PersonProjections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExternalPersonId = table.Column<string>(type: "text", nullable: false),
                ProtectedCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                FullName = table.Column<string>(type: "text", nullable: false),
                IsBystander = table.Column<bool>(type: "boolean", nullable: false),
                SourceUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PersonProjections", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SyncCursors",
            columns: table => new
            {
                Name = table.Column<string>(type: "text", nullable: false),
                CursorUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SyncCursors", x => x.Name));

        migrationBuilder.CreateTable(
            name: "WebhookDeliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "text", nullable: false),
                ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WebhookDeliveries", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Cameras",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                StreamEndpoint = table.Column<string>(type: "text", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Cameras", x => x.Id));

        migrationBuilder.CreateTable(
            name: "EvidenceArtifacts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                ArtifactType = table.Column<int>(type: "integer", nullable: false),
                StoragePath = table.Column<string>(type: "text", nullable: false),
                RetentionMode = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_EvidenceArtifacts", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditLogs");
        migrationBuilder.DropTable(name: "BiometricTemplates");
        migrationBuilder.DropTable(name: "Cameras");
        migrationBuilder.DropTable(name: "EvidenceArtifacts");
        migrationBuilder.DropTable(name: "Incidents");
        migrationBuilder.DropTable(name: "MonitoringRules");
        migrationBuilder.DropTable(name: "PersonProjections");
        migrationBuilder.DropTable(name: "ProtectedCases");
        migrationBuilder.DropTable(name: "Sites");
        migrationBuilder.DropTable(name: "SyncCursors");
        migrationBuilder.DropTable(name: "WebhookDeliveries");
        migrationBuilder.DropTable(name: "Institutions");
    }
}
