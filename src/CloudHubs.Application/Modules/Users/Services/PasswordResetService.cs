using System.Security.Cryptography;
using System.Text;
using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.Application.Modules.Users.DTOs;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Application.Modules.Users.Services;

public sealed class PasswordResetService(
    IUserRepository userRepository,
    IPasswordHashingService passwordHashingService,
    PlatformSettingsService platformSettingsService,
    IPasswordResetEmailSender emailSender) : IPasswordResetService
{
    private const string GenericConfirmationMessage =
        "If an account matching that email or member ID exists, we have sent instructions to reset your password.";

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(
        string identifier,
        string callbackBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return new PasswordResetRequestResult(false, "Please enter your email or member ID.");
        }

        var normalized = identifier.Trim();
        var user = await userRepository.GetByEmailAsync(normalized, cancellationToken)
            ?? await userRepository.GetByLoginIdAsync(normalized, cancellationToken);

        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            return new PasswordResetRequestResult(true, GenericConfirmationMessage);
        }

        // Generate high-entropy 32-byte cryptographic token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var tokenHash = HashToken(rawToken);

        user.PasswordResetTokenHash = tokenHash;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(2);
        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user, cancellationToken);

        var resetUrl = $"{callbackBaseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(rawToken)}";

        try
        {
            await emailSender.SendPasswordResetEmailAsync(user.Email, user.Name, resetUrl, user.LoginId, cancellationToken);
        }
        catch
        {
            // Suppress error so response remains safe and consistent
        }

        return new PasswordResetRequestResult(
            Success: true,
            Message: GenericConfirmationMessage,
            ResetLink: resetUrl,
            UserEmail: user.Email);
    }

    public async Task<bool> ValidateResetTokenAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var user = await userRepository.GetByEmailAsync(email.Trim(), cancellationToken);
        if (user is null
            || !user.IsActive
            || string.IsNullOrWhiteSpace(user.PasswordResetTokenHash)
            || !user.PasswordResetTokenExpiresAt.HasValue
            || user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
        {
            return false;
        }

        var computedHash = HashToken(token.Trim());
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(user.PasswordResetTokenHash),
            Encoding.UTF8.GetBytes(computedHash));
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return new PasswordResetResult(false, "The password reset link is invalid or incomplete.");
        }

        if (string.IsNullOrEmpty(newPassword))
        {
            return new PasswordResetResult(false, "Please enter a new password.");
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return new PasswordResetResult(false, "Passwords do not match.");
        }

        var user = await userRepository.GetByEmailAsync(email.Trim(), cancellationToken);
        if (user is null
            || !user.IsActive
            || string.IsNullOrWhiteSpace(user.PasswordResetTokenHash)
            || !user.PasswordResetTokenExpiresAt.HasValue
            || user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
        {
            return new PasswordResetResult(false, "This password reset link has expired or is invalid. Please request a new one.");
        }

        var computedHash = HashToken(token.Trim());
        var isMatch = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(user.PasswordResetTokenHash),
            Encoding.UTF8.GetBytes(computedHash));

        if (!isMatch)
        {
            return new PasswordResetResult(false, "This password reset link has expired or is invalid. Please request a new one.");
        }

        var settings = await platformSettingsService.GetAsync(cancellationToken);
        if (newPassword.Length < settings.MinimumPasswordLength)
        {
            return new PasswordResetResult(false, $"Password must be at least {settings.MinimumPasswordLength} characters long.");
        }

        if (settings.RequirePasswordDigit && !newPassword.Any(char.IsAsciiDigit))
        {
            return new PasswordResetResult(false, "Password must contain at least one digit.");
        }

        if (newPassword.Length > 128)
        {
            return new PasswordResetResult(false, "Password must be 128 characters or fewer.");
        }

        user.PasswordHash = passwordHashingService.HashPassword(user, newPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        user.MustChangePasswordOnNextLogin = false;
        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user, cancellationToken);

        return new PasswordResetResult(true);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
