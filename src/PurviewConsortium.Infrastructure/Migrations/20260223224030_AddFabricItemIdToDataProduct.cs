using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFabricItemIdToDataProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FabricItemId",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "DataProducts",
                keyColumn: "Id",
                keyValue: new Guid("aaaa1111-1111-1111-1111-111111111111"),
                column: "FabricItemId",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataProducts",
                keyColumn: "Id",
                keyValue: new Guid("aaaa2222-2222-2222-2222-222222222222"),
                column: "FabricItemId",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FabricItemId",
                table: "DataProducts");
        }
    }
}
