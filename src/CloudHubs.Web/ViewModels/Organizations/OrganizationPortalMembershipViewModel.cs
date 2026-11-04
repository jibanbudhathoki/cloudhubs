namespace CloudHubs.Web.ViewModels.Organizations;

public sealed record OrganizationPortalMembershipViewModel(
    Guid Id,
    string Name,
    string Email,
    string Slug,
    string Role);
