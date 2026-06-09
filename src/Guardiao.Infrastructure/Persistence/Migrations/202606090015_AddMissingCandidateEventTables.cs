using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardiao.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GuardiaoDbContext))]
[Migration("202606090015_AddMissingCandidateEventTables")]
public partial class AddMissingCandidateEventTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'BiometricCandidateEvents'
    ) THEN
        CREATE TABLE "BiometricCandidateEvents"
        (
            "Id" uuid NOT NULL,
            "ProtectedCaseId" uuid NOT NULL,
            "MatchScore" double precision NOT NULL,
            "CandidateCameraScopeSiteId" uuid NOT NULL,
            "CandidateCameraScopeCameraId" uuid NOT NULL,
            "OccurredAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_BiometricCandidateEvents" PRIMARY KEY ("Id")
        );
    END IF;
END
$$;
""");

        migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'CorrelationDecisions'
    ) THEN
        CREATE TABLE "CorrelationDecisions"
        (
            "Id" uuid NOT NULL,
            "ProtectedCaseId" uuid NOT NULL,
            "CandidateEventId" uuid NOT NULL,
            "CreatesIncident" boolean NOT NULL,
            "ReasonCode" text NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_CorrelationDecisions" PRIMARY KEY ("Id")
        );
    END IF;
END
$$;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DROP TABLE IF EXISTS "CorrelationDecisions";
""");

        migrationBuilder.Sql("""
DROP TABLE IF EXISTS "BiometricCandidateEvents";
""");
    }
}
