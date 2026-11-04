namespace CloudHubs.Web.ViewModels.Auth;

public sealed record TenantTokenResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt,
    IReadOnlyCollection<TenantOrganizationMembershipResponse> Organizations);
