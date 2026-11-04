using CloudHubs.Domain.Common;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Domain.Entities;

public class OrganizationRole : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public OrganizationPermission Permissions { get; set; }
    public ICollection<OrganizationMembership> Members { get; set; } = new List<OrganizationMembership>();
}
