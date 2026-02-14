using PurviewConsortium.Core.Enums;

namespace PurviewConsortium.Core.Entities;

public class UserRoleAssignment
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public Guid? InstitutionId { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Institution? Institution { get; set; }
}
