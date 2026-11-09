namespace CloudHubs.Application.Modules.Users.Interfaces;

public interface IPasswordResetEmailSender
{
    Task<bool> SendPasswordResetEmailAsync(
        string recipientEmail,
        string recipientName,
        string resetLink,
        string? memberId = null,
        CancellationToken cancellationToken = default);
}

