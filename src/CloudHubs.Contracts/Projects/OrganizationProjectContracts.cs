using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Contracts.Projects;

public sealed class CreateOrganizationProjectRequest
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [RegularExpression("^[a-zA-Z0-9_-]+$")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string RootPath { get; set; } = string.Empty;

    [StringLength(150)]
    public string TemplateId { get; set; } = "default";
}

public sealed class AddOrganizationMemberRequest
{
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Admin|Developer|Viewer)$")]
    public string Role { get; set; } = "Developer";
}

public sealed class UpdateOrganizationMemberRoleRequest
{
    [Required]
    [RegularExpression("^(Admin|Developer|Viewer)$")]
    public string Role { get; set; } = "Developer";

    public Guid? CustomRoleId { get; set; }
}

public sealed record OrganizationMemberResponse(
    Guid UserId,
    string? MemberId,
    string Name,
    string? Email,
    string Role,
    string? CustomRole);

public sealed record OrganizationProjectResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string TemplateId,
    bool IsActive,
    DateTime CreatedAt);
