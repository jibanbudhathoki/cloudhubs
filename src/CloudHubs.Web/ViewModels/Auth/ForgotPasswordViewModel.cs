using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Auth;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Please enter your email or member ID.")]
    [StringLength(200)]
    [Display(Name = "Email or Member ID")]
    public string Identifier { get; set; } = string.Empty;

    public bool IsSubmitted { get; set; }

    public string? ConfirmationMessage { get; set; }

    public string? DevResetLink { get; set; }
}

