namespace CloudHubs.Web.ViewModels.Auth;

public sealed record TenantOrganizationMembershipResponse(
    Guid OrganizationId,
    string OrganizationName,
    string Role);
