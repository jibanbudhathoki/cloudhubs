using CloudHubs.Domain.Enums;

namespace CloudHubs.Application.Modules.Organizations.DTOs;

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
