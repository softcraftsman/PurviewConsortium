using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataProductUnifiedCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssetCount",
                table: "DataProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BusinessUse",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataProductType",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Documentation",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Endorsed",
                table: "DataProducts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GovernanceDomain",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdateFrequency",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "DataProducts",
                keyColumn: "Id",
                keyValue: new Guid("aaaa1111-1111-1111-1111-111111111111"),
                columns: new[] { "AssetCount", "BusinessUse", "DataProductType", "Documentation", "Endorsed", "GovernanceDomain", "Status", "UpdateFrequency" },
                values: new object[] { 0, null, null, null, false, null, null, null });

            migrationBuilder.UpdateData(
                table: "DataProducts",
                keyColumn: "Id",
                keyValue: new Guid("aaaa2222-2222-2222-2222-222222222222"),
                columns: new[] { "AssetCount", "BusinessUse", "DataProductType", "Documentation", "Endorsed", "GovernanceDomain", "Status", "UpdateFrequency" },
                values: new object[] { 0, null, null, null, false, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssetCount",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "BusinessUse",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "DataProductType",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "Documentation",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "Endorsed",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "GovernanceDomain",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "UpdateFrequency",
                table: "DataProducts");
        }
    }
}
