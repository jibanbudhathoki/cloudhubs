using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CloudHubs.Application.Organizations;
using CloudHubs.Application.Users;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Users;

namespace CloudHubs.Api.Features.Organizations;

public static class TenantCookieDefaults
{
    public const string AuthenticationScheme = "TenantCookie";
}

[Route("organization")]
[AutoValidateAntiforgeryToken]
public class OrganizationPortalController(
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    IOrganizationRoleRepository roleRepository,
    ILogger<OrganizationPortalController> logger) : Controller
{
    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login(Guid? oId)
    {
        return View("~/Views/organization/Login.cshtml", new OrganizationLoginViewModel
        {
            OrganizationContext = oId?.ToString()
        });
    }

    [HttpPost("/login")]
    [AllowAnonymous]
    [EnableRateLimiting("tenant-login")]
    public async Task<IActionResult> Login(
        OrganizationLoginViewModel model,
        [FromForm(Name = "oId")] string? organizationIdValue,
        CancellationToken cancellationToken)
    {
        model.OrganizationContext = organizationIdValue;
        Guid? organizationId = null;
        if (!string.IsNullOrWhiteSpace(organizationIdValue))
        {
            if (!Guid.TryParse(organizationIdValue, out var parsedOrganizationId))
            {
                ModelState.AddModelError(string.Empty, "The organization sign-in link is invalid. Open the link provided by your organization.");
            }
            else
            {
                organizationId = parsedOrganizationId;
            }
        }

        if (!ModelState.IsValid)
        {
            return View("~/Views/organization/Login.cshtml", model);
        }

        var user = await userRepository.GetByEmailAsync(model.Identifier, cancellationToken)
            ?? await userRepository.GetByLoginIdAsync(model.Identifier, cancellationToken);
        if (user is null
            || !user.IsActive
            || user.Role == UserRole.Superuser
            || string.IsNullOrWhiteSpace(user.PasswordHash)
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Email or member ID and password are incorrect, or this account has no active organization.");
            model.Password = string.Empty;
            return View("~/Views/organization/Login.cshtml", model);
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(user.Id, cancellationToken);
        var activeMemberships = memberships
            .Where(membership => membership.Organization.IsAvailableForUse())
            .ToArray();
        if (activeMemberships.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Email or member ID and password are incorrect, or this account has no active organization.");
            model.Password = string.Empty;
            return View("~/Views/organization/Login.cshtml", model);
        }

        var selectedMembership = organizationId is Guid selectedOrganizationId
            ? activeMemberships.FirstOrDefault(membership => membership.OrganizationId == selectedOrganizationId)
            : activeMemberships.Length == 1 ? activeMemberships[0] : null;
        if (organizationId.HasValue && selectedMembership is null)
        {
            ModelState.AddModelError(string.Empty, "This account does not have access to the organization in that sign-in link.");
            model.Password = string.Empty;
            return View("~/Views/organization/Login.cshtml", model);
        }

        await SignInAdminAsync(user, selectedMembership?.OrganizationId);

        logger.LogInformation("Organization portal sign-in completed for user {UserId}.", user.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult LegacyLogin(Guid? oId, string? m)
    {
        return RedirectToAction(nameof(Login), new { oId });
    }

    [HttpGet("")]
    [Authorize(
        AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme,
        Roles = "OrganizationMember")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var selectedOrganizationId = User.FindFirstValue("OrganizationId");
        var activeMemberships = memberships
            .Where(membership => membership.Organization.IsAvailableForUse())
            .ToArray();
        if (activeMemberships.Length == 0)
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        if (selectedOrganizationId is not null)
        {
            if (!Guid.TryParse(selectedOrganizationId, out var organizationId))
            {
                await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            var selectedMembership = activeMemberships.FirstOrDefault(candidate => candidate.OrganizationId == organizationId);
            if (selectedMembership is null)
            {
                await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            activeMemberships = [selectedMembership];
        }

        return View("~/Views/organization/Index.cshtml", new OrganizationPortalIndexViewModel
        {
            UserName = User.Identity?.Name ?? "Organization member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = activeMemberships.Select(membership => new OrganizationPortalMembershipViewModel(
                    membership.OrganizationId,
                    membership.Organization.Name,
                    membership.Organization.Email,
                    membership.Organization.Slug,
                    membership.Role.ToString())).ToArray(),
            RequiresOrganizationSelection = selectedOrganizationId is null && activeMemberships.Length > 1
        });
    }

    [HttpPost("select")]
    [Authorize(
        AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme,
        Roles = "OrganizationMember")]
    public async Task<IActionResult> SelectOrganization(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || user.Role == UserRole.Superuser)
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships.FirstOrDefault(candidate =>
            candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        await SignInAdminAsync(user, membership.OrganizationId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInAdminAsync(User user, Guid? organizationId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, "OrganizationMember")
        };
        if (organizationId is Guid selectedOrganizationId)
        {
            claims.Add(new Claim("OrganizationId", selectedOrganizationId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, TenantCookieDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            TenantCookieDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
    }

}

public sealed class OrganizationLoginViewModel
{
    public string? OrganizationContext { get; set; }

    [Required, StringLength(200)]
    public string Identifier { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

public sealed class OrganizationPortalIndexViewModel
{
    public string UserName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public IReadOnlyCollection<OrganizationPortalMembershipViewModel> Organizations { get; init; } = [];
    public bool RequiresOrganizationSelection { get; init; }
}

public sealed record OrganizationPortalMembershipViewModel(
    Guid Id,
    string Name,
    string Email,
    string Slug,
    string Role);
