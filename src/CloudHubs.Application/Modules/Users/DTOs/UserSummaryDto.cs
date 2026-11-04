namespace CloudHubs.Application.Modules.Users.DTOs;

public record UserSummaryDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);
