using CloudHubs.Domain.Enums;

namespace CloudHubs.Domain.Entities;

public class OrganizationMembership
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public OrganizationMemberRole Role { get; set; }
    public Guid? CustomRoleId { get; set; }
    public OrganizationRole? CustomRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
