using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Web.Security;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IOrganizationRepository _organizationRepository;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOrganizationRepository organizationRepository)
        : base(options, logger, encoder)
    {
        _organizationRepository = organizationRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? apiKey = null;

        // 1. Check X-Api-Key header
        if (Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeaderVal))
        {
            apiKey = apiKeyHeaderVal.FirstOrDefault();
        }

        // 2. Fallback to Authorization: Bearer ch_... or ApiKey ch_...
        if (string.IsNullOrWhiteSpace(apiKey) && Request.Headers.TryGetValue("Authorization", out var authHeaderVal))
        {
            var header = authHeaderVal.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header))
            {
                if (header.StartsWith("Bearer ch_", StringComparison.OrdinalIgnoreCase))
                {
                    apiKey = header["Bearer ".Length..].Trim();
                }
                else if (header.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
                {
                    apiKey = header["ApiKey ".Length..].Trim();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var organization = await _organizationRepository.GetByApiKeyAsync(apiKey, Context.RequestAborted);
        if (organization is null)
        {
            return AuthenticateResult.Fail("Invalid Organization API Key.");
        }

        if (!organization.IsAvailableForUse())
        {
            return AuthenticateResult.Fail("Organization is not active or available.");
        }

        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        if (plan is null || !plan.HasApiAccess)
        {
            return AuthenticateResult.Fail("API access requires a Pro or Custom subscription plan.");
        }

        // Enforce route organizationId matching if present in route
        if (Context.Request.RouteValues.TryGetValue("organizationId", out var routeOrgIdVal)
            && Guid.TryParse(routeOrgIdVal?.ToString(), out var routeOrgId)
            && routeOrgId != organization.Id)
        {
            return AuthenticateResult.Fail("API key does not belong to the requested organization.");
        }

        var owner = organization.Members.FirstOrDefault(m => m.Role == OrganizationMemberRole.Owner)
            ?? organization.Members.FirstOrDefault();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner?.UserId.ToString() ?? Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, owner?.User.Name ?? organization.Name),
            new(ClaimTypes.Email, organization.Email),
            new(ClaimTypes.Role, "OrganizationMember"),
            new("OrganizationId", organization.Id.ToString()),
            new("ApiKeyAuth", "true"),
            new("PlanName", plan.Name)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
