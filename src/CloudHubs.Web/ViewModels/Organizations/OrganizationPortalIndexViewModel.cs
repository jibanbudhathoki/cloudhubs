using CloudHubs.Web.ViewModels.Projects;

namespace CloudHubs.Web.ViewModels.Organizations;

public sealed class OrganizationPortalIndexViewModel
{
    public string UserName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public IReadOnlyCollection<OrganizationPortalMembershipViewModel> Organizations { get; init; } = [];
    public OrganizationPortalCurrentOrganizationViewModel? CurrentOrganization { get; init; }
    public IReadOnlyCollection<OrganizationPortalProjectViewModel> Projects { get; init; } = [];
    public IReadOnlyCollection<OrganizationPortalMemberViewModel> Members { get; init; } = [];
    public bool CanViewProjects { get; init; }
    public bool CanManageProjects { get; init; }
    public bool CanViewMembers { get; init; }
    public bool CanManageMembers { get; init; }
    public bool RequiresOrganizationSelection { get; init; }
    public bool CanCreateOrganization { get; init; }
    public bool CanShowOrganizationAction { get; init; }
    public bool HasAccountSubscription { get; init; }
    public int AccountOrganizationCount { get; init; }
    public int MaxOrganizations { get; init; }
    public string MemberFormEmail { get; init; } = string.Empty;
    public string MemberFormName { get; init; } = string.Empty;
    public string MemberFormRole { get; init; } = "Developer";
    public bool MemberFormOpen { get; init; }
}
