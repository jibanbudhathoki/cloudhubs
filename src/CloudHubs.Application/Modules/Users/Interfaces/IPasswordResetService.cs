using CloudHubs.Application.Modules.Users.DTOs;

namespace CloudHubs.Application.Modules.Users.Interfaces;

public interface IPasswordResetService
{
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(
        string identifier,
        string callbackBaseUrl,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateResetTokenAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default);
}

