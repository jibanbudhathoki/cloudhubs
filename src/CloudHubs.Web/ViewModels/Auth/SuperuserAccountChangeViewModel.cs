using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Auth;

public class SuperuserAccountChangeViewModel
{
    [Required]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [EmailAddress, StringLength(200)]
    public string? NewEmail { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 8)]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation do not match.")]
    public string? ConfirmNewPassword { get; set; }
}
