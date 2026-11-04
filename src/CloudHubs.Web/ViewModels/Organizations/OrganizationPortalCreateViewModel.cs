using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Organizations;

public sealed class OrganizationPortalCreateViewModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string ContactEmail { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public int OrganizationCount { get; set; }
    public int MaxOrganizations { get; set; }
    public bool CanCreateOrganization { get; set; }
}
