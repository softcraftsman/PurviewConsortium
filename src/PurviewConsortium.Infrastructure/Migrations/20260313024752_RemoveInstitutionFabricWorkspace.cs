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
            migrationBuilder.AlterColumn<string>(
                name: "SourceLakehouseItemId",
                table: "DataProducts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

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
                name: "SourceFabricWorkspaceId",
                table: "DataProducts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsOfUseJson",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE dp
                SET dp.SourceFabricWorkspaceId = COALESCE(dp.SourceFabricWorkspaceId, i.FabricWorkspaceId)
                FROM DataProducts dp
                INNER JOIN Institutions i ON i.Id = dp.InstitutionId
                WHERE i.FabricWorkspaceId IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "FabricWorkspaceId",
                table: "Institutions");

            migrationBuilder.UpdateData(
                table: "DataProducts",
                keyColumn: "Id",
                keyValue: new Guid("aaaa1111-1111-1111-1111-111111111111"),
                columns: new[] { "DocumentationJson", "OwnerContactsJson", "SourceFabricWorkspaceId", "TermsOfUseJson" },
                values: new object[] { null, null, "contoso-workspace-id", null });

            migrationBuilder.UpdateData(
                table: "DataProducts",
                keyColumn: "Id",
                keyValue: new Guid("aaaa2222-2222-2222-2222-222222222222"),
                columns: new[] { "DocumentationJson", "OwnerContactsJson", "SourceFabricWorkspaceId", "TermsOfUseJson" },
                values: new object[] { null, null, "fabrikam-workspace-id", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentationJson",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "OwnerContactsJson",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "SourceFabricWorkspaceId",
                table: "DataProducts");

            migrationBuilder.DropColumn(
                name: "TermsOfUseJson",
                table: "DataProducts");

            migrationBuilder.AddColumn<string>(
                name: "FabricWorkspaceId",
                table: "Institutions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SourceLakehouseItemId",
                table: "DataProducts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.FabricWorkspaceId = src.SourceFabricWorkspaceId
                FROM Institutions i
                CROSS APPLY (
                    SELECT TOP 1 dp.SourceFabricWorkspaceId
                    FROM DataProducts dp
                    WHERE dp.InstitutionId = i.Id AND dp.SourceFabricWorkspaceId IS NOT NULL
                    ORDER BY dp.ModifiedDate DESC, dp.CreatedDate DESC
                ) src;
                """);

            migrationBuilder.UpdateData(
                table: "Institutions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "FabricWorkspaceId",
                value: "contoso-workspace-id");

            migrationBuilder.UpdateData(
                table: "Institutions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "FabricWorkspaceId",
                value: "fabrikam-workspace-id");
        }
    }
}
