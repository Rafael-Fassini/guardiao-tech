using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardiao.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GuardiaoDbContext))]
[Migration("202606090730_AddProtectedCaseSubjectRole")]
public partial class AddProtectedCaseSubjectRole : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SubjectRole",
            table: "ProtectedCases",
            type: "text",
            nullable: false,
            defaultValue: "ProtectedWoman");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SubjectRole",
            table: "ProtectedCases");
    }
}
