using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Web.ViewModels.Organizations;

public class OrganizationRoleFormViewModel
{
    public Guid OrganizationId { get; set; }
    public Guid? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool ViewProjects { get; set; }
    public bool ManageProjects { get; set; }
    public bool ViewMembers { get; set; }
    public bool ManageMembers { get; set; }
    public bool ManageSettings { get; set; }
    public bool ManageIntegrations { get; set; }
    public bool ViewAuditLog { get; set; }
    public bool ManageBilling { get; set; }

    public OrganizationPermission ToPermissions()
    {
        var permissions = OrganizationPermission.None;
        if (ViewProjects) permissions |= OrganizationPermission.ViewProjects;
        if (ManageProjects) permissions |= OrganizationPermission.ManageProjects;
        if (ViewMembers) permissions |= OrganizationPermission.ViewMembers;
        if (ManageMembers) permissions |= OrganizationPermission.ManageMembers;
        if (ManageSettings) permissions |= OrganizationPermission.ManageSettings;
        if (ManageIntegrations) permissions |= OrganizationPermission.ManageIntegrations;
        if (ViewAuditLog) permissions |= OrganizationPermission.ViewAuditLog;
        if (ManageBilling) permissions |= OrganizationPermission.ManageBilling;
        return permissions;
    }

    public static OrganizationRoleFormViewModel From(OrganizationRole role) => new()
    {
        OrganizationId = role.OrganizationId,
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        ViewProjects = role.Permissions.HasFlag(OrganizationPermission.ViewProjects),
        ManageProjects = role.Permissions.HasFlag(OrganizationPermission.ManageProjects),
        ViewMembers = role.Permissions.HasFlag(OrganizationPermission.ViewMembers),
        ManageMembers = role.Permissions.HasFlag(OrganizationPermission.ManageMembers),
        ManageSettings = role.Permissions.HasFlag(OrganizationPermission.ManageSettings),
        ManageIntegrations = role.Permissions.HasFlag(OrganizationPermission.ManageIntegrations),
        ViewAuditLog = role.Permissions.HasFlag(OrganizationPermission.ViewAuditLog),
        ManageBilling = role.Permissions.HasFlag(OrganizationPermission.ManageBilling)
    };
}
