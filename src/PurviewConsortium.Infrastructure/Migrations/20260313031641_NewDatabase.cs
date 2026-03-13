using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PurviewConsortium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NewDatabase : Migration
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
                    ConsortiumDomainIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AdminConsentGranted = table.Column<bool>(type: "bit", nullable: false),
                    AutoFulfillEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.Id);
                });

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
                    SourceWorkspaceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                    OwnerContactsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassificationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GlossaryTermsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SensitivityLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsListed = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncedFromPurview = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurviewLastModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataProductType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GovernanceDomain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssetCount = table.Column<int>(type: "int", nullable: false),
                    BusinessUse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Endorsed = table.Column<bool>(type: "bit", nullable: false),
                    UpdateFrequency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Documentation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UseCases = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataQualityScore = table.Column<int>(type: "int", nullable: true),
                    TermsOfUseUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TermsOfUseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentationUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataAssetsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceLakehouseItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
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
                    RequestingInstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestingTenantId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetFabricWorkspaceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TargetLakehouseItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BusinessJustification = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RequestedDurationDays = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StatusChangedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExternalShareId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FabricShortcutName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FabricShortcutCreated = table.Column<bool>(type: "bit", nullable: false),
                    PurviewWorkflowRunId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurviewWorkflowStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShareType = table.Column<int>(type: "int", nullable: false),
                    SourceFabricWorkspaceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceLakehouseItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SourceTenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceInstitutionName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
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

            migrationBuilder.InsertData(
                table: "Institutions",
                columns: new[] { "Id", "AdminConsentGranted", "AutoFulfillEnabled", "ConsortiumDomainIds", "CreatedDate", "IsActive", "ModifiedDate", "Name", "PrimaryContactEmail", "PurviewAccountName", "TenantId" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), true, false, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Contoso University", "datasteward@contoso.edu", "contoso-purview", "contoso-tenant-id" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), true, false, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fabrikam Medical Center", "data@fabrikam.org", "fabrikam-purview", "fabrikam-tenant-id" }
                });

            migrationBuilder.InsertData(
                table: "DataProducts",
                columns: new[] { "Id", "AssetCount", "BusinessUse", "ClassificationsJson", "CreatedDate", "DataAssetsJson", "DataProductType", "DataQualityScore", "Description", "Documentation", "DocumentationJson", "DocumentationUrl", "Endorsed", "GlossaryTermsJson", "GovernanceDomain", "InstitutionId", "IsListed", "LastSyncedFromPurview", "ModifiedDate", "Name", "Owner", "OwnerContactsJson", "OwnerEmail", "PurviewLastModified", "PurviewQualifiedName", "SchemaJson", "SensitivityLabel", "SourceLakehouseItemId", "SourceSystem", "Status", "TermsOfUseJson", "TermsOfUseUrl", "UpdateFrequency", "UseCases" },
                values: new object[,]
                {
                    { new Guid("aaaa1111-1111-1111-1111-111111111111"), 0, null, "[\"PHI\",\"De-identified\"]", new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Utc), "[{\"Name\":\"patient_demographics_v2\",\"Type\":\"Table\",\"Description\":\"Core demographics table with de-identified records\"},{\"Name\":\"geographic_regions\",\"Type\":\"Table\",\"Description\":\"Region lookup and mapping data\"}]", null, 87, "De-identified patient demographic data including age, gender, and geographic region.", null, null, "https://contoso.edu/datasets/patient-demographics/docs", false, "[\"Consortium-Shareable\",\"Demographics\"]", null, new Guid("11111111-1111-1111-1111-111111111111"), true, null, new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Utc), "Patient Demographics", "Dr. Jane Smith", null, "jsmith@contoso.edu", null, "contoso://datasets/patient-demographics", null, "Confidential", null, "Azure SQL Database", null, null, "https://contoso.edu/data-sharing/terms", "Weekly", "Population health studies, outcome analysis, and demographic trend research across the consortium." },
                    { new Guid("aaaa2222-2222-2222-2222-222222222222"), 0, null, "[\"Research\",\"Aggregated\"]", new DateTime(2026, 2, 1, 0, 0, 0, 0, DateTimeKind.Utc), "[{\"Name\":\"trials_summary_2020_2025\",\"Type\":\"Table\",\"Description\":\"Aggregated trial results and outcomes\"},{\"Name\":\"trial_metadata\",\"Type\":\"Table\",\"Description\":\"Trial identifiers, phases, and sponsors\"},{\"Name\":\"outcomes_analysis\",\"Type\":\"View\",\"Description\":\"Pre-computed outcome statistics\"}]", null, 92, "Aggregated clinical trial results and outcomes data from 2020-2025.", null, null, "https://fabrikam.org/datasets/clinical-trials/documentation", false, "[\"Consortium-Shareable\",\"Clinical-Trials\"]", null, new Guid("22222222-2222-2222-2222-222222222222"), true, null, new DateTime(2026, 2, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Clinical Trials Summary", "Dr. Alex Johnson", null, "ajohnson@fabrikam.org", null, "fabrikam://datasets/clinical-trials", null, "General", null, "Fabric Lakehouse", null, null, "https://fabrikam.org/data-governance/terms", "Monthly", "Cross-institutional research collaboration, meta-analyses of clinical trials, and regulatory reporting." }
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
                name: "IX_DataAssets_InstitutionId_PurviewAssetId",
                table: "DataAssets",
                columns: new[] { "InstitutionId", "PurviewAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataAssets_Name",
                table: "DataAssets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DataProductDataAssets_DataAssetId",
                table: "DataProductDataAssets",
                column: "DataAssetId");

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
                name: "DataProductDataAssets");

            migrationBuilder.DropTable(
                name: "SyncHistories");

            migrationBuilder.DropTable(
                name: "UserRoleAssignments");

            migrationBuilder.DropTable(
                name: "DataAssets");

            migrationBuilder.DropTable(
                name: "DataProducts");

            migrationBuilder.DropTable(
                name: "Institutions");
        }
    }
}
