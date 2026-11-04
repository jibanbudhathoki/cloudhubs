using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Organizations;

public sealed class OrganizationPortalMemberFormModel
{
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Admin|Developer|Viewer)$")]
    public string Role { get; set; } = "Developer";

    [Required, StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    public Guid OrganizationId { get; set; }
}
