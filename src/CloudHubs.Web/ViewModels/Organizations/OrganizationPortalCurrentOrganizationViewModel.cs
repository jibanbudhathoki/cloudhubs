namespace CloudHubs.Web.ViewModels.Organizations;

public sealed record OrganizationPortalCurrentOrganizationViewModel(
    Guid Id,
    string Name,
    string Email,
    string Slug,
    string Role,
    string PlanName,
    DateTime? ExpiresAt,
    int MaxProjects,
    int MaxMembers);
