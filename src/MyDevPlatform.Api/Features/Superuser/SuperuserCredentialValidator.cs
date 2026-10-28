using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyDevPlatform.Domain.Organizations;
using MyDevPlatform.Domain.Users;
using MyDevPlatform.Infrastructure.Persistence;

namespace MyDevPlatform.Api.Features.Superuser;

public sealed class SuperuserCredentialValidator(
    IConfiguration configuration,
    MyDevPlatformDbContext dbContext,
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
}
