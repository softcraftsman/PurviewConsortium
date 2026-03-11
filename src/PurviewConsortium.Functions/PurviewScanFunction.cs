using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Functions;

/// <summary>
/// Timer-triggered function that scans all institutions' Purview accounts
/// for Data Products tagged with "Consortium-Shareable" every 6 hours.
/// </summary>
public class PurviewScanFunction
{
    private readonly ISyncOrchestrator _syncOrchestrator;
    private readonly ILogger<PurviewScanFunction> _logger;

    public PurviewScanFunction(ISyncOrchestrator syncOrchestrator, ILogger<PurviewScanFunction> logger)
    {
        _syncOrchestrator = syncOrchestrator;
        _logger = logger;
    }

    [Function("PurviewScheduledScan")]
    public async Task RunScheduledScan(
        [TimerTrigger("0 0 */6 * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled Purview scan started at {Time}", DateTime.UtcNow);

        try
        {
            await _syncOrchestrator.ScanAllInstitutionsAsync(cancellationToken);
            _logger.LogInformation("Scheduled Purview scan completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled Purview scan failed");
            throw;
        }

        if (timerInfo.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next scan scheduled at {Next}", timerInfo.ScheduleStatus.Next);
        }
    }
}
