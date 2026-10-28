using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MyDevPlatform.Application.Organizations;

namespace MyDevPlatform.Api.Features.Superuser;

[ApiController]
[Route("api/superuser")]
public sealed class SuperuserTokenController(
    IConfiguration configuration,
    SuperuserCredentialValidator credentialValidator,
    PlatformSettingsService platformSettingsService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> CreateToken([FromBody] SuperuserTokenRequest request, CancellationToken cancellationToken)
    {
        var signingKey = configuration["Superuser:Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "JWT signing is not configured",
                detail: "Set SUPERUSER__JWT__SIGNINGKEY to a secret of at least 32 UTF-8 bytes.");
        }

        var user = await credentialValidator.ValidateAsync(request.Id, request.Password, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Login ID or password is incorrect."
            });
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
            new Claim(ClaimTypes.Name, user.LoginId ?? user.Email ?? user.Id.ToString()),
            new Claim(ClaimTypes.Role, "Superuser")
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, now, expiresAt, credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new SuperuserTokenResponse(accessToken, "Bearer", expiresAt));
    }
}

public sealed class SuperuserTokenRequest
{
    [Required, StringLength(200)]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record SuperuserTokenResponse(string AccessToken, string TokenType, DateTime ExpiresAt);
