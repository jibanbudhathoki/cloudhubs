namespace CloudHubs.Application.Modules.Users.DTOs;

public record PasswordResetRequestResult(
    bool Success,
    string Message,
    string? ResetLink = null,
    string? UserEmail = null);

public record PasswordResetResult(
    bool Success,
    string? ErrorMessage = null);

