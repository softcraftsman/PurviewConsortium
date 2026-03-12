using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations;

public partial class AddDataProductDetailJsonFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DocumentationJson",
            table: "DataProducts",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OwnerContactsJson",
            table: "DataProducts",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TermsOfUseJson",
            table: "DataProducts",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DocumentationJson",
            table: "DataProducts");

        migrationBuilder.DropColumn(
            name: "OwnerContactsJson",
            table: "DataProducts");

        migrationBuilder.DropColumn(
            name: "TermsOfUseJson",
            table: "DataProducts");
    }
}