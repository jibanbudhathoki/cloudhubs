using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Auth;

public class SuperuserLoginViewModel
{
    [Required, StringLength(200)]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }
}
