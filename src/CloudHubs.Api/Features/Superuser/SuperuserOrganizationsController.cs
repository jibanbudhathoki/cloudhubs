using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Organizations;
using CloudHubs.Application.Users;
using CloudHubs.Contracts.Organizations;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace CloudHubs.Api.Features.Superuser;

[Route("su/organization")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserOrganizationsController : Controller
{
    private readonly OrganizationService _organizationService;
    private readonly OrganizationCreatedNotificationSender _notificationSender;
    private readonly ILogger<SuperuserOrganizationsController> _logger;
    private readonly UserService _userService;
    private readonly PlatformSettingsService _platformSettingsService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly SubscriptionPlanService _planService;

    public SuperuserOrganizationsController(
        OrganizationService organizationService,
        OrganizationCreatedNotificationSender notificationSender,
        ILogger<SuperuserOrganizationsController> logger,
        UserService userService,
        PlatformSettingsService platformSettingsService,
        IPasswordHasher<User> passwordHasher,
        SubscriptionPlanService planService)
    {
        _organizationService = organizationService;
        _notificationSender = notificationSender;
        _logger = logger;
        _userService = userService;
        _platformSettingsService = platformSettingsService;
        _passwordHasher = passwordHasher;
        _planService = planService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? query, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Organizations";
        try
        {
            var organizations = await _organizationService.GetOrganizationsAsync(cancellationToken);
            var normalizedQuery = query?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                organizations = organizations.Where(item =>
                    item.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || item.Slug.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || item.Email.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || (item.OwnerName?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (item.OwnerEmail?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                    || item.Status.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || (item.SubscriptionStatus?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false)).ToArray();
            }

            return View("~/Views/superuser/Pages/Organization.cshtml", new SuperuserDashboardViewModel(organizations, true, null, normalizedQuery));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to load organizations for the Superuser organization page.");
            return View("~/Views/superuser/Pages/Organization.cshtml", new SuperuserDashboardViewModel([], false, "Organization data is unavailable. Check the database connection and try again.", query));
        }
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Create organization";
        await SetAvailableUsersAsync(cancellationToken);
        await SetAvailablePlansAsync(cancellationToken);
        return View("~/Views/superuser/Pages/OrganizationForm.cshtml", new OrganizationFormViewModel());
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(OrganizationFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Create organization";
        ModelState.Remove(nameof(model.Slug));
        model.Slug = model.Name;
        if (model.SubscriptionPlanId is null)
        {
            ModelState.AddModelError(nameof(model.SubscriptionPlanId), "Select a subscription plan.");
        }
        var users = await _userService.GetUsersAsync(cancellationToken);
        var availableUsers = users
            .Where(IsAvailableForOrganization)
            .ToArray();
        var settings = await _platformSettingsService.GetAsync(cancellationToken);
        var memberships = await CreateMembershipsAsync(model, users, settings.MinimumPasswordLength, settings.RequirePasswordDigit, cancellationToken);
        if (!ModelState.IsValid)
        {
            ClearPasswords(model);
            await SetAvailableUsersAsync(availableUsers);
            await SetAvailablePlansAsync(cancellationToken);
            return View("~/Views/superuser/Pages/OrganizationForm.cshtml", model);
        }

        try
        {
            var organization = await _organizationService.CreateOrganizationAsync(
                new CreateOrganizationRequest(model.Name, model.Email, model.Slug, string.Empty, string.Empty),
                cancellationToken,
                memberships,
                model.SubscriptionPlanId);
            TempData["Success"] = "Organization created.";
            try
            {
                await _notificationSender.SendIfEnabledAsync(organization, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Organization {OrganizationId} was created, but its notification email could not be sent.", organization.Id);
                TempData["Error"] = "Organization was created, but the notification email could not be sent. Check SMTP configuration and server connectivity.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ClearPasswords(model);
            await SetAvailableUsersAsync(availableUsers);
            await SetAvailablePlansAsync(cancellationToken);
            return View("~/Views/superuser/Pages/OrganizationForm.cshtml", model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create organization {OrganizationName}.", model.Name);
            ModelState.AddModelError(string.Empty, "The organization could not be saved. Check the database connection and try again.");
            ClearPasswords(model);
            await SetAvailableUsersAsync(availableUsers);
            await SetAvailablePlansAsync(cancellationToken);
            return View("~/Views/superuser/Pages/OrganizationForm.cshtml", model);
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var organization = await _organizationService.GetOrganizationAsync(id, cancellationToken);
        if (organization is null) return NotFound();
        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Edit organization";
        return View("~/Views/superuser/Pages/OrganizationForm.cshtml", new OrganizationFormViewModel
        {
            Id = id,
            Name = organization.Name,
            Email = organization.Email,
            Slug = organization.Slug,
            Status = organization.Status
        });
    }

    [HttpGet("{id:guid}/features")]
    public async Task<IActionResult> Features(Guid id, CancellationToken cancellationToken)
    {
        var organization = await _organizationService.GetOrganizationAsync(id, cancellationToken);
        if (organization is null) return NotFound();

        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Organization subscription features";
        return View("~/Views/superuser/Pages/OrganizationFeatures.cshtml", organization);
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, OrganizationFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Edit organization";
        model.Id = id;
        if (!ModelState.IsValid) return View("~/Views/superuser/Pages/OrganizationForm.cshtml", model);

        try
        {
            var updated = await _organizationService.UpdateOrganizationAsync(id, model.Name, model.Email, model.Slug, model.Status, cancellationToken);
            if (updated is null) return NotFound();
            TempData["Success"] = "Organization updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("~/Views/superuser/Pages/OrganizationForm.cshtml", model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update organization {OrganizationId}.", id);
            ModelState.AddModelError(string.Empty, "The organization could not be saved. Check the database connection and try again.");
            return View("~/Views/superuser/Pages/OrganizationForm.cshtml", model);
        }
    }

    [HttpGet("{id:guid}/subscription")]
    public async Task<IActionResult> ChangeSubscriptionPlan(Guid id, CancellationToken cancellationToken)
    {
        var organization = await _organizationService.GetOrganizationAsync(id, cancellationToken);
        if (organization is null) return NotFound();

        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Change subscription plan";
        await SetAvailablePlansAsync(cancellationToken);
        return View("~/Views/superuser/Pages/OrganizationSubscriptionPlan.cshtml", new OrganizationSubscriptionPlanViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            CurrentPlanName = organization.SubscriptionPlanName,
            SubscriptionStatus = organization.SubscriptionStatus
        });
    }

    [HttpPost("{id:guid}/subscription")]
    public async Task<IActionResult> ChangeSubscriptionPlan(
        Guid id,
        OrganizationSubscriptionPlanViewModel model,
        CancellationToken cancellationToken)
    {
        model.OrganizationId = id;
        if (!ModelState.IsValid || model.PlanId is null)
        {
            await PopulateSubscriptionPlanFormAsync(id, model, cancellationToken);
            return View("~/Views/superuser/Pages/OrganizationSubscriptionPlan.cshtml", model);
        }

        try
        {
            if (!await _organizationService.ChangeSubscriptionPlanAsync(id, model.PlanId.Value, cancellationToken))
            {
                return NotFound();
            }

            TempData["Success"] = "Organization subscription plan updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(model.PlanId), exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to change subscription plan for organization {OrganizationId}.", id);
            ModelState.AddModelError(string.Empty, "The subscription plan could not be changed. Check the database connection and try again.");
        }

        await PopulateSubscriptionPlanFormAsync(id, model, cancellationToken);
        return View("~/Views/superuser/Pages/OrganizationSubscriptionPlan.cshtml", model);
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _organizationService.DeleteOrganizationAsync(id, cancellationToken)) return NotFound();
            TempData["Success"] = "Organization deleted.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to delete organization {OrganizationId}.", id);
            TempData["Error"] = "The organization could not be deleted. Check the database connection and try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<OrganizationMembership>> CreateMembershipsAsync(
        OrganizationFormViewModel model,
        IReadOnlyCollection<User> availableUsers,
        int minimumPasswordLength,
        bool requirePasswordDigit,
        CancellationToken cancellationToken)
    {
        if (string.Equals(model.AdminMode, "existing", StringComparison.Ordinal))
        {
            ModelState.Remove(nameof(model.AdminName));
            ModelState.Remove(nameof(model.AdminEmail));
            ModelState.Remove(nameof(model.AdminPassword));
        }
        else if (string.Equals(model.AdminMode, "new", StringComparison.Ordinal))
        {
            ModelState.Remove(nameof(model.AdminUserId));
        }

        var memberships = new List<OrganizationMembership>();
        var usedUserIds = new HashSet<Guid>();
        var usedEmails = availableUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.Email))
            .Select(user => user.Email!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        User? owner = null;
        if (string.Equals(model.AdminMode, "existing", StringComparison.Ordinal))
        {
            if (model.AdminUserId is not Guid adminUserId)
            {
                ModelState.AddModelError(nameof(model.AdminUserId), "Select an existing organization owner/admin.");
            }
            else
            {
                owner = await _userService.GetUserAsync(adminUserId, cancellationToken);
                if (owner is null || !IsAvailableForOrganization(owner))
                {
                    ModelState.AddModelError(nameof(model.AdminUserId), "Choose an active user with sign-in credentials.");
                    owner = null;
                }
            }
        }
        else if (string.Equals(model.AdminMode, "new", StringComparison.Ordinal))
        {
            owner = CreateInlineUser(
                model.AdminName,
                model.AdminEmail,
                model.AdminPassword,
                nameof(model.AdminName),
                nameof(model.AdminEmail),
                nameof(model.AdminPassword),
                usedEmails,
                minimumPasswordLength,
                requirePasswordDigit);
        }
        else
        {
            ModelState.AddModelError(nameof(model.AdminMode), "Choose an existing or new organization owner/admin.");
        }

        if (owner is not null)
        {
            usedUserIds.Add(owner.Id);
            memberships.Add(new OrganizationMembership { User = owner, Role = OrganizationMemberRole.Owner });
        }

        for (var index = 0; index < model.Members.Count; index++)
        {
            var member = model.Members[index];
            var key = $"Members[{index}]";
            if (string.Equals(member.Mode, "existing", StringComparison.Ordinal))
            {
                ModelState.Remove($"{key}.Name");
                ModelState.Remove($"{key}.Email");
                ModelState.Remove($"{key}.Password");
            }
            else if (string.Equals(member.Mode, "new", StringComparison.Ordinal))
            {
                ModelState.Remove($"{key}.UserId");
            }
            if (member.UserId is null
                && string.IsNullOrWhiteSpace(member.Name)
                && string.IsNullOrWhiteSpace(member.Email)
                && string.IsNullOrWhiteSpace(member.Password))
            {
                continue;
            }

            User? user;
            if (string.Equals(member.Mode, "existing", StringComparison.Ordinal))
            {
                user = member.UserId is Guid userId
                    ? await _userService.GetUserAsync(userId, cancellationToken)
                    : null;
                if (user is null || !IsAvailableForOrganization(user))
                {
                    ModelState.AddModelError($"{key}.UserId", "Choose an active user with sign-in credentials.");
                    continue;
                }
            }
            else if (string.Equals(member.Mode, "new", StringComparison.Ordinal))
            {
                user = CreateInlineUser(
                    member.Name,
                    member.Email,
                    member.Password,
                    $"{key}.Name",
                    $"{key}.Email",
                    $"{key}.Password",
                    usedEmails,
                    minimumPasswordLength,
                    requirePasswordDigit);
                if (user is null) continue;
            }
            else
            {
                ModelState.AddModelError($"{key}.Mode", "Choose an existing or new member.");
                continue;
            }

            if (!Enum.IsDefined(member.Role) || member.Role == OrganizationMemberRole.Owner)
            {
                ModelState.AddModelError($"{key}.Role", "Choose a valid member role (Admin, Developer, or Viewer).");
                continue;
            }
            if (!usedUserIds.Add(user.Id))
            {
                ModelState.AddModelError($"{key}.UserId", "A user can only be assigned once to this organization.");
                continue;
            }

            memberships.Add(new OrganizationMembership { User = user, Role = member.Role });
        }

        return memberships;
    }

    private User? CreateInlineUser(
        string? name,
        string? email,
        string? password,
        string nameKey,
        string emailKey,
        string passwordKey,
        HashSet<string> usedEmails,
        int minimumPasswordLength,
        bool requirePasswordDigit)
    {
        if (string.IsNullOrWhiteSpace(name)) ModelState.AddModelError(nameKey, "Full name is required.");
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            ModelState.AddModelError(emailKey, "Enter a valid email address.");
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(passwordKey, "A password is required.");
        }
        else if (password.Length < minimumPasswordLength)
        {
            ModelState.AddModelError(passwordKey, $"The password must be at least {minimumPasswordLength} characters long.");
        }
        else if (requirePasswordDigit && !password.Any(char.IsAsciiDigit))
        {
            ModelState.AddModelError(passwordKey, "The password must contain at least one digit.");
        }

        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(email)
            || !new EmailAddressAttribute().IsValid(email)
            || string.IsNullOrWhiteSpace(password)
            || name.Trim().Length > 200
            || email.Trim().Length > 200
            || password.Length > 128
            || password.Length < minimumPasswordLength
            || requirePasswordDigit && !password.Any(char.IsAsciiDigit))
        {
            return null;
        }

        if (ModelState[nameKey]?.Errors.Count > 0
            || ModelState[emailKey]?.Errors.Count > 0
            || ModelState[passwordKey]?.Errors.Count > 0)
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (!usedEmails.Add(normalizedEmail))
        {
            ModelState.AddModelError(emailKey, "A user with this email already exists.");
            return null;
        }

        var user = new User
        {
            Name = name.Trim(),
            Email = normalizedEmail,
            Role = UserRole.Viewer
        };
        user.LoginId = user.Id.ToString();
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        return user;
    }

    private async Task SetAvailableUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _userService.GetUsersAsync(cancellationToken);
        await SetAvailableUsersAsync(users.Where(IsAvailableForOrganization).ToArray());
    }

    private Task SetAvailableUsersAsync(IReadOnlyCollection<User> users)
    {
        ViewData["AvailableUsers"] = users;
        return Task.CompletedTask;
    }

    private async Task SetAvailablePlansAsync(CancellationToken cancellationToken)
    {
        ViewData["AvailablePlans"] = await _planService.GetPlansAsync(
            activeOnly: true,
            cancellationToken: cancellationToken);
    }

    private async Task PopulateSubscriptionPlanFormAsync(
        Guid id,
        OrganizationSubscriptionPlanViewModel model,
        CancellationToken cancellationToken)
    {
        var organization = await _organizationService.GetOrganizationAsync(id, cancellationToken);
        if (organization is not null)
        {
            model.OrganizationName = organization.Name;
            model.CurrentPlanName = organization.SubscriptionPlanName;
            model.SubscriptionStatus = organization.SubscriptionStatus;
        }

        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Change subscription plan";
        await SetAvailablePlansAsync(cancellationToken);
    }

    private static void ClearPasswords(OrganizationFormViewModel model)
    {
        model.AdminPassword = null;
        foreach (var member in model.Members)
        {
            member.Password = null;
        }
    }

    private static bool IsAvailableForOrganization(User user) =>
        user.Role != UserRole.Superuser
        && user.IsActive
        && !string.IsNullOrWhiteSpace(user.LoginId)
        && !string.IsNullOrWhiteSpace(user.PasswordHash);
}