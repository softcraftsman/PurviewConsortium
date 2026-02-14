using System.Text.Json;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly IInstitutionRepository _institutionRepo;
    private readonly IDataProductRepository _dataProductRepo;
    private readonly ISyncHistoryRepository _syncHistoryRepo;
    private readonly IPurviewScannerService _scanner;
    private readonly ICatalogSearchService _searchService;
    private readonly ILogger<SyncOrchestrator> _logger;

    public SyncOrchestrator(
        IInstitutionRepository institutionRepo,
        IDataProductRepository dataProductRepo,
        ISyncHistoryRepository syncHistoryRepo,
        IPurviewScannerService scanner,
        ICatalogSearchService searchService,
        ILogger<SyncOrchestrator> logger)
    {
        _institutionRepo = institutionRepo;
        _dataProductRepo = dataProductRepo;
        _syncHistoryRepo = syncHistoryRepo;
        _scanner = scanner;
        _searchService = searchService;
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
                        PurviewLastModified = result.PurviewLastModified
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
                    await _dataProductRepo.UpdateAsync(existing);
                    await _searchService.IndexDataProductAsync(existing.Id, cancellationToken);
                    updated++;
                }
            }

            // Delist products no longer returned by Purview
            var activeNames = scanResults.Select(r => r.PurviewQualifiedName).ToList();
            var delisted = await _dataProductRepo.DelistByInstitutionExceptAsync(institutionId, activeNames);

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
            syncHistory.ErrorDetails = ex.Message;
            await _syncHistoryRepo.UpdateAsync(syncHistory);
            throw;
        }
    }
}
