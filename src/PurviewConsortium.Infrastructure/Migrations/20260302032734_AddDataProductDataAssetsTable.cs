using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataProductDataAssetsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataProductDataAssets",
                columns: table => new
                {
                    DataProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProductDataAssets", x => new { x.DataProductId, x.DataAssetId });
                    table.ForeignKey(
                        name: "FK_DataProductDataAssets_DataAssets_DataAssetId",
                        column: x => x.DataAssetId,
                        principalTable: "DataAssets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DataProductDataAssets_DataProducts_DataProductId",
                        column: x => x.DataProductId,
                        principalTable: "DataProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataProductDataAssets_DataAssetId",
                table: "DataProductDataAssets",
                column: "DataAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProductDataAssets");
        }
    }
}
