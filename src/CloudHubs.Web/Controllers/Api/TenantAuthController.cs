using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.Web.Security;
using CloudHubs.Web.ViewModels.Auth;

namespace CloudHubs.Web.Controllers.Api;

[ApiController]
[Route("api/tenant-auth")]
[EnableRateLimiting("tenant-login")]
public class TenantAuthController(
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    IOrganizationRoleRepository roleRepository,
    TenantTokenIssuer tokenIssuer,
    ILogger<TenantAuthController> logger) : ControllerBase
{
    [HttpPost("token")]
    public async Task<IActionResult> CreateToken([FromBody] TenantTokenRequest request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null
            || !user.IsActive
            || user.Role == UserRole.Superuser
            || string.IsNullOrWhiteSpace(user.PasswordHash)
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Email or password is incorrect."
            });
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(user.Id, cancellationToken);
        var availableOrganizations = memberships
            .Where(membership => membership.Organization.IsAvailableForUse())
            .Select(membership => new TenantOrganizationMembershipResponse(
                membership.OrganizationId,
                membership.Organization.Name,
                membership.Role.ToString()))
            .ToArray();
        if (availableOrganizations.Length == 0)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "This account has no active organization membership."
            });
        }

        try
        {
            var (accessToken, expiresAt) = await tokenIssuer.IssueAsync(user, cancellationToken);
            return Ok(new TenantTokenResponse(accessToken, "Bearer", expiresAt, availableOrganizations));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Tenant JWT signing is unavailable.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authentication is temporarily unavailable.");
        }
    }
    }
