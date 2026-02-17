using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Institutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PurviewAccountName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FabricWorkspaceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AdminConsentGranted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurviewQualifiedName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OwnerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassificationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GlossaryTermsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SensitivityLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsListed = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncedFromPurview = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurviewLastModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataProducts_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyncHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProductsFound = table.Column<int>(type: "int", nullable: false),
                    ProductsAdded = table.Column<int>(type: "int", nullable: false),
                    ProductsUpdated = table.Column<int>(type: "int", nullable: false),
                    ProductsDelisted = table.Column<int>(type: "int", nullable: false),
                    ErrorDetails = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncHistories_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestingUserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestingUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestingInstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetFabricWorkspaceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TargetLakehouseName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BusinessJustification = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RequestedDurationDays = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StatusChangedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExternalShareId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessRequests_DataProducts_DataProductId",
                        column: x => x.DataProductId,
                        principalTable: "DataProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessRequests_Institutions_RequestingInstitutionId",
                        column: x => x.RequestingInstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Institutions",
                columns: new[] { "Id", "AdminConsentGranted", "CreatedDate", "FabricWorkspaceId", "IsActive", "ModifiedDate", "Name", "PrimaryContactEmail", "PurviewAccountName", "TenantId" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "contoso-workspace-id", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Contoso University", "datasteward@contoso.edu", "contoso-purview", "contoso-tenant-id" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "fabrikam-workspace-id", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fabrikam Medical Center", "data@fabrikam.org", "fabrikam-purview", "fabrikam-tenant-id" }
                });

            migrationBuilder.InsertData(
                table: "DataProducts",
                columns: new[] { "Id", "ClassificationsJson", "CreatedDate", "Description", "GlossaryTermsJson", "InstitutionId", "IsListed", "LastSyncedFromPurview", "ModifiedDate", "Name", "Owner", "OwnerEmail", "PurviewLastModified", "PurviewQualifiedName", "SchemaJson", "SensitivityLabel", "SourceSystem" },
                values: new object[,]
                {
                    { new Guid("aaaa1111-1111-1111-1111-111111111111"), "[\"PHI\",\"De-identified\"]", new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Utc), "De-identified patient demographic data including age, gender, and geographic region.", "[\"Consortium-Shareable\",\"Demographics\"]", new Guid("11111111-1111-1111-1111-111111111111"), true, null, new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Utc), "Patient Demographics", "Dr. Jane Smith", "jsmith@contoso.edu", null, "contoso://datasets/patient-demographics", null, "Confidential", "Azure SQL Database" },
                    { new Guid("aaaa2222-2222-2222-2222-222222222222"), "[\"Research\",\"Aggregated\"]", new DateTime(2026, 2, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Aggregated clinical trial results and outcomes data from 2020-2025.", "[\"Consortium-Shareable\",\"Clinical-Trials\"]", new Guid("22222222-2222-2222-2222-222222222222"), true, null, new DateTime(2026, 2, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Clinical Trials Summary", "Dr. Alex Johnson", "ajohnson@fabrikam.org", null, "fabrikam://datasets/clinical-trials", null, "General", "Fabric Lakehouse" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_DataProductId",
                table: "AccessRequests",
                column: "DataProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_RequestingInstitutionId",
                table: "AccessRequests",
                column: "RequestingInstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_RequestingUserId_DataProductId",
                table: "AccessRequests",
                columns: new[] { "RequestingUserId", "DataProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_Status_RequestingInstitutionId",
                table: "AccessRequests",
                columns: new[] { "Status", "RequestingInstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DataProducts_InstitutionId_PurviewQualifiedName",
                table: "DataProducts",
                columns: new[] { "InstitutionId", "PurviewQualifiedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataProducts_IsListed",
                table: "DataProducts",
                column: "IsListed");

            migrationBuilder.CreateIndex(
                name: "IX_DataProducts_Name",
                table: "DataProducts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_Name",
                table: "Institutions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_TenantId",
                table: "Institutions",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncHistories_InstitutionId_StartTime",
                table: "SyncHistories",
                columns: new[] { "InstitutionId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_InstitutionId",
                table: "UserRoleAssignments",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId",
                table: "UserRoleAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_InstitutionId",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "InstitutionId" },
                unique: true,
                filter: "[InstitutionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessRequests");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "SyncHistories");

            migrationBuilder.DropTable(
                name: "UserRoleAssignments");

            migrationBuilder.DropTable(
                name: "DataProducts");

            migrationBuilder.DropTable(
                name: "Institutions");
        }
    }
}
