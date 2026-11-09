using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Organizations;

public sealed class SubscriptionPlanTierViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "USD";
    public int MaxProjects { get; init; }
    public int MaxMembers { get; init; }
    public int StorageLimitGb { get; init; }
    public bool HasApiAccess { get; init; }
    public bool HasSingleSignOn { get; init; }
    public bool HasIntegrations { get; init; }
    public bool HasCustomRoles { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class OrganizationAccountViewModel
{
    // Sidebar & Navigation Context
    public string UserName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public string MemberId { get; init; } = string.Empty;
    public IReadOnlyCollection<OrganizationPortalMembershipViewModel> Organizations { get; init; } = [];
    public OrganizationPortalCurrentOrganizationViewModel? CurrentOrganization { get; init; }
    public bool CanViewProjects { get; init; }
    public bool CanViewMembers { get; init; }
    public bool CanManageOrganization { get; init; }
    public bool CanShowOrganizationAction { get; init; }
    public bool HasAccountSubscription { get; init; }
    public int AccountOrganizationCount { get; init; }
    public int MaxOrganizations { get; init; }

    // Admin User Profile Form
    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(200)]
    [Display(Name = "Full Name")]
    public string ProfileName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(200)]
    [Display(Name = "Account Email")]
    public string ProfileEmail { get; set; } = string.Empty;

    // Password Change Form
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "New passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmNewPassword { get; set; } = string.Empty;

    // Organization Profile Form
    [Required(ErrorMessage = "Organization name is required.")]
    [StringLength(200)]
    [Display(Name = "Organization Name")]
    public string OrganizationName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid organization email address.")]
    [StringLength(200)]
    [Display(Name = "Organization Email")]
    public string OrganizationEmail { get; set; } = string.Empty;

    public string OrganizationSlug { get; set; } = string.Empty;
    public string? OrganizationApiKey { get; set; }
    public string? OrganizationBaseApiUrl { get; set; }
    public string OrganizationStatus { get; set; } = string.Empty;
    public string SubscriptionPlan { get; set; } = string.Empty;
    public bool HasApiAccess { get; set; }
    public bool IsProOrCustomPlan { get; set; }
    public string ActiveTab { get; set; } = "workspace";
    public string? SelectedCategory { get; set; }
    public bool IsCategoryDetailPage => !string.IsNullOrWhiteSpace(SelectedCategory);
    public DateTime? SubscriptionExpiresAt { get; set; }
    public string UserRole { get; set; } = string.Empty;

    // Available Packages / Plans
    public IReadOnlyList<SubscriptionPlanTierViewModel> AvailablePlans { get; set; } = [];
}
