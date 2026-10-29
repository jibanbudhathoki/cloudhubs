using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CloudHubs.Application.Organizations;
using CloudHubs.Domain.Users;

namespace CloudHubs.Api.Features.Organizations;

public class TenantTokenIssuer(IConfiguration configuration, PlatformSettingsService platformSettingsService)
{
    public async Task<(string AccessToken, DateTime ExpiresAt)> IssueAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var signingKey = configuration["Superuser:Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("JWT signing is not configured.");
        }

        var issuer = configuration["Superuser:Jwt:Issuer"] ?? "CloudHubs";
        var audience = configuration["Superuser:Jwt:Audience"] ?? "CloudHubs.Api";
        var now = DateTime.UtcNow;
        var settings = await platformSettingsService.GetAsync(cancellationToken);
        var expiresAt = now.AddMinutes(settings.JwtLifetimeMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email ?? user.LoginId ?? user.Id.ToString()),
            new Claim(ClaimTypes.Role, "OrganizationMember")
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, now, expiresAt, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
