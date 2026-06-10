using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardiao.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GuardiaoDbContext))]
[Migration("202606090940_ConvertEvidenceArtifactTypeToText")]
public partial class ConvertEvidenceArtifactTypeToText : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "EvidenceArtifacts"
            ALTER COLUMN "ArtifactType" TYPE text
            USING (
                CASE "ArtifactType"
                    WHEN 1 THEN 'FaceCrop'
                    WHEN 2 THEN 'Snapshot'
                    WHEN 3 THEN 'AuditAttachment'
                    ELSE "ArtifactType"::text
                END
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "EvidenceArtifacts"
            ALTER COLUMN "ArtifactType" TYPE integer
            USING (
                CASE "ArtifactType"
                    WHEN 'FaceCrop' THEN 1
                    WHEN 'Snapshot' THEN 2
                    WHEN 'AuditAttachment' THEN 3
                    ELSE 2
                END
            );
            """);
    }
}
