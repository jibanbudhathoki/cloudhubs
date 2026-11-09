using System.Security.Cryptography;
using System.Text;
using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Application.Modules.Users.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Users;

public class PasswordResetServiceTests
{
    private sealed class EmailSenderStub : IPasswordResetEmailSender
    {
        public string? SentEmail { get; private set; }
        public string? SentLink { get; private set; }
        public string? SentMemberId { get; private set; }

        public Task<bool> SendPasswordResetEmailAsync(
            string recipientEmail,
            string recipientName,
            string resetLink,
            string? memberId = null,
            CancellationToken cancellationToken = default)
        {
            SentEmail = recipientEmail;
            SentLink = resetLink;
            SentMemberId = memberId;
            return Task.FromResult(true);
        }
    }

    private sealed class PasswordHashingStub : IPasswordHashingService
    {
        public string HashPassword(User user, string password) => "hashed_" + password;
        public bool VerifyPassword(User user, string hashedPassword, string providedPassword) =>
            hashedPassword == "hashed_" + providedPassword;
    }

    [Fact]
    public async Task RequestPasswordResetAsync_EmptyIdentifier_ReturnsFailure()
    {
        var userRepo = new UserRepositoryStub();
        var emailSender = new EmailSenderStub();
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub()),
            emailSender);

        var result = await service.RequestPasswordResetAsync("", "http://localhost:8000");

        Assert.False(result.Success);
        Assert.Null(emailSender.SentEmail);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_NonExistentUser_ReturnsGenericSuccessWithoutEmail()
    {
        var userRepo = new UserRepositoryStub();
        var emailSender = new EmailSenderStub();
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub()),
            emailSender);

        var result = await service.RequestPasswordResetAsync("nobody@example.com", "http://localhost:8000");

        Assert.True(result.Success);
        Assert.Contains("If an account matching", result.Message);
        Assert.Null(emailSender.SentEmail);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_ValidUser_SetsTokenAndSendsEmail()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "John Doe",
            Email = "john@example.com",
            LoginId = "ID12345",
            IsActive = true
        };
        var userRepo = new UserRepositoryStub { ExistingUser = user };
        var emailSender = new EmailSenderStub();
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub()),
            emailSender);

        var result = await service.RequestPasswordResetAsync("john@example.com", "http://localhost:8000");

        Assert.True(result.Success);
        Assert.NotNull(user.PasswordResetTokenHash);
        Assert.True(user.PasswordResetTokenExpiresAt > DateTime.UtcNow);
        Assert.Equal("john@example.com", emailSender.SentEmail);
        Assert.Equal("ID12345", emailSender.SentMemberId);
        Assert.NotNull(emailSender.SentLink);
        Assert.Contains("http://localhost:8000/reset-password", emailSender.SentLink);
        Assert.Contains("john%40example.com", emailSender.SentLink);
    }

    [Fact]
    public async Task ValidateResetTokenAsync_ValidToken_ReturnsTrue()
    {
        var rawToken = "my-secret-test-token-123456789";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            IsActive = true,
            PasswordResetTokenHash = tokenHash,
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var userRepo = new UserRepositoryStub { ExistingUser = user };
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub()),
            new EmailSenderStub());

        var isValid = await service.ValidateResetTokenAsync("alice@example.com", rawToken);
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateResetTokenAsync_ExpiredToken_ReturnsFalse()
    {
        var rawToken = "my-secret-test-token-123456789";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            IsActive = true,
            PasswordResetTokenHash = tokenHash,
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expired
        };
        var userRepo = new UserRepositoryStub { ExistingUser = user };
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub()),
            new EmailSenderStub());

        var isValid = await service.ValidateResetTokenAsync("alice@example.com", rawToken);
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateResetTokenAsync_WrongToken_ReturnsFalse()
    {
        var rawToken = "correct-token";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            IsActive = true,
            PasswordResetTokenHash = tokenHash,
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var userRepo = new UserRepositoryStub { ExistingUser = user };
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub()),
            new EmailSenderStub());

        var isValid = await service.ValidateResetTokenAsync("alice@example.com", "wrong-token");
        Assert.False(isValid);
    }

    [Fact]
    public async Task ResetPasswordAsync_MismatchedPasswords_ReturnsError()
    {
        var userRepo = new UserRepositoryStub();
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub()),
            new EmailSenderStub());

        var result = await service.ResetPasswordAsync("test@example.com", "token", "Password123", "Mismatch456");

        Assert.False(result.Success);
        Assert.Equal("Passwords do not match.", result.ErrorMessage);
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_FailsPolicy()
    {
        var rawToken = "secret-token";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "bob@example.com",
            IsActive = true,
            PasswordResetTokenHash = tokenHash,
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var userRepo = new UserRepositoryStub { ExistingUser = user };
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub
            {
                Settings = new PlatformSettings { MinimumPasswordLength = 8, RequirePasswordDigit = true }
            }),
            new EmailSenderStub());

        // Missing digit
        var resultNoDigit = await service.ResetPasswordAsync("bob@example.com", rawToken, "Abcdefgh", "Abcdefgh");
        Assert.False(resultNoDigit.Success);
        Assert.Contains("digit", resultNoDigit.ErrorMessage);

        // Too short
        var resultTooShort = await service.ResetPasswordAsync("bob@example.com", rawToken, "Ab1", "Ab1");
        Assert.False(resultTooShort.Success);
        Assert.Contains("at least 8 characters", resultTooShort.ErrorMessage);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidInput_UpdatesHashAndClearsToken()
    {
        var rawToken = "secret-token";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "bob@example.com",
            IsActive = true,
            PasswordResetTokenHash = tokenHash,
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            MustChangePasswordOnNextLogin = true
        };
        var userRepo = new UserRepositoryStub { ExistingUser = user };
        var service = new PasswordResetService(
            userRepo,
            new PasswordHashingStub(),
            new PlatformSettingsService(new PlatformSettingsRepositoryStub
            {
                Settings = new PlatformSettings { MinimumPasswordLength = 8, RequirePasswordDigit = true }
            }),
            new EmailSenderStub());

        var result = await service.ResetPasswordAsync("bob@example.com", rawToken, "SecurePass123", "SecurePass123");

        Assert.True(result.Success);
        Assert.Equal("hashed_SecurePass123", user.PasswordHash);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Null(user.PasswordResetTokenExpiresAt);
        Assert.False(user.MustChangePasswordOnNextLogin);
    }
}

