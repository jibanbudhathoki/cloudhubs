using CloudHubs.Domain.Enums;

namespace CloudHubs.Application.Modules.Organizations.DTOs;

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
