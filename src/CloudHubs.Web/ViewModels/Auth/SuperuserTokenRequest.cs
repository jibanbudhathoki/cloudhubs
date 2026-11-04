using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Auth;

public sealed class SuperuserTokenRequest
{
    [Required, StringLength(200)]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
