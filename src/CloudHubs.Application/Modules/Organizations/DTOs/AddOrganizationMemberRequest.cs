using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Application.Modules.Organizations.DTOs;

public sealed class AddOrganizationMemberRequest
{
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Admin|Developer|Viewer)$")]
    public string Role { get; set; } = "Developer";
}
