namespace CloudHubs.Domain.Organizations;

[Flags]
public enum OrganizationPermission
{
    None = 0,
    ViewProjects = 1 << 0,
    ManageProjects = 1 << 1,
    ViewMembers = 1 << 2,
    ManageMembers = 1 << 3,
    ManageSettings = 1 << 4,
    ManageIntegrations = 1 << 5,
    ViewAuditLog = 1 << 6,
    ManageBilling = 1 << 7,
    All = ViewProjects | ManageProjects | ViewMembers | ManageMembers |
          ManageSettings | ManageIntegrations | ViewAuditLog | ManageBilling
}
