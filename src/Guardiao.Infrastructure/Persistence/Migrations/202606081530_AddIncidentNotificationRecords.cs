using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardiao.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GuardiaoDbContext))]
[Migration("202606081530_AddIncidentNotificationRecords")]
public partial class AddIncidentNotificationRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IncidentNotificationRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "text", nullable: false),
                Channel = table.Column<string>(type: "text", nullable: false),
                DeliveryStatus = table.Column<string>(type: "text", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                HasEvidence = table.Column<bool>(type: "boolean", nullable: false),
                Details = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncidentNotificationRecords", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IncidentNotificationRecords");
    }
}
