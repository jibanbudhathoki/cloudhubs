using CloudHubs.Domain.Entities;

namespace CloudHubs.Web.ViewModels.Organizations;

public class OrganizationRolesIndexViewModel
{
    public Guid OrganizationId { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public IReadOnlyCollection<OrganizationRole> Roles { get; init; } = [];
    public IReadOnlyCollection<OrganizationMembership> Members { get; init; } = [];
}
