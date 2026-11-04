using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Auth;

public sealed class OrganizationLoginViewModel
{
    public string? OrganizationContext { get; set; }

    [Required, StringLength(200)]
    public string Identifier { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}
