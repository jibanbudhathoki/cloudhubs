using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Entities;
using CloudHubs.Web.ViewModels.Organizations;

namespace CloudHubs.Web.ViewModels.Docs;

public sealed class OrganizationDocsViewModel
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string OrganizationSlug { get; set; } = string.Empty;
    public string OrganizationRole { get; set; } = string.Empty;
    public string? OrganizationApiKey { get; set; }
    public string? OrganizationBaseApiUrl { get; set; }
    public string SubscriptionPlanName { get; set; } = "Basic";
    public bool HasApiAccess { get; set; }
    public bool IsProOrCustomPlan { get; set; }
    public bool HasPendingApiRequest { get; set; }
    public string? ActiveTab { get; set; } = "docs";

    public IReadOnlyCollection<OrganizationDoc> Docs { get; set; } = [];
    public OrganizationDoc? SelectedDoc { get; set; }
    public IReadOnlyCollection<Project> Projects { get; set; } = [];

    public bool CanManageDocs { get; set; }
    public bool CanViewProjects { get; set; } = true;
    public bool CanViewMembers { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;

    public IReadOnlyCollection<OrganizationPortalMembershipViewModel> Organizations { get; set; } = [];
    public OrganizationPortalCurrentOrganizationViewModel? CurrentOrganization { get; set; }
    public bool CanShowOrganizationAction { get; set; }
}

public sealed class OrganizationDocFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Slug { get; set; }

    [StringLength(100)]
    public string Category { get; set; } = "API";

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? BaseUrl { get; set; }

    [StringLength(500)]
    public string? ApiKey { get; set; }

    [StringLength(500)]
    public string? TestApiKey { get; set; }

    [StringLength(500)]
    public string? SecretKey { get; set; }

    public string? EnvironmentVariables { get; set; }

    public string? PostmanCollection { get; set; }

    public string? Content { get; set; }

    public Guid? ProjectId { get; set; }

    public bool IsPublicToMembers { get; set; } = true;
    public List<Guid> AllowedMemberUserIds { get; set; } = [];
}

public sealed class OrganizationDocEditorViewModel
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string OrganizationSlug { get; set; } = string.Empty;
    public string OrganizationRole { get; set; } = string.Empty;
    public string? OrganizationApiKey { get; set; }
    public string SubscriptionPlanName { get; set; } = "Basic";
    public bool HasApiAccess { get; set; }

    public bool IsEdit { get; set; }
    public Guid? DocId { get; set; }
    public OrganizationDocFormModel Form { get; set; } = new();
    public IReadOnlyCollection<Project> Projects { get; set; } = [];
    public IReadOnlyCollection<OrganizationMembership> OrganizationMembers { get; set; } = [];

    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public bool CanViewProjects { get; set; } = true;
    public bool CanViewMembers { get; set; }

    public IReadOnlyCollection<OrganizationPortalMembershipViewModel> Organizations { get; set; } = [];
    public OrganizationPortalCurrentOrganizationViewModel? CurrentOrganization { get; set; }
    public bool CanShowOrganizationAction { get; set; }
}

