namespace CloudHubs.Web.ViewModels.Organizations;

public sealed record OrganizationPortalMemberViewModel(
    Guid UserId,
    string MemberId,
    string Name,
    string Email,
    string Role,
    bool IsOwner,
    bool IsCustomRole);
