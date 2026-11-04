using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Web.ViewModels.Organizations;

public class OrganizationMemberFormViewModel
{
    public string Mode { get; set; } = "existing";

    public Guid? UserId { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string? Password { get; set; }

    public OrganizationMemberRole Role { get; set; } = OrganizationMemberRole.Developer;
}
