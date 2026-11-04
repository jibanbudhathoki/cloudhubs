using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Auth;

public sealed class TenantTokenRequest
{
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}
