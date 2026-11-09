using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Modules.Organizations.DTOs;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Application.Modules.Subscriptions.Services;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.Infrastructure.Data;
using CloudHubs.Web.Security;
using CloudHubs.Web.ViewModels.Auth;
using CloudHubs.Web.ViewModels.Docs;
using CloudHubs.Web.ViewModels.Organizations;
using CloudHubs.Web.ViewModels.Projects;

namespace CloudHubs.Web.Areas.Organization.Controllers;

[Area("Organization")]
[Route("organization")]
[AutoValidateAntiforgeryToken]
public class PortalController(
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    IOrganizationRoleRepository roleRepository,
    OrganizationTenantService tenantService,
    OrganizationPermissionService permissionService,
    OrganizationService organizationService,
    AccountSubscriptionService accountSubscriptionService,
    IProjectTemplateService templateService,
    PlatformSettingsService platformSettingsService,
    IPasswordHashingService passwordHashingService,
    CloudHubsDbContext dbContext,
    ILogger<PortalController> logger) : Controller
{
    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login(Guid? oId)
    {
        return View("Login", new OrganizationLoginViewModel
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
            return View("Login", model);
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
            return View("Login", model);
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(user.Id, cancellationToken);
        var activeMemberships = memberships
            .Where(membership => membership.Organization.IsAvailableForUse())
            .ToArray();
        if (activeMemberships.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Email or member ID and password are incorrect, or this account has no active organization.");
            model.Password = string.Empty;
            return View("Login", model);
        }

        var selectedMembership = organizationId is Guid selectedOrganizationId
            ? activeMemberships.FirstOrDefault(membership => membership.OrganizationId == selectedOrganizationId)
            : activeMemberships.Length == 1 ? activeMemberships[0] : null;
        if (organizationId.HasValue && selectedMembership is null)
        {
            ModelState.AddModelError(string.Empty, "This account does not have access to the organization in that sign-in link.");
            model.Password = string.Empty;
            return View("Login", model);
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

        OrganizationMembership? currentMembership = null;
        if (selectedOrganizationId is not null)
        {
            if (!Guid.TryParse(selectedOrganizationId, out var organizationId))
            {
                await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            currentMembership = activeMemberships.FirstOrDefault(candidate => candidate.OrganizationId == organizationId);
            if (currentMembership is null)
            {
                await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }
        }
        else if (activeMemberships.Length == 1)
        {
            currentMembership = activeMemberships[0];
        }

        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        var accountOrganizationCount = accountSubscription is null
            ? 0
            : await accountSubscriptionService.GetOrganizationCountAsync(accountSubscription.Id, cancellationToken);
        var maxOrganizations = accountSubscription?.Plan.MaxOrganizations ?? 0;

        IReadOnlyCollection<Project> projects = [];
        IReadOnlyCollection<OrganizationMembership> members = [];
        var canViewProjects = false;
        var canManageProjects = false;
        var canViewMembers = false;
        var canManageMembers = false;
        OrganizationPortalCurrentOrganizationViewModel? currentOrganization = null;
        if (currentMembership is not null)
        {
            var organization = currentMembership.Organization;
            var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
            canViewProjects = await permissionService.HasPermissionAsync(
                organization.Id, userId, OrganizationPermission.ViewProjects, cancellationToken);
            canManageProjects = await permissionService.HasPermissionAsync(
                organization.Id, userId, OrganizationPermission.ManageProjects, cancellationToken);
            canViewMembers = await permissionService.HasPermissionAsync(
                organization.Id, userId, OrganizationPermission.ViewMembers, cancellationToken);
            canManageMembers = await permissionService.HasPermissionAsync(
                organization.Id, userId, OrganizationPermission.ManageMembers, cancellationToken);

            if (canViewProjects)
            {
                projects = await tenantService.GetProjectsAsync(
                    organization.Id, userId, cancellationToken, requireApiAccess: false);
            }

            if (canViewMembers)
            {
                members = await tenantService.GetMembersAsync(
                    organization.Id, userId, cancellationToken, requireApiAccess: false);
            }

            currentOrganization = new OrganizationPortalCurrentOrganizationViewModel(
                organization.Id,
                organization.Name,
                organization.Email,
                organization.Slug,
                currentMembership.CustomRole?.Name ?? currentMembership.Role.ToString(),
                plan?.Name ?? "No plan",
                organization.AccountSubscription?.ExpiresAt ?? organization.Subscription?.ExpiresAt,
                plan?.MaxProjects ?? 0,
                plan?.MaxMembers ?? 0);
        }

        return View("Index", new OrganizationPortalIndexViewModel
        {
            UserName = User.Identity?.Name ?? "Organization member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = activeMemberships.Select(membership => new OrganizationPortalMembershipViewModel(
                    membership.OrganizationId,
                    membership.Organization.Name,
                    membership.Organization.Email,
                    membership.Organization.Slug,
                    membership.CustomRole?.Name ?? membership.Role.ToString())).ToArray(),
            CurrentOrganization = currentOrganization,
            Projects = projects.Select(project =>
            {
                var template = templateService.GetTemplate(project.TemplateId);
                return new OrganizationPortalProjectViewModel(
                    project.Id,
                    project.Name,
                    project.Slug,
                    project.Description,
                    project.CreatedAt,
                    project.TemplateId,
                    template?.Name ?? project.TemplateId,
                    project.RootPath);
            }).ToArray(),
            Members = members.Select(membership => new OrganizationPortalMemberViewModel(
                membership.UserId,
                membership.User.LoginId ?? string.Empty,
                membership.User.Name,
                membership.User.Email ?? string.Empty,
                membership.CustomRole?.Name ?? membership.Role.ToString(),
                membership.Role == OrganizationMemberRole.Owner,
                membership.CustomRole is not null)).ToArray(),
            AvailableTemplates = templateService.GetTemplates(),
            CanViewProjects = canViewProjects,
            CanManageProjects = canManageProjects,
            CanViewMembers = canViewMembers,
            CanManageMembers = canManageMembers,
            RequiresOrganizationSelection = currentMembership is null && activeMemberships.Length > 0,
            CanCreateOrganization = accountSubscription is not null
                && (maxOrganizations == 0 || accountOrganizationCount < maxOrganizations),
            CanShowOrganizationAction = accountSubscription is not null
                || activeMemberships.Any(membership => membership.Role == OrganizationMemberRole.Owner),
            HasAccountSubscription = accountSubscription is not null,
            AccountOrganizationCount = accountOrganizationCount,
            MaxOrganizations = maxOrganizations
        });
    }

    [HttpGet("account")]
    [HttpGet("settings")]
    [HttpGet("settings/{category}")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> Account(
        [FromRoute] string? category,
        [FromQuery] string? tab,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var activeMemberships = memberships
            .Where(membership => membership.Organization.IsAvailableForUse())
            .ToArray();
        if (activeMemberships.Length == 0)
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var selectedOrganizationId = User.FindFirstValue("OrganizationId");
        OrganizationMembership? currentMembership = null;
        if (selectedOrganizationId is not null && Guid.TryParse(selectedOrganizationId, out var parsedOrgId))
        {
            currentMembership = activeMemberships.FirstOrDefault(candidate => candidate.OrganizationId == parsedOrgId);
        }
        currentMembership ??= activeMemberships[0];

        var organization = currentMembership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;

        var canViewProjects = await permissionService.HasPermissionAsync(
            organization.Id, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var canViewMembers = await permissionService.HasPermissionAsync(
            organization.Id, userId, OrganizationPermission.ViewMembers, cancellationToken);
        var canManageOrg = await permissionService.HasPermissionAsync(
            organization.Id, userId, OrganizationPermission.ManageMembers, cancellationToken)
            || currentMembership.Role is OrganizationMemberRole.Owner or OrganizationMemberRole.Admin;

        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        var accountOrganizationCount = accountSubscription is null
            ? 0
            : await accountSubscriptionService.GetOrganizationCountAsync(accountSubscription.Id, cancellationToken);
        var maxOrganizations = accountSubscription?.Plan.MaxOrganizations ?? 0;

        var currentOrgVm = new OrganizationPortalCurrentOrganizationViewModel(
            organization.Id,
            organization.Name,
            organization.Email,
            organization.Slug,
            currentMembership.CustomRole?.Name ?? currentMembership.Role.ToString(),
            plan?.Name ?? "Standard",
            organization.AccountSubscription?.ExpiresAt ?? organization.Subscription?.ExpiresAt,
            plan?.MaxProjects ?? 0,
            plan?.MaxMembers ?? 0);

        var allDbPlans = await dbContext.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync(cancellationToken);

        var currentPlanName = plan?.Name ?? "Basic";
        var availablePlans = allDbPlans.Select(p => new SubscriptionPlanTierViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Currency = p.Currency,
            MaxProjects = p.MaxProjects,
            MaxMembers = p.MaxMembers,
            StorageLimitGb = p.StorageLimitGb,
            HasApiAccess = p.HasApiAccess,
            HasSingleSignOn = p.HasSingleSignOn,
            HasIntegrations = p.HasIntegrations,
            HasCustomRoles = p.HasCustomRoles,
            IsCurrent = p.Name.Equals(currentPlanName, StringComparison.OrdinalIgnoreCase)
        }).ToList();

        var selectedCategory = !string.IsNullOrWhiteSpace(category)
            ? category.Trim().ToLowerInvariant()
            : (!string.IsNullOrWhiteSpace(tab) ? tab.Trim().ToLowerInvariant() : null);

        var model = new OrganizationAccountViewModel
        {
            UserName = user.Name,
            UserEmail = user.Email ?? string.Empty,
            MemberId = user.LoginId ?? user.Id.ToString(),
            Organizations = activeMemberships.Select(m => new OrganizationPortalMembershipViewModel(
                m.OrganizationId,
                m.Organization.Name,
                m.Organization.Email,
                m.Organization.Slug,
                m.CustomRole?.Name ?? m.Role.ToString())).ToArray(),
            CurrentOrganization = currentOrgVm,
            CanViewProjects = canViewProjects,
            CanViewMembers = canViewMembers,
            CanManageOrganization = canManageOrg,
            CanShowOrganizationAction = maxOrganizations == 0 || accountOrganizationCount < maxOrganizations,
            HasAccountSubscription = accountSubscription is not null,
            AccountOrganizationCount = accountOrganizationCount,
            MaxOrganizations = maxOrganizations,

            ProfileName = user.Name,
            ProfileEmail = user.Email ?? string.Empty,
            OrganizationName = organization.Name,
            OrganizationEmail = organization.Email,
            OrganizationSlug = organization.Slug,
            OrganizationApiKey = organization.ApiKey,
            OrganizationBaseApiUrl = organization.BaseApiUrl,
            OrganizationStatus = organization.Status.ToString(),
            SubscriptionPlan = currentPlanName,
            HasApiAccess = plan?.HasApiAccess ?? false,
            IsProOrCustomPlan = (plan?.HasApiAccess == true) || (currentPlanName is "Pro" or "Custom"),
            ActiveTab = selectedCategory ?? "workspace",
            SelectedCategory = selectedCategory,
            SubscriptionExpiresAt = organization.AccountSubscription?.ExpiresAt ?? organization.Subscription?.ExpiresAt,
            UserRole = currentMembership.CustomRole?.Name ?? currentMembership.Role.ToString(),
            AvailablePlans = availablePlans
        };

        return View("Account", model);
    }

    [HttpPost("account/upgrade-plan")]
    [HttpPost("settings/upgrade-plan")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> UpgradePlan(
        [FromForm] string planName,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Login));
        }

        var organization = await dbContext.Organizations
            .Include(o => o.Subscription)
            .Include(o => o.AccountSubscription)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            TempData["Error"] = "Organization workspace was not found.";
            return RedirectToAction(nameof(Account), new { tab = "subscription" });
        }

        var targetPlan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Name.ToLower() == planName.Trim().ToLower(), cancellationToken);

        if (targetPlan is null)
        {
            TempData["Error"] = $"Selected subscription package '{planName}' is not available.";
            return RedirectToAction(nameof(Account), new { tab = "subscription" });
        }

        // Update organization subscription
        if (organization.Subscription is null)
        {
            organization.Subscription = new Subscription
            {
                OrganizationId = organization.Id,
                PlanId = targetPlan.Id,
                PlanName = targetPlan.Name,
                Status = SubscriptionStatus.Active,
                StartsAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
        }
        else
        {
            organization.Subscription.PlanId = targetPlan.Id;
            organization.Subscription.PlanName = targetPlan.Name;
            organization.Subscription.Status = SubscriptionStatus.Active;
            organization.Subscription.UpdatedAt = DateTime.UtcNow;
        }

        // Update account subscription if present
        if (organization.AccountSubscriptionId is Guid accountSubId)
        {
            var accountSub = await dbContext.AccountSubscriptions
                .FirstOrDefaultAsync(a => a.Id == accountSubId, cancellationToken);
            if (accountSub is not null)
            {
                accountSub.PlanId = targetPlan.Id;
                accountSub.Plan = targetPlan;
                accountSub.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Auto-generate Master API Key if new plan provides API access and none exists
        if (targetPlan.HasApiAccess && string.IsNullOrWhiteSpace(organization.ApiKey))
        {
            var rawKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            organization.ApiKey = $"ch_live_cloudhubs_{rawKey}";
        }

        organization.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"🎉 Plan successfully updated to {targetPlan.Name}! Features unlocked: {(targetPlan.HasApiAccess ? "⚡ Organization Platform API Unlocked, " : "")}Max Projects: {targetPlan.MaxProjects}, Max Members: {targetPlan.MaxMembers}, Cloud Storage: {targetPlan.StorageLimitGb} GB.";

        return RedirectToAction(nameof(Account), new { category = "subscription" });
    }

    [HttpPost("account/user-profile")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> UpdateUserProfile(
        [FromForm] string profileName,
        [FromForm] string profileEmail,
        CancellationToken cancellationToken)
    {
        return await RunPortalActionAsync(
            async userId =>
            {
                var updated = await tenantService.UpdateUserProfileAsync(userId, profileName, profileEmail, cancellationToken);
                var selectedOrgId = User.FindFirstValue("OrganizationId");
                Guid? orgId = Guid.TryParse(selectedOrgId, out var parsed) ? parsed : null;
                await SignInAdminAsync(updated, orgId);
            },
            "Your profile details have been updated successfully.",
            "Unable to update your user profile.",
            cancellationToken,
            nameof(Account),
            new { category = "profile" });
    }

    [HttpPost("account/change-password")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> ChangeUserPassword(
        [FromForm] string currentPassword,
        [FromForm] string newPassword,
        [FromForm] string confirmNewPassword,
        CancellationToken cancellationToken)
    {
        return await RunPortalActionAsync(
            async userId =>
            {
                var settings = await platformSettingsService.GetAsync(cancellationToken);
                await tenantService.ChangeUserPasswordAsync(
                    userId,
                    currentPassword,
                    newPassword,
                    confirmNewPassword,
                    passwordHashingService,
                    settings.MinimumPasswordLength,
                    settings.RequirePasswordDigit,
                    cancellationToken);
            },
            "Your password has been changed successfully.",
            "Unable to change your password.",
            cancellationToken,
            nameof(Account),
            new { category = "profile" });
    }

    [HttpPost("account/organization-profile")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> UpdateOrganizationProfile(
        [FromForm] Guid organizationId,
        [FromForm] string organizationName,
        [FromForm] string organizationEmail,
        [FromForm] string? organizationBaseApiUrl,
        CancellationToken cancellationToken)
    {
        return await RunPortalActionAsync(
            async userId =>
            {
                await tenantService.UpdateOrganizationProfileAsync(
                    organizationId,
                    userId,
                    organizationName,
                    organizationEmail,
                    organizationBaseApiUrl,
                    cancellationToken);
            },
            "Organization profile updated successfully.",
            "Unable to update organization profile.",
            cancellationToken,
            nameof(Account),
            new { category = "workspace" });
    }

    [HttpPost("account/regenerate-api-key")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> RegenerateApiKey(
        [FromForm] Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await RunPortalActionAsync(
            async userId =>
            {
                await tenantService.RegenerateApiKeyAsync(organizationId, userId, cancellationToken);
            },
            "A new organization API key has been generated.",
            "Unable to regenerate organization API key.",
            cancellationToken,
            nameof(Account),
            new { category = "api" });
    }

    [HttpGet("projects")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> Projects(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Index));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate => candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null
            || !await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken))
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var canManageProjects = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        var canViewMembers = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken);
        var canManageMembers = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ManageMembers, cancellationToken);

        var projects = await tenantService.GetProjectsAsync(organizationId, userId, cancellationToken, requireApiAccess: false);
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        var accountOrganizationCount = accountSubscription is null
            ? 0
            : await accountSubscriptionService.GetOrganizationCountAsync(accountSubscription.Id, cancellationToken);
        var maxOrganizations = accountSubscription?.Plan.MaxOrganizations ?? 0;

        return View("Projects", new OrganizationPortalIndexViewModel
        {
            UserName = User.Identity?.Name ?? "Organization member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(m => m.Organization.IsAvailableForUse())
                .Select(m => new OrganizationPortalMembershipViewModel(
                    m.OrganizationId,
                    m.Organization.Name,
                    m.Organization.Email,
                    m.Organization.Slug,
                    m.CustomRole?.Name ?? m.Role.ToString())).ToArray(),
            CurrentOrganization = new OrganizationPortalCurrentOrganizationViewModel(
                organization.Id,
                organization.Name,
                organization.Email,
                organization.Slug,
                membership.CustomRole?.Name ?? membership.Role.ToString(),
                plan?.Name ?? "No plan",
                organization.AccountSubscription?.ExpiresAt ?? organization.Subscription?.ExpiresAt,
                plan?.MaxProjects ?? 0,
                plan?.MaxMembers ?? 0),
            Projects = projects.Select(p =>
            {
                var template = templateService.GetTemplate(p.TemplateId);
                return new OrganizationPortalProjectViewModel(
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Description,
                    p.CreatedAt,
                    p.TemplateId,
                    template?.Name ?? p.TemplateId,
                    p.RootPath);
            }).ToArray(),
            AvailableTemplates = templateService.GetTemplates(),
            CanViewProjects = true,
            CanManageProjects = canManageProjects,
            CanViewMembers = canViewMembers,
            CanManageMembers = canManageMembers,
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(m => m.Role == OrganizationMemberRole.Owner),
            HasAccountSubscription = accountSubscription is not null,
            AccountOrganizationCount = accountOrganizationCount,
            MaxOrganizations = maxOrganizations
        });
    }

    [HttpGet("projects/create")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> CreateProject(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Index));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate => candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null
            || !await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken))
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var projects = await tenantService.GetProjectsAsync(organizationId, userId, cancellationToken, requireApiAccess: false);
        var maxProjects = plan?.MaxProjects ?? 0;
        var canCreate = maxProjects == 0 || projects.Count < maxProjects;

        return View("CreateProject", new CreateOrganizationProjectViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            OrganizationSlug = organization.Slug,
            TemplateId = "nodejs-mvc",
            AvailableTemplates = templateService.GetTemplates(),
            PlanName = plan?.Name ?? "No plan",
            ProjectCount = projects.Count,
            MaxProjects = maxProjects,
            CanCreateProject = canCreate
        });
    }

    [HttpPost("projects/create")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> CreateProject(
        OrganizationPortalProjectFormModel model,
        [FromQuery] string? source,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            if (string.Equals(source, "page", StringComparison.OrdinalIgnoreCase))
            {
                return await RenderCreateProjectPageWithErrorAsync(model, "Enter a project name and a valid slug using letters, numbers, hyphens, or underscores.", cancellationToken);
            }
            TempData["Error"] = "Enter a project name and a valid slug using letters, numbers, hyphens, or underscores.";
            return RedirectToAction(nameof(Index));
        }

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        try
        {
            var project = await tenantService.CreateProjectAsync(
                model.OrganizationId,
                userId,
                model.Name,
                model.Slug,
                model.Description,
                model.RootPath,
                model.TemplateId,
                cancellationToken,
                requireApiAccess: false);

            var template = templateService.GetTemplate(project.TemplateId);
            TempData["Success"] = $"Project '{project.Name}' created using the {template?.Name ?? project.TemplateId} template.";
            return RedirectToAction("Index", "MeroSpace", new { projectId = project.Id });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            if (string.Equals(source, "page", StringComparison.OrdinalIgnoreCase))
            {
                return await RenderCreateProjectPageWithErrorAsync(model, exception.Message, cancellationToken);
            }
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The project could not be created for user {UserId}.", userId);
            if (string.Equals(source, "page", StringComparison.OrdinalIgnoreCase))
            {
                return await RenderCreateProjectPageWithErrorAsync(model, "The project could not be created. Please try again later.", cancellationToken);
            }
            TempData["Error"] = "The project could not be created.";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task<IActionResult> RenderCreateProjectPageWithErrorAsync(
        OrganizationPortalProjectFormModel model,
        string error,
        CancellationToken cancellationToken)
    {
        ModelState.AddModelError(string.Empty, error);
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return RedirectToAction(nameof(Index));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships.FirstOrDefault(candidate => candidate.OrganizationId == model.OrganizationId);
        var organization = membership?.Organization;
        var plan = organization?.AccountSubscription?.Plan ?? organization?.Subscription?.Plan;
        var projectCount = model.OrganizationId != Guid.Empty
            ? (await tenantService.GetProjectsAsync(model.OrganizationId, userId, cancellationToken, requireApiAccess: false)).Count
            : 0;

        return View("CreateProject", new CreateOrganizationProjectViewModel
        {
            OrganizationId = model.OrganizationId,
            OrganizationName = organization?.Name ?? string.Empty,
            OrganizationSlug = organization?.Slug ?? string.Empty,
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            RootPath = model.RootPath,
            TemplateId = model.TemplateId,
            AvailableTemplates = templateService.GetTemplates(),
            PlanName = plan?.Name ?? "No plan",
            ProjectCount = projectCount,
            MaxProjects = plan?.MaxProjects ?? 0,
            CanCreateProject = plan?.MaxProjects == 0 || projectCount < (plan?.MaxProjects ?? 0)
        });
    }

    [HttpGet("members")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public Task<IActionResult> Members(CancellationToken cancellationToken) =>
        RenderMembersPageAsync(null, cancellationToken);

    private async Task<IActionResult> RenderMembersPageAsync(
        OrganizationPortalMemberFormModel? memberForm,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Index));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate =>
                candidate.OrganizationId == organizationId
                && candidate.Organization.IsAvailableForUse());
        if (membership is null
            || !await permissionService.HasPermissionAsync(
                organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken))
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        var members = await tenantService.GetMembersAsync(
            organizationId, userId, cancellationToken, requireApiAccess: false);

        return View("Members", new OrganizationPortalIndexViewModel
        {
            UserName = User.Identity?.Name ?? "Organization member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(candidate => candidate.Organization.IsAvailableForUse())
                .Select(candidate => new OrganizationPortalMembershipViewModel(
                    candidate.OrganizationId,
                    candidate.Organization.Name,
                    candidate.Organization.Email,
                    candidate.Organization.Slug,
                    candidate.CustomRole?.Name ?? candidate.Role.ToString()))
                .ToArray(),
            CurrentOrganization = new OrganizationPortalCurrentOrganizationViewModel(
                organization.Id,
                organization.Name,
                organization.Email,
                organization.Slug,
                membership.CustomRole?.Name ?? membership.Role.ToString(),
                plan?.Name ?? "No plan",
                organization.AccountSubscription?.ExpiresAt ?? organization.Subscription?.ExpiresAt,
                plan?.MaxProjects ?? 0,
                plan?.MaxMembers ?? 0),
            Members = members.Select(item => new OrganizationPortalMemberViewModel(
                item.UserId,
                item.User.LoginId ?? string.Empty,
                item.User.Name,
                item.User.Email ?? string.Empty,
                item.CustomRole?.Name ?? item.Role.ToString(),
                item.Role == OrganizationMemberRole.Owner,
                item.CustomRole is not null)).ToArray(),
            CanViewProjects = await permissionService.HasPermissionAsync(
                organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken),
            CanViewMembers = true,
            CanManageMembers = await permissionService.HasPermissionAsync(
                organizationId, userId, OrganizationPermission.ManageMembers, cancellationToken),
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(item => item.Role == OrganizationMemberRole.Owner),
            HasAccountSubscription = accountSubscription is not null,
            AccountOrganizationCount = accountSubscription is null
                ? 0
                : await accountSubscriptionService.GetOrganizationCountAsync(accountSubscription.Id, cancellationToken),
            MaxOrganizations = accountSubscription?.Plan.MaxOrganizations ?? 0,
            MemberFormEmail = memberForm?.Email ?? string.Empty,
            MemberFormName = memberForm?.Name ?? string.Empty,
            MemberFormRole = memberForm?.Role ?? "Developer",
            MemberFormOpen = memberForm is not null
        });
    }

    [HttpPost("projects/delete")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public Task<IActionResult> DeleteProject(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        RunPortalActionAsync(
            async userId => await tenantService.DeleteProjectAsync(
                organizationId, projectId, userId, cancellationToken, requireApiAccess: false),
            "Project deleted.",
            "The project could not be deleted.",
            cancellationToken);

    [HttpGet("projects/{projectId:guid}/resources")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> ProjectResources(Guid projectId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Index));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate => candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null
            || !await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken))
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var project = await tenantService.GetProjectAsync(organizationId, projectId, userId, cancellationToken);
        if (project is null)
        {
            TempData["Error"] = "Project was not found.";
            return RedirectToAction(nameof(Projects));
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var canManageProjects = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        var canViewMembers = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken);

        var template = templateService.GetTemplate(project.TemplateId);
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);

        return View("ProjectResources", new ProjectResourcesViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            OrganizationSlug = organization.Slug,
            OrganizationRole = membership.Role.ToString(),
            OrganizationApiKey = organization.ApiKey,
            ProjectId = project.Id,
            ProjectName = project.Name,
            ProjectSlug = project.Slug,
            ProjectDescription = project.Description,
            ProjectTemplateId = project.TemplateId,
            ProjectTemplateName = template?.Name ?? project.TemplateId,
            ProjectRootPath = project.RootPath,
            Documentation = project.Documentation,
            PostmanCollection = project.PostmanCollection,
            EnvironmentVariables = project.EnvironmentVariables,
            CanManageProjects = canManageProjects,
            CanViewMembers = canViewMembers,
            CanViewProjects = true,
            UserName = User.Identity?.Name ?? "Member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(candidate => candidate.Organization.IsAvailableForUse())
                .Select(candidate => new OrganizationPortalMembershipViewModel(
                    candidate.Organization.Id,
                    candidate.Organization.Name,
                    candidate.Organization.Email,
                    candidate.Organization.Slug,
                    candidate.Role.ToString()))
                .ToArray(),
            CurrentOrganization = new OrganizationPortalCurrentOrganizationViewModel(
                organization.Id,
                organization.Name,
                organization.Email,
                organization.Slug,
                membership.CustomRole?.Name ?? membership.Role.ToString(),
                plan?.Name ?? "No plan",
                organization.AccountSubscription?.ExpiresAt ?? organization.Subscription?.ExpiresAt,
                plan?.MaxProjects ?? 0,
                plan?.MaxMembers ?? 0),
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(m => m.Role == OrganizationMemberRole.Owner)
        });
    }

    [HttpPost("projects/{projectId:guid}/resources")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> UpdateProjectResources(
        Guid projectId,
        UpdateProjectResourcesFormModel form,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await tenantService.UpdateProjectResourcesAsync(
                organizationId,
                projectId,
                userId,
                form.Documentation,
                form.PostmanCollection,
                form.EnvironmentVariables,
                cancellationToken);
            TempData["Success"] = "Project documentation, Postman collection, and environment files updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(ProjectResources), new { projectId });
    }

    [HttpGet("projects/{projectId:guid}/resources/download-postman")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> DownloadPostmanCollection(Guid projectId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Index));
        }

        var project = await tenantService.GetProjectAsync(organizationId, projectId, userId, cancellationToken);
        if (project is null) return NotFound();

        var content = project.PostmanCollection ?? "{}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var filename = $"{project.Slug}-postman_collection.json";
        return File(bytes, "application/json", filename);
    }

    [HttpGet("projects/{projectId:guid}/resources/download-env")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> DownloadEnvFile(Guid projectId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Index));
        }

        var project = await tenantService.GetProjectAsync(organizationId, projectId, userId, cancellationToken);
        if (project is null) return NotFound();

        var content = project.EnvironmentVariables ?? "# Environment variables\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var filename = $"{project.Slug}.env";
        return File(bytes, "text/plain", filename);
    }

    [HttpPost("members/add")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> AddMember(
        OrganizationPortalMemberFormModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid
            || !Enum.TryParse<OrganizationMemberRole>(model.Role, true, out var role)
            || role is OrganizationMemberRole.Owner
            || !Enum.IsDefined(role))
        {
            if (ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Choose Admin, Developer, or Viewer.");
            }

            return await RenderMembersPageAsync(model, cancellationToken);
        }

        try
        {
            var existingUser = await userRepository.GetByEmailAsync(model.Email, cancellationToken);
            OrganizationMembership membership;
            if (existingUser is not null)
            {
                membership = await tenantService.AddMemberAsync(
                    model.OrganizationId,
                    GetSignedInUserId(),
                    model.Email,
                    role,
                    cancellationToken,
                    requireApiAccess: false);
                TempData["Success"] =
                    $"Existing account added. Member ID: {membership.User.LoginId}. Its password was not changed.";
            }
            else
            {
                var member = new User
                {
                    Name = model.Name.Trim(),
                    Email = model.Email.Trim().ToLowerInvariant(),
                    Role = role switch
                    {
                        OrganizationMemberRole.Admin => UserRole.OrganizationAdmin,
                        OrganizationMemberRole.Viewer => UserRole.Viewer,
                        _ => UserRole.Developer
                    },
                    IsActive = true
                };
                member.PasswordHash = passwordHasher.HashPassword(member, model.Password);
                membership = await tenantService.CreateMemberAsync(
                    model.OrganizationId,
                    GetSignedInUserId(),
                    member,
                    role,
                    cancellationToken,
                    requireApiAccess: false);
                TempData["Success"] =
                    $"Member created. Member ID: {membership.User.LoginId}. The password you provided is set.";
            }

            return RedirectToAction(nameof(Members));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await RenderMembersPageAsync(model, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Unable to add member to organization {OrganizationId}.", model.OrganizationId);
            ModelState.AddModelError(string.Empty, "The member could not be added. Please try again later.");
            return await RenderMembersPageAsync(model, cancellationToken);
        }
    }

    [HttpPost("members/update-role")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid organizationId,
        Guid memberId,
        string role,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrganizationMemberRole>(role, true, out var parsedRole)
            || parsedRole is OrganizationMemberRole.Owner
            || !Enum.IsDefined(parsedRole))
        {
            TempData["Error"] = "Choose Admin, Developer, or Viewer.";
            return RedirectToAction(nameof(Members));
        }

        return await RunPortalActionAsync(
            async userId => await tenantService.UpdateMemberRoleAsync(
                organizationId,
                userId,
                memberId,
                parsedRole,
                customRoleId: null,
                cancellationToken,
                requireApiAccess: false),
            "Member role updated.",
            "The member role could not be updated.",
            cancellationToken,
            nameof(Members));
    }

    [HttpPost("members/remove")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public Task<IActionResult> RemoveMember(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken) =>
        RunPortalActionAsync(
            async userId => await tenantService.RemoveMemberAsync(
                organizationId, userId, memberId, cancellationToken, requireApiAccess: false),
            "Member removed from the organization.",
            "The member could not be removed.",
            cancellationToken,
            nameof(Members));

    [HttpPost("workspaces")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> ShowWorkspaces(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        if (user is null
            || !user.IsActive
            || user.Role == UserRole.Superuser
            || !memberships.Any(membership => membership.Organization.IsAvailableForUse()))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        await SignInAdminAsync(user, organizationId: null);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("create")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> CreateOrganization(CancellationToken cancellationToken)
    {
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        if (accountSubscription is null)
        {
            return View("SubscriptionRequired");
        }

        var organizationCount = await accountSubscriptionService.GetOrganizationCountAsync(
            accountSubscription.Id, cancellationToken);
        var maxOrganizations = accountSubscription.Plan.MaxOrganizations;
        return View("Create", new OrganizationPortalCreateViewModel
        {
            ContactEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            PlanName = accountSubscription.Plan.Name,
            ExpiresAt = accountSubscription.ExpiresAt,
            OrganizationCount = organizationCount,
            MaxOrganizations = maxOrganizations,
            CanCreateOrganization = maxOrganizations == 0 || organizationCount < maxOrganizations
        });
    }

    [HttpPost("create")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> CreateOrganization(
        OrganizationPortalCreateViewModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            var accountSubscription = await accountSubscriptionService.GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
                userId, cancellationToken);
            if (user is null || accountSubscription is null || !accountSubscription.IsAvailable())
            {
                return View("SubscriptionRequired");
            }

            model.PlanName = accountSubscription.Plan.Name;
            model.ExpiresAt = accountSubscription.ExpiresAt;
            model.MaxOrganizations = accountSubscription.Plan.MaxOrganizations;
            model.OrganizationCount = await accountSubscriptionService.GetOrganizationCountAsync(
                accountSubscription.Id, cancellationToken);
            model.CanCreateOrganization = model.MaxOrganizations == 0
                || model.OrganizationCount < model.MaxOrganizations;
            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            if (!model.CanCreateOrganization)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"Your {model.PlanName} plan allows up to {model.MaxOrganizations} organizations. Upgrade your plan to create more.");
                return View("Create", model);
            }

            var organization = await organizationService.CreateOrganizationAsync(
                new CreateOrganizationRequest(model.Name, model.ContactEmail, string.Empty, string.Empty, string.Empty),
                cancellationToken,
                [new OrganizationMembership { User = user, Role = OrganizationMemberRole.Owner }],
                accountSubscription.PlanId,
                accountSubscription);

            await SignInAdminAsync(user, organization.Id);
            TempData["Success"] = "Organization created under your account subscription.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                && await accountSubscriptionService.GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
                    userId, cancellationToken) is { } subscription)
            {
                model.PlanName = subscription.Plan.Name;
                model.ExpiresAt = subscription.ExpiresAt;
                model.MaxOrganizations = subscription.Plan.MaxOrganizations;
                model.OrganizationCount = await accountSubscriptionService.GetOrganizationCountAsync(
                    subscription.Id, cancellationToken);
                model.CanCreateOrganization = model.MaxOrganizations == 0
                    || model.OrganizationCount < model.MaxOrganizations;
            }
            return View("Create", model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to create organization for account {UserId}.", User.FindFirstValue(ClaimTypes.NameIdentifier));
            ModelState.AddModelError(string.Empty, "The organization could not be created. Please try again later.");
            return View("Create", model);
        }
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

    private async Task<AccountSubscription?> GetCurrentAccountSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return null;
        }

        var subscription = await accountSubscriptionService.GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
            userId, cancellationToken);
        return subscription is { } accountSubscription && accountSubscription.IsAvailable()
            ? accountSubscription
            : null;
    }

    private Guid GetSignedInUserId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            throw new UnauthorizedAccessException("Your session has expired. Sign in again.");
        }

        return userId;
    }

    private async Task<IActionResult> RunPortalActionAsync(
        Func<Guid, Task> action,
        string successMessage,
        string genericError,
        CancellationToken cancellationToken,
        string redirectAction = nameof(Index),
        object? routeValues = null)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        try
        {
            await action(userId);
            TempData["Success"] = successMessage;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "{Message} Organization portal user {UserId}.", genericError, userId);
            TempData["Error"] = genericError;
        }

        return routeValues != null
            ? RedirectToAction(redirectAction, routeValues)
            : RedirectToAction(redirectAction);
    }

}
