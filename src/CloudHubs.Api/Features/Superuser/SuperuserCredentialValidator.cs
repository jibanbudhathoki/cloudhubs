using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Users;
using CloudHubs.Infrastructure.Persistence;

namespace CloudHubs.Api.Features.Superuser;

public sealed class SuperuserCredentialValidator(
    IConfiguration configuration,
    CloudHubsDbContext dbContext,
    IPasswordHasher<User> passwordHasher)
{
    public async Task BootstrapIfConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var configuredId = configuration["Superuser:Id"];
        var configuredPassword = configuration["Superuser:Password"];
        if (string.IsNullOrWhiteSpace(configuredId) && string.IsNullOrEmpty(configuredPassword))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(configuredId) || string.IsNullOrEmpty(configuredPassword))
        {
            throw new InvalidOperationException("Set both SUPERUSER__ID and SUPERUSER__PASSWORD to bootstrap the Superuser account.");
        }
        if (configuredId.Length > 200)
        {
            throw new InvalidOperationException("SUPERUSER__ID must be no more than 200 characters.");
        }

        var normalizedId = configuredId.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.LoginId != null && candidate.LoginId.ToLower() == normalizedId
                || candidate.Email != null && candidate.Email.ToLower() == normalizedId,
            cancellationToken);

        if (user is null || user.Role != UserRole.Superuser || string.IsNullOrEmpty(user.PasswordHash) || user.LoginId is null)
        {
            var platformSettings = await dbContext.PlatformSettings.SingleOrDefaultAsync(
                item => item.Id == PlatformSettings.SingletonId,
                cancellationToken) ?? new PlatformSettings();
            if (configuredPassword.Length < platformSettings.MinimumPasswordLength
                || platformSettings.RequirePasswordDigit && !configuredPassword.Any(char.IsAsciiDigit))
            {
                throw new InvalidOperationException(
                    $"The configured Superadmin password does not meet the platform password policy (minimum {platformSettings.MinimumPasswordLength} characters{(platformSettings.RequirePasswordDigit ? " and at least one digit" : string.Empty)}).");
            }

            var isNewUser = user is null;
            user = new User
                {
                    Name = configuration["Superuser:Name"]?.Trim() is { Length: > 0 } name ? name : "Platform Superuser",
                    Email = user?.Email,
                    LoginId = normalizedId,
                    Role = UserRole.Superuser,
                    IsActive = true
                };
            user.PasswordHash = passwordHasher.HashPassword(user, configuredPassword);
            if (isNewUser)
            {
                dbContext.Users.Add(user);
            }
            else
            {
                user.UpdatedAt = DateTime.UtcNow;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<User?> ValidateAsync(string id, string password, CancellationToken cancellationToken = default)
    {
        var normalizedId = id.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.LoginId != null && candidate.LoginId.ToLower() == normalizedId,
            cancellationToken);
        if (user is null
            || !user.IsActive
            || user.Role != UserRole.Superuser
            || string.IsNullOrEmpty(user.PasswordHash))
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task<string?> GetSuperuserEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.Role == UserRole.Superuser,
            cancellationToken);
        return user?.Email;
    }

    public async Task<bool> UpdateAccountAsync(
        Guid userId,
        string currentPassword,
        string? newEmail,
        string? newPassword,
        string? confirmNewPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.Role == UserRole.Superuser && candidate.IsActive,
            cancellationToken)
            ?? throw new InvalidOperationException("The active Superadmin account could not be found.");

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new UnauthorizedAccessException("The current password is incorrect.");
        }

        var passwordVerification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (passwordVerification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("The current password is incorrect.");
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(newEmail) ? null : newEmail.Trim();
        if (normalizedEmail is not null)
        {
            if (normalizedEmail.Length > 200 || !new EmailAddressAttribute().IsValid(normalizedEmail))
            {
                throw new ArgumentException("Enter a valid email address of 200 characters or fewer.", nameof(newEmail));
            }

            var normalizedEmailLower = normalizedEmail.ToLowerInvariant();
            var conflictsWithUser = await dbContext.Users.AnyAsync(
                candidate => candidate.Id != userId
                    && (candidate.Email != null && candidate.Email.ToLower() == normalizedEmailLower
                        || candidate.LoginId != null && candidate.LoginId.ToLower() == normalizedEmailLower),
                cancellationToken);
            if (conflictsWithUser)
            {
                throw new InvalidOperationException("That email address is already in use.");
            }
        }

        var emailChanged = normalizedEmail is not null
            && !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        var passwordChanged = !string.IsNullOrEmpty(newPassword);
        if (!emailChanged && !passwordChanged)
        {
            throw new ArgumentException("Enter a new email address, a new password, or both.");
        }

        if (passwordChanged)
        {
            if (!string.Equals(newPassword, confirmNewPassword, StringComparison.Ordinal))
            {
                throw new ArgumentException("The new password and confirmation do not match.", nameof(confirmNewPassword));
            }

            var settings = await dbContext.PlatformSettings.SingleOrDefaultAsync(
                item => item.Id == PlatformSettings.SingletonId,
                cancellationToken) ?? new PlatformSettings();
            if (newPassword!.Length < settings.MinimumPasswordLength)
            {
                throw new ArgumentException(
                    $"The new password must be at least {settings.MinimumPasswordLength} characters long.",
                    nameof(newPassword));
            }

            if (settings.RequirePasswordDigit && !newPassword.Any(char.IsAsciiDigit))
            {
                throw new ArgumentException("The new password must contain at least one digit.", nameof(newPassword));
            }

            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        }
        else if (passwordVerification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, currentPassword);
        }

        if (emailChanged)
        {
            user.Email = normalizedEmail;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return passwordChanged;
    }
}
