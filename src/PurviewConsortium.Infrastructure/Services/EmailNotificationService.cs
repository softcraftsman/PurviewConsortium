using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

/// <summary>
/// Notification service using email (Azure Communication Services).
/// For PoC, logs notifications. Replace with ACS SDK calls for production.
/// </summary>
public class EmailNotificationService : INotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(IConfiguration configuration, ILogger<EmailNotificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendAccessRequestNotificationAsync(
        string recipientEmail,
        string dataProductName,
        string requestingUser,
        string justification)
    {
        // TODO: Integrate Azure Communication Services email SDK
        _logger.LogInformation(
            "NOTIFICATION: New access request for '{DataProduct}' from {User} sent to {Email}. Justification: {Justification}",
            dataProductName, requestingUser, recipientEmail, justification);

        return Task.CompletedTask;
    }

    public Task SendStatusChangeNotificationAsync(
        string recipientEmail,
        string dataProductName,
        string newStatus,
        string? comment = null)
    {
        // TODO: Integrate Azure Communication Services email SDK
        _logger.LogInformation(
            "NOTIFICATION: Access request for '{DataProduct}' status changed to {Status}. Sent to {Email}. Comment: {Comment}",
            dataProductName, newStatus, recipientEmail, comment ?? "(none)");

        return Task.CompletedTask;
    }
}
