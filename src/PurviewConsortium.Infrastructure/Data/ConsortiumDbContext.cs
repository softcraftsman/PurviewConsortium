using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;

namespace PurviewConsortium.Infrastructure.Data;

public class ConsortiumDbContext : DbContext
{
    public ConsortiumDbContext(DbContextOptions<ConsortiumDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<DataProduct> DataProducts => Set<DataProduct>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<SyncHistory> SyncHistories => Set<SyncHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Institution
        modelBuilder.Entity<Institution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.TenantId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.PurviewAccountName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.FabricWorkspaceId).HasMaxLength(128);
            entity.Property(e => e.PrimaryContactEmail).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.TenantId).IsUnique();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // DataProduct
        modelBuilder.Entity<DataProduct>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PurviewQualifiedName).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Owner).HasMaxLength(256);
            entity.Property(e => e.OwnerEmail).HasMaxLength(256);
            entity.Property(e => e.SourceSystem).HasMaxLength(256);
            entity.Property(e => e.SensitivityLabel).HasMaxLength(128);

            entity.HasIndex(e => new { e.InstitutionId, e.PurviewQualifiedName }).IsUnique();
            entity.HasIndex(e => e.IsListed);
            entity.HasIndex(e => e.Name);

            entity.HasOne(e => e.Institution)
                .WithMany(i => i.DataProducts)
                .HasForeignKey(e => e.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AccessRequest
        modelBuilder.Entity<AccessRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestingUserId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.RequestingUserEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.RequestingUserName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.TargetFabricWorkspaceId).HasMaxLength(128);
            entity.Property(e => e.TargetLakehouseName).HasMaxLength(256);
            entity.Property(e => e.BusinessJustification).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.StatusChangedBy).HasMaxLength(256);
            entity.Property(e => e.ExternalShareId).HasMaxLength(256);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasIndex(e => new { e.Status, e.RequestingInstitutionId });
            entity.HasIndex(e => new { e.RequestingUserId, e.DataProductId });
            entity.HasIndex(e => e.DataProductId);

            entity.HasOne(e => e.DataProduct)
                .WithMany(d => d.AccessRequests)
                .HasForeignKey(e => e.DataProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RequestingInstitution)
                .WithMany(i => i.IncomingRequests)
                .HasForeignKey(e => e.RequestingInstitutionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SyncHistory
        modelBuilder.Entity<SyncHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(e => e.ErrorDetails).HasMaxLength(4000);

            entity.HasIndex(e => new { e.InstitutionId, e.StartTime });

            entity.HasOne(e => e.Institution)
                .WithMany(i => i.SyncHistories)
                .HasForeignKey(e => e.InstitutionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(128);
            entity.Property(e => e.UserEmail).HasMaxLength(256);
            entity.Property(e => e.Action)
                .HasConversion<string>()
                .HasMaxLength(64);
            entity.Property(e => e.EntityType).HasMaxLength(128);
            entity.Property(e => e.EntityId).HasMaxLength(128);
            entity.Property(e => e.IpAddress).HasMaxLength(64);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
        });

        // UserRoleAssignment
        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.UserEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Role)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.InstitutionId }).IsUnique();

            entity.HasOne(e => e.Institution)
                .WithMany()
                .HasForeignKey(e => e.InstitutionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var institutionId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var institutionId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        modelBuilder.Entity<Institution>().HasData(
            new Institution
            {
                Id = institutionId1,
                Name = "Contoso University",
                TenantId = "contoso-tenant-id",
                PurviewAccountName = "contoso-purview",
                FabricWorkspaceId = "contoso-workspace-id",
                PrimaryContactEmail = "datasteward@contoso.edu",
                IsActive = true,
                AdminConsentGranted = true,
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Institution
            {
                Id = institutionId2,
                Name = "Fabrikam Medical Center",
                TenantId = "fabrikam-tenant-id",
                PurviewAccountName = "fabrikam-purview",
                FabricWorkspaceId = "fabrikam-workspace-id",
                PrimaryContactEmail = "data@fabrikam.org",
                IsActive = true,
                AdminConsentGranted = true,
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<DataProduct>().HasData(
            new DataProduct
            {
                Id = Guid.Parse("aaaa1111-1111-1111-1111-111111111111"),
                PurviewQualifiedName = "contoso://datasets/patient-demographics",
                InstitutionId = institutionId1,
                Name = "Patient Demographics",
                Description = "De-identified patient demographic data including age, gender, and geographic region.",
                Owner = "Dr. Jane Smith",
                OwnerEmail = "jsmith@contoso.edu",
                SourceSystem = "Azure SQL Database",
                ClassificationsJson = "[\"PHI\",\"De-identified\"]",
                GlossaryTermsJson = "[\"Consortium-Shareable\",\"Demographics\"]",
                SensitivityLabel = "Confidential",
                IsListed = true,
                CreatedDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new DataProduct
            {
                Id = Guid.Parse("aaaa2222-2222-2222-2222-222222222222"),
                PurviewQualifiedName = "fabrikam://datasets/clinical-trials",
                InstitutionId = institutionId2,
                Name = "Clinical Trials Summary",
                Description = "Aggregated clinical trial results and outcomes data from 2020-2025.",
                Owner = "Dr. Alex Johnson",
                OwnerEmail = "ajohnson@fabrikam.org",
                SourceSystem = "Fabric Lakehouse",
                ClassificationsJson = "[\"Research\",\"Aggregated\"]",
                GlossaryTermsJson = "[\"Consortium-Shareable\",\"Clinical-Trials\"]",
                SensitivityLabel = "General",
                IsListed = true,
                CreatedDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
