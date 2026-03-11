using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataAssetsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: DataAssetsJson, DataQualityScore, DocumentationUrl, TermsOfUseUrl, UseCases columns
            // already exist on DataProducts from the AddDataProductUnifiedCatalogFields migration.
            // Only create the new DataAssets table.

            migrationBuilder.CreateTable(
                name: "DataAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurviewAssetId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AssetType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FullyQualifiedName = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AccountName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    WorkspaceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProvisioningState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastRefreshedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurviewCreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurviewLastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContactsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassificationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataAssets_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataAssets_InstitutionId_PurviewAssetId",
                table: "DataAssets",
                columns: new[] { "InstitutionId", "PurviewAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataAssets_Name",
                table: "DataAssets",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataAssets");
        }
    }
}
