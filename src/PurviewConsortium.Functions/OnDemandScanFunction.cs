using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Functions;

/// <summary>
/// HTTP-triggered function for on-demand Purview scans.
/// Can scan all institutions or a specific one.
/// </summary>
public class OnDemandScanFunction
{
    private readonly ISyncOrchestrator _syncOrchestrator;
    private readonly ILogger<OnDemandScanFunction> _logger;

    public OnDemandScanFunction(ISyncOrchestrator syncOrchestrator, ILogger<OnDemandScanFunction> logger)
    {
        _syncOrchestrator = syncOrchestrator;
        _logger = logger;
    }

    [Function("OnDemandScan")]
    public async Task<HttpResponseData> RunOnDemandScan(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "scan")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("On-demand scan triggered");

        var institutionId = req.Query["institutionId"];

        try
        {
            if (!string.IsNullOrEmpty(institutionId) && Guid.TryParse(institutionId, out var id))
            {
                _logger.LogInformation("Scanning institution {Id}", id);
                await _syncOrchestrator.ScanInstitutionAsync(id, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Scanning all institutions");
                await _syncOrchestrator.ScanAllInstitutionsAsync(cancellationToken);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Scan completed successfully.");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "On-demand scan failed");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Scan failed: {ex.Message}");
            return errorResponse;
        }
    }
}
