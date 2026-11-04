using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Application.Modules.Organizations.DTOs;

public sealed class UpdateOrganizationMemberRoleRequest
{
    [Required]
    [RegularExpression("^(Admin|Developer|Viewer)$")]
    public string Role { get; set; } = "Developer";

    public Guid? CustomRoleId { get; set; }
}
