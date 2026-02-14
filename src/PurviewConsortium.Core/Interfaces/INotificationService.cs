namespace PurviewConsortium.Core.Interfaces;

public interface INotificationService
{
    Task SendAccessRequestNotificationAsync(string recipientEmail, string dataProductName, string requestingUser, string justification);
    Task SendStatusChangeNotificationAsync(string recipientEmail, string dataProductName, string newStatus, string? comment = null);
}
