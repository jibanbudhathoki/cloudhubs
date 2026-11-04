namespace CloudHubs.Web.ViewModels.Auth;

public sealed record SuperuserTokenResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt);
