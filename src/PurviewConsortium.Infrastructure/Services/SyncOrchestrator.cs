using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Infrastructure.Services;

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly IInstitutionRepository _institutionRepo;
    private readonly IDataProductRepository _dataProductRepo;
    private readonly IDataAssetRepository _dataAssetRepo;
    private readonly ISyncHistoryRepository _syncHistoryRepo;
    private readonly IPurviewScannerService _scanner;
    private readonly ICatalogSearchService _searchService;
    private readonly ConsortiumDbContext _dbContext;
    private readonly ILogger<SyncOrchestrator> _logger;

    public SyncOrchestrator(
        IInstitutionRepository institutionRepo,
        IDataProductRepository dataProductRepo,
        IDataAssetRepository dataAssetRepo,
        ISyncHistoryRepository syncHistoryRepo,
        IPurviewScannerService scanner,
        ICatalogSearchService searchService,
        ConsortiumDbContext dbContext,
        ILogger<SyncOrchestrator> logger)
    {
        _institutionRepo = institutionRepo;
        _dataProductRepo = dataProductRepo;
        _dataAssetRepo = dataAssetRepo;
        _syncHistoryRepo = syncHistoryRepo;
        _scanner = scanner;
        _searchService = searchService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ScanAllInstitutionsAsync(CancellationToken cancellationToken = default)
    {
        var institutions = await _institutionRepo.GetAllAsync(activeOnly: true);
        _logger.LogInformation("Starting scan for {Count} active institutions", institutions.Count);

        foreach (var institution in institutions)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await ScanInstitutionAsync(institution.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scan institution {InstitutionName} ({InstitutionId})",
                    institution.Name, institution.Id);
                // Continue with next institution — one failure shouldn't stop others
            }
        }

        _logger.LogInformation("Completed scan of all institutions");
    }

    public async Task ScanInstitutionAsync(Guid institutionId, CancellationToken cancellationToken = default)
    {
        var institution = await _institutionRepo.GetByIdAsync(institutionId)
            ?? throw new InvalidOperationException($"Institution {institutionId} not found");

        if (!institution.IsActive || !institution.AdminConsentGranted)
        {
            _logger.LogWarning("Skipping institution {Name} — not active or consent not granted", institution.Name);
            return;
        }

        var syncHistory = new SyncHistory
        {
            InstitutionId = institutionId,
            StartTime = DateTime.UtcNow,
            Status = SyncStatus.Success
        };
        await _syncHistoryRepo.CreateAsync(syncHistory);

        try
        {
            _logger.LogInformation("Scanning institution {Name} ({Id})", institution.Name, institution.Id);

            var scanResults = await _scanner.ScanForShareableDataProductsAsync(
                institution.PurviewAccountName,
                institution.TenantId,
                institution.ConsortiumDomainIds,
                cancellationToken);

            int added = 0, updated = 0;

            foreach (var result in scanResults)
            {
                var existing = await _dataProductRepo.GetByPurviewQualifiedNameAsync(
                    result.PurviewQualifiedName, institutionId);

                if (existing == null)
                {
                    // New product
                    var product = new DataProduct
                    {
                        PurviewQualifiedName = result.PurviewQualifiedName,
                        InstitutionId = institutionId,
                        Name = result.Name,
                        Description = result.Description,
                        Owner = result.Owner,
                        OwnerEmail = result.OwnerEmail,
                        SourceSystem = result.SourceSystem,
                        SchemaJson = result.SchemaJson,
                        ClassificationsJson = JsonSerializer.Serialize(result.Classifications),
                        GlossaryTermsJson = JsonSerializer.Serialize(result.GlossaryTerms),
                        SensitivityLabel = result.SensitivityLabel,
                        IsListed = true,
                        LastSyncedFromPurview = DateTime.UtcNow,
                        PurviewLastModified = result.PurviewLastModified,
                        Status = result.Status,
                        DataProductType = result.DataProductType,
                        GovernanceDomain = result.GovernanceDomain,
                        AssetCount = result.AssetCount,
                        BusinessUse = result.BusinessUse,
                        Endorsed = result.Endorsed,
                        UpdateFrequency = result.UpdateFrequency,
                        Documentation = result.Documentation,
                        UseCases = result.UseCases,
                        DataQualityScore = result.DataQualityScore,
                        TermsOfUseUrl = result.TermsOfUseUrl,
                        DocumentationUrl = result.DocumentationUrl,
                        DataAssetsJson = result.DataAssets.Count > 0
                            ? JsonSerializer.Serialize(result.DataAssets.Select(a => new { a.Name, a.Type, a.Description }))
                            : null
                    };
                    var created = await _dataProductRepo.CreateAsync(product);
                    await _searchService.IndexDataProductAsync(created.Id, cancellationToken);
                    added++;
                }
                else
                {
                    // Update existing
                    existing.Name = result.Name;
                    existing.Description = result.Description;
                    existing.Owner = result.Owner;
                    existing.OwnerEmail = result.OwnerEmail;
                    existing.SourceSystem = result.SourceSystem;
                    existing.SchemaJson = result.SchemaJson;
                    existing.ClassificationsJson = JsonSerializer.Serialize(result.Classifications);
                    existing.GlossaryTermsJson = JsonSerializer.Serialize(result.GlossaryTerms);
                    existing.SensitivityLabel = result.SensitivityLabel;
                    existing.IsListed = true;
                    existing.LastSyncedFromPurview = DateTime.UtcNow;
                    existing.PurviewLastModified = result.PurviewLastModified;
                    existing.Status = result.Status;
                    existing.DataProductType = result.DataProductType;
                    existing.GovernanceDomain = result.GovernanceDomain;
                    existing.AssetCount = result.AssetCount;
                    existing.BusinessUse = result.BusinessUse;
                    existing.Endorsed = result.Endorsed;
                    existing.UpdateFrequency = result.UpdateFrequency;
                    existing.Documentation = result.Documentation;
                    existing.UseCases = result.UseCases;
                    existing.DataQualityScore = result.DataQualityScore;
                    existing.TermsOfUseUrl = result.TermsOfUseUrl;
                    existing.DocumentationUrl = result.DocumentationUrl;
                    existing.DataAssetsJson = result.DataAssets.Count > 0
                        ? JsonSerializer.Serialize(result.DataAssets.Select(a => new { a.Name, a.Type, a.Description }))
                        : null;
                    await _dataProductRepo.UpdateAsync(existing);
                    await _searchService.IndexDataProductAsync(existing.Id, cancellationToken);
                    updated++;
                }
            }

            // Delist products no longer returned by Purview
            var activeNames = scanResults.Select(r => r.PurviewQualifiedName).ToList();
            var delisted = await _dataProductRepo.DelistByInstitutionExceptAsync(institutionId, activeNames);

            // ── Sync Data Assets ──────────────────────────────────────────
            int assetsAdded = 0, assetsUpdated = 0;
            try
            {
                var dataAssetResults = await _scanner.ScanForDataAssetsAsync(
                    institution.TenantId, cancellationToken);

                foreach (var assetResult in dataAssetResults)
                {
                    var existingAsset = await _dataAssetRepo.GetByPurviewAssetIdAsync(
                        assetResult.PurviewAssetId, institutionId);

                    if (existingAsset == null)
                    {
                        await _dataAssetRepo.CreateAsync(new DataAsset
                        {
                            PurviewAssetId = assetResult.PurviewAssetId,
                            InstitutionId = institutionId,
                            Name = assetResult.Name,
                            Type = assetResult.Type,
                            Description = assetResult.Description,
                            AssetType = assetResult.AssetType,
                            FullyQualifiedName = assetResult.FullyQualifiedName,
                            AccountName = assetResult.AccountName,
                            WorkspaceName = assetResult.WorkspaceName,
                            ProvisioningState = assetResult.ProvisioningState,
                            LastRefreshedAt = assetResult.LastRefreshedAt,
                            PurviewCreatedAt = assetResult.PurviewCreatedAt,
                            PurviewLastModifiedAt = assetResult.PurviewLastModifiedAt,
                            ContactsJson = assetResult.ContactsJson,
                            ClassificationsJson = assetResult.ClassificationsJson,
                        });
                        assetsAdded++;
                    }
                    else
                    {
                        existingAsset.Name = assetResult.Name;
                        existingAsset.Type = assetResult.Type;
                        existingAsset.Description = assetResult.Description;
                        existingAsset.AssetType = assetResult.AssetType;
                        existingAsset.FullyQualifiedName = assetResult.FullyQualifiedName;
                        existingAsset.AccountName = assetResult.AccountName;
                        existingAsset.WorkspaceName = assetResult.WorkspaceName;
                        existingAsset.ProvisioningState = assetResult.ProvisioningState;
                        existingAsset.LastRefreshedAt = assetResult.LastRefreshedAt;
                        existingAsset.PurviewCreatedAt = assetResult.PurviewCreatedAt;
                        existingAsset.PurviewLastModifiedAt = assetResult.PurviewLastModifiedAt;
                        existingAsset.ContactsJson = assetResult.ContactsJson;
                        existingAsset.ClassificationsJson = assetResult.ClassificationsJson;
                        await _dataAssetRepo.UpdateAsync(existingAsset);
                        assetsUpdated++;
                    }
                }

                // Remove assets no longer in Purview
                var activeAssetIds = dataAssetResults.Select(r => r.PurviewAssetId).ToList();
                await _dataAssetRepo.DeleteByInstitutionExceptAsync(institutionId, activeAssetIds);

                _logger.LogInformation(
                    "Data Assets sync for {Name}: {Added} added, {Updated} updated",
                    institution.Name, assetsAdded, assetsUpdated);
            }
            catch (Exception assetEx)
            {
                _logger.LogWarning(assetEx,
                    "Data Assets sync failed for {Name} — continuing with product sync results",
                    institution.Name);
            }

            // ── Link Data Assets to Data Products ─────────────────────────
            try
            {
                await LinkDataAssetsToProductsAsync(institutionId, scanResults, institution, cancellationToken);
            }
            catch (Exception linkEx)
            {
                _logger.LogWarning(linkEx,
                    "Data Asset linking failed for {Name} — continuing",
                    institution.Name);
            }

            syncHistory.EndTime = DateTime.UtcNow;
            syncHistory.Status = SyncStatus.Success;
            syncHistory.ProductsFound = scanResults.Count;
            syncHistory.ProductsAdded = added;
            syncHistory.ProductsUpdated = updated;
            syncHistory.ProductsDelisted = delisted;
            await _syncHistoryRepo.UpdateAsync(syncHistory);

            _logger.LogInformation(
                "Scan complete for {Name}: {Found} found, {Added} added, {Updated} updated, {Delisted} delisted",
                institution.Name, scanResults.Count, added, updated, delisted);
        }
        catch (Exception ex)
        {
            syncHistory.EndTime = DateTime.UtcNow;
            syncHistory.Status = SyncStatus.Failed;
            syncHistory.ErrorDetails = ex.Message.Length > 4000
                ? ex.Message[..3997] + "..."
                : ex.Message;
            await _syncHistoryRepo.UpdateAsync(syncHistory);
            throw;
        }
    }

    /// <summary>
    /// Links data assets to data products based on:
    /// 1. dataAssetId references found in the scan results (termsOfUse/documentation)
    /// 2. Individual product detail fetches from Purview for additional linkage
    /// </summary>
    private async Task LinkDataAssetsToProductsAsync(
        Guid institutionId,
        List<DataProductSyncResult> scanResults,
        Institution institution,
        CancellationToken cancellationToken)
    {
        // Get all data products for this institution
        var products = await _dataProductRepo.GetByInstitutionAsync(institutionId);
        var assets = await _dataAssetRepo.GetByInstitutionAsync(institutionId);

        if (!products.Any() || !assets.Any())
            return;

        // Build a lookup of PurviewAssetId -> DataAsset.Id for quick matching
        var assetLookup = assets.ToDictionary(a => a.PurviewAssetId, a => a.Id, StringComparer.OrdinalIgnoreCase);

        int linksCreated = 0;

        foreach (var product in products)
        {
            // Collect all Purview asset IDs that should be linked to this product
            var linkedPurviewAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Source 1: From the scan results (list endpoint) — termsOfUse/documentation dataAssetId refs
            var matchingScanResult = scanResults.FirstOrDefault(r =>
                string.Equals(r.PurviewQualifiedName, product.PurviewQualifiedName, StringComparison.OrdinalIgnoreCase));

            if (matchingScanResult != null)
            {
                foreach (var id in matchingScanResult.LinkedPurviewAssetIds)
                    linkedPurviewAssetIds.Add(id);
            }

            // Source 2: Fetch individual product detail from Purview for additional linkage
            if (linkedPurviewAssetIds.Count == 0 && !string.IsNullOrEmpty(product.PurviewQualifiedName))
            {
                try
                {
                    var detailIds = await _scanner.FetchProductLinkedAssetIdsAsync(
                        product.PurviewQualifiedName, institution.TenantId, cancellationToken);
                    foreach (var id in detailIds)
                        linkedPurviewAssetIds.Add(id);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex,
                        "Could not fetch detail for product {ProductId} — skipping detail linkage",
                        product.PurviewQualifiedName);
                }
            }

            if (linkedPurviewAssetIds.Count == 0)
                continue;

            // Remove existing links for this product
            var existingLinks = await _dbContext.DataProductDataAssets
                .Where(l => l.DataProductId == product.Id)
                .ToListAsync(cancellationToken);
            _dbContext.DataProductDataAssets.RemoveRange(existingLinks);

            // Create new links
            foreach (var purviewAssetId in linkedPurviewAssetIds)
            {
                if (assetLookup.TryGetValue(purviewAssetId, out var dataAssetId))
                {
                    _dbContext.DataProductDataAssets.Add(new DataProductDataAsset
                    {
                        DataProductId = product.Id,
                        DataAssetId = dataAssetId
                    });
                    linksCreated++;
                }
                else
                {
                    _logger.LogDebug(
                        "Product '{Product}' references asset ID {AssetId} which is not in our DB",
                        product.Name, purviewAssetId);
                }
            }
        }

        if (linksCreated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Linked {Count} data assets to products for {Name}",
                linksCreated, institution.Name);
        }
    }
}
