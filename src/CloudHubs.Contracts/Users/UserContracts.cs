namespace CloudHubs.Contracts.Users;

public record UserSummaryDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);