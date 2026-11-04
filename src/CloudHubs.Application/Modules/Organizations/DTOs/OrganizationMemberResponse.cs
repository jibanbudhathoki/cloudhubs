namespace CloudHubs.Application.Modules.Organizations.DTOs;

public sealed record OrganizationMemberResponse(
    Guid UserId,
    string? MemberId,
    string Name,
    string? Email,
    string Role,
    string? CustomRole);
