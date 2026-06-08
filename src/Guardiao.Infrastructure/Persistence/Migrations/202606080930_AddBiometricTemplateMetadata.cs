using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardiao.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GuardiaoDbContext))]
[Migration("202606080930_AddBiometricTemplateMetadata")]
public partial class AddBiometricTemplateMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ContentType",
            table: "BiometricTemplates",
            type: "text",
            nullable: false,
            defaultValue: "application/octet-stream");

        migrationBuilder.AddColumn<DateTime>(
            name: "DeactivatedAtUtc",
            table: "BiometricTemplates",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "BiometricTemplates",
            type: "text",
            nullable: false,
            defaultValue: "template");

        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "BiometricTemplates",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "Source",
            table: "BiometricTemplates",
            type: "text",
            nullable: false,
            defaultValue: "legacy");

        migrationBuilder.AddColumn<string>(
            name: "StoragePath",
            table: "BiometricTemplates",
            type: "text",
            nullable: false,
            defaultValue: string.Empty);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ContentType", table: "BiometricTemplates");
        migrationBuilder.DropColumn(name: "DeactivatedAtUtc", table: "BiometricTemplates");
        migrationBuilder.DropColumn(name: "DisplayName", table: "BiometricTemplates");
        migrationBuilder.DropColumn(name: "IsActive", table: "BiometricTemplates");
        migrationBuilder.DropColumn(name: "Source", table: "BiometricTemplates");
        migrationBuilder.DropColumn(name: "StoragePath", table: "BiometricTemplates");
    }
}
