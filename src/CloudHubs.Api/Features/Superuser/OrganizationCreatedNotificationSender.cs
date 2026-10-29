using System.Net;
using System.Net.Mail;
using CloudHubs.Application.Organizations;
using CloudHubs.Contracts.Organizations;

namespace CloudHubs.Api.Features.Superuser;

public sealed class OrganizationCreatedNotificationSender(
    IConfiguration configuration,
    PlatformSettingsService platformSettingsService)
{
    public async Task<bool> SendIfEnabledAsync(
        OrganizationDetailsDto organization,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettingsService.GetAsync(cancellationToken);
        if (!settings.NotifyOnOrganizationCreated)
        {
            return false;
        }

        var host = configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Set EMAIL__SMTP__HOST before enabling organization-created email notifications.");
        }

        var port = configuration.GetValue("Email:Smtp:Port", 587);
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException("EMAIL__SMTP__PORT must be between 1 and 65535.");
        }

        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("Set both EMAIL__SMTP__USERNAME and EMAIL__SMTP__PASSWORD, or leave both unset.");
        }

        var fromAddress = configuration["Email:Smtp:FromAddress"];
        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            fromAddress = settings.SupportEmail;
        }

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("Set EMAIL__SMTP__FROMADDRESS or configure the platform support email.");
        }

        var from = new MailAddress(fromAddress, settings.PlatformName);
        var recipient = new MailAddress(settings.NotificationEmail);
        using var message = new MailMessage(from, recipient)
        {
            Subject = $"Organization created: {organization.Name}",
            Body = $"""
                A new organization was created on {settings.PlatformName}.

                Name: {organization.Name}
                Slug: {organization.Slug}
                Contact email: {organization.Email}
                Status: {organization.Status}
                """,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Email:Smtp:EnableSsl", true),
            Timeout = 15000
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message, cancellationToken);
        return true;
    }
}
