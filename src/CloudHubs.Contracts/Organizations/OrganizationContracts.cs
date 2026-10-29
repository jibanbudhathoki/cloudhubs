namespace CloudHubs.Contracts.Organizations;

public record CreateOrganizationRequest(
    string Name,
    string Email,
    string Slug,
    string OwnerName,
    string OwnerEmail);

public record OrganizationSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Email,
    string Status,
    string? SubscriptionStatus,
    DateTime CreatedAt,
    string? OwnerName = null,
    string? OwnerEmail = null,
    int MemberCount = 0,
    string? PlanName = null);

public record OrganizationDetailsDto(
    Guid Id,
    string Name,
    string Slug,
    string Email,
    string Status,
    string SubscriptionStatus,
    DateTime CreatedAt,
    string? BaseApiUrl,
    string? SubscriptionPlanName = null,
    OrganizationSubscriptionFeaturesDto? SubscriptionFeatures = null);

public record OrganizationSubscriptionFeaturesDto(
    string Name,
    string Currency,
    decimal Price,
    string BillingInterval,
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
