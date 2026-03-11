using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoFulfillAndShareType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoFulfillEnabled",
                table: "Institutions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShareType",
                table: "AccessRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.UpdateData(
                table: "Institutions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "AutoFulfillEnabled",
                value: false);

            migrationBuilder.UpdateData(
                table: "Institutions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "AutoFulfillEnabled",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoFulfillEnabled",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "ShareType",
                table: "AccessRequests");

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
    }
}
