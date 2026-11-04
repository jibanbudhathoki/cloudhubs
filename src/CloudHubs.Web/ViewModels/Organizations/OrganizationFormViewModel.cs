using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Organizations;

public class OrganizationFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "Use letters, numbers, hyphens, or underscores.")]
    public string Slug { get; set; } = string.Empty;

    public Guid? SubscriptionPlanId { get; set; }

    [Required]
    public string Status { get; set; } = "Active";

    public string AdminMode { get; set; } = "existing";

    public Guid? AdminUserId { get; set; }

    [StringLength(200)]
    public string? AdminName { get; set; }

    [EmailAddress, StringLength(200)]
    public string? AdminEmail { get; set; }

    [DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string? AdminPassword { get; set; }

    public List<OrganizationMemberFormViewModel> Members { get; set; } = [];
}
