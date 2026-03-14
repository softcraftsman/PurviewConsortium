using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAccessRequestSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceFabricWorkspaceId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "SourceInstitutionName",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "SourceLakehouseItemId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "SourceTenantId",
                table: "AccessRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFabricWorkspaceId",
                table: "AccessRequests",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceInstitutionName",
                table: "AccessRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceLakehouseItemId",
                table: "AccessRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceTenantId",
                table: "AccessRequests",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
