using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardiao.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GuardiaoDbContext))]
[Migration("202606081130_AddEvidenceArtifactMetadata")]
public partial class AddEvidenceArtifactMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CandidateEventId",
            table: "EvidenceArtifacts",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ContentType",
            table: "EvidenceArtifacts",
            type: "text",
            nullable: false,
            defaultValue: "application/octet-stream");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CandidateEventId", table: "EvidenceArtifacts");
        migrationBuilder.DropColumn(name: "ContentType", table: "EvidenceArtifacts");
    }
}
