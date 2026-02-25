using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameToSourceAndTargetLakehouseIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FabricItemId",
                table: "DataProducts",
                newName: "SourceLakehouseItemId");

            migrationBuilder.RenameColumn(
                name: "TargetLakehouseName",
                table: "AccessRequests",
                newName: "TargetLakehouseItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SourceLakehouseItemId",
                table: "DataProducts",
                newName: "FabricItemId");

            migrationBuilder.RenameColumn(
                name: "TargetLakehouseItemId",
                table: "AccessRequests",
                newName: "TargetLakehouseName");
        }
    }
}
