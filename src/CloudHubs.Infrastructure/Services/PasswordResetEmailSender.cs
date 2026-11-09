using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.Application.Modules.Users.Interfaces;

namespace CloudHubs.Infrastructure.Services;

public sealed class PasswordResetEmailSender(
    IConfiguration configuration,
    PlatformSettingsService platformSettingsService,
    ILogger<PasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    public async Task<bool> SendPasswordResetEmailAsync(
        string recipientEmail,
        string recipientName,
        string resetLink,
        string? memberId = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettingsService.GetAsync(cancellationToken);
        var host = configuration["Email:Smtp:Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "[Password Reset] SMTP not configured. Reset link for {Email} ({Name}): {ResetLink}",
                recipientEmail, recipientName, resetLink);
            return true;
        }

        try
        {
            var port = configuration.GetValue("Email:Smtp:Port", 587);
            var username = configuration["Email:Smtp:Username"];
            var password = configuration["Email:Smtp:Password"];
            var fromAddress = configuration["Email:Smtp:FromAddress"];

            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                fromAddress = string.IsNullOrWhiteSpace(settings.SupportEmail)
                    ? "noreply@cloudhubs.local"
                    : settings.SupportEmail;
            }

            var from = new MailAddress(fromAddress, settings.PlatformName);
            var to = new MailAddress(recipientEmail, recipientName);

            using var message = new MailMessage(from, to)
            {
                Subject = $"Reset your {settings.PlatformName} password",
                IsBodyHtml = true,
                Body = $$"""
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="utf-8" />
                        <style>
                            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; color: #333; line-height: 1.6; padding: 20px; }
                            .container { max-width: 560px; margin: 0 auto; background: #ffffff; border: 1px solid #e1e4e8; border-radius: 8px; padding: 32px; }
                            .brand { font-size: 20px; font-weight: bold; color: #1f795f; margin-bottom: 24px; }
                            .button { display: inline-block; padding: 12px 24px; background-color: #1f795f; color: #ffffff !important; text-decoration: none; border-radius: 6px; font-weight: 600; margin: 20px 0; }
                            .footer { font-size: 12px; color: #6a737d; margin-top: 32px; border-top: 1px solid #eaecef; padding-top: 16px; }
                        </style>
                    </head>
                    <body>
                        <div class="container">
                            <div class="brand">{{settings.PlatformName}}</div>
                            <h2>Reset Your Password</h2>
                            <p>Hello {{WebUtility.HtmlEncode(recipientName)}},</p>
                            <p>We received a request to reset the password for your {{settings.PlatformName}} account.</p>
                            <div style="background: #f6f8fa; padding: 12px 16px; border-radius: 6px; margin: 16px 0; border: 1px solid #d0d7de; font-size: 13px;">
                                <p style="margin: 0 0 6px;"><strong>Member ID:</strong> <code style="background: #eaecef; padding: 2px 6px; border-radius: 3px;">{{WebUtility.HtmlEncode(memberId ?? "N/A")}}</code></p>
                                <p style="margin: 0;"><strong>Registered Email:</strong> {{WebUtility.HtmlEncode(recipientEmail)}}</p>
                            </div>
                            <p>Click the button below to set a new password. This link is valid for 2 hours.</p>
                            <p><a href="{{WebUtility.HtmlEncode(resetLink)}}" class="button">Reset Password</a></p>
                            <p>If the button doesn't work, copy and paste this link into your browser:</p>
                            <p><small style="word-break: break-all; color: #0366d6;">{{WebUtility.HtmlEncode(resetLink)}}</small></p>
                            <div class="footer">
                                <p>If you did not request a password reset, you can safely ignore this email. Your password will remain unchanged.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                    """
            };

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = configuration.GetValue("Email:Smtp:EnableSsl", true)
            };

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrEmpty(password))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Password reset email sent to {Email}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to dispatch password reset email to {Email}", recipientEmail);
            return false;
        }
    }
}
