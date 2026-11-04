namespace CloudHubs.Application.Modules.Organizations.DTOs;

public record OrganizationSubscriptionFeaturesDto(
    string Name,
    string Currency,
    decimal Price,
    string BillingInterval,
    int MaxOrganizations,
    int MaxProjects,
    int MaxMembers,
    int StorageLimitGb,
    bool HasApiAccess,
    bool HasCustomDomains,
    bool HasAdvancedAnalytics,
    bool HasPrioritySupport,
    bool HasSingleSignOn,
    bool HasAuditLogs,
    bool HasAutomatedBackups,
    bool HasIntegrations,
    bool HasCustomRoles,
    int SupportResponseHours);
