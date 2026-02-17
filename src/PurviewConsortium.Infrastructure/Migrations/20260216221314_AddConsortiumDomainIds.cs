using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsortiumDomainIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsortiumDomainIds",
                table: "Institutions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Institutions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "ConsortiumDomainIds",
                value: null);

            migrationBuilder.UpdateData(
                table: "Institutions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "ConsortiumDomainIds",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsortiumDomainIds",
                table: "Institutions");
        }
    }
}
