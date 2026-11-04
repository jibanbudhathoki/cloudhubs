using CloudHubs.Domain.Common;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Domain.Entities;

public class User : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? LoginId { get; set; }
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public bool MustChangePasswordOnNextLogin { get; set; }
    public ICollection<OrganizationMembership> OrganizationMemberships { get; set; } = new List<OrganizationMembership>();
    public AccountSubscription? AccountSubscription { get; set; }
}
