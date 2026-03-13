using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInstitutionFabricWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FabricWorkspaceId",
                table: "Institutions");

            migrationBuilder.AddColumn<string>(
                name: "SourceWorkspaceId",
                table: "DataAssets",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceWorkspaceId",
                table: "DataAssets");

            migrationBuilder.AddColumn<string>(
                name: "FabricWorkspaceId",
                table: "Institutions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
