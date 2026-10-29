using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using CloudHubs.Application.Organizations;
using System.Security.Claims;

namespace CloudHubs.Api.Features.Superuser;

[Route("su")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserController : Controller
{
    private readonly OrganizationService _organizationService;
    private readonly ILogger<SuperuserController> _logger;
    private readonly SuperuserCredentialValidator _credentialValidator;
    private readonly PlatformSettingsService _platformSettingsService;

    private static readonly string[] SettingSections =
        ["profile", "security", "organizations", "catalog", "billing", "notifications"];

    private static readonly IReadOnlyDictionary<string, string> SettingSectionTitles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profile"] = "Platform profile",
            ["security"] = "Security",
            ["organizations"] = "Organization defaults",
            ["catalog"] = "Catalog defaults",
            ["billing"] = "Billing defaults",
            ["notifications"] = "Notifications"
        };

    private static readonly IReadOnlyDictionary<string, string[]> SettingSectionFields =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["profile"] = ["PlatformName", "SupportEmail", "PublicUrl"],
            ["security"] = ["SessionTimeoutMinutes", "JwtLifetimeMinutes", "MinimumPasswordLength", "RequirePasswordDigit"],
            ["organizations"] = ["AllowOrganizationCreation", "DefaultOrganizationStatus", "DefaultTrialDays"],
            ["catalog"] = ["DefaultTemplateId"],
            ["billing"] = ["DefaultPlanName", "BillingCurrency", "MonthlyPrice"],
            ["notifications"] = ["NotifyOnOrganizationCreated", "NotificationEmail"]
        };

    public SuperuserController(
        OrganizationService organizationService,
        ILogger<SuperuserController> logger,
        SuperuserCredentialValidator credentialValidator,
        PlatformSettingsService platformSettingsService)
    {
        _organizationService = organizationService;
        _logger = logger;
        _credentialValidator = credentialValidator;
        _platformSettingsService = platformSettingsService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await GetDashboardModel(cancellationToken);
        ViewData["Section"] = "Overview";
        return View("~/Views/superuser/Pages/Dashboard.cshtml", model);
    }

    [HttpGet("settings/{section?}")]
    public async Task<IActionResult> Settings(string? section, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Settings";
        if (section is not null && !SettingSections.Contains(section))
        {
            return NotFound();
        }

        ViewData["Title"] = section is null ? "Settings" : SettingSectionTitles[section];
        ViewData["SettingsSection"] = section;
        if (section == "security")
        {
            ViewData["SuperuserEmail"] = await _credentialValidator.GetSuperuserEmailAsync(GetCurrentSuperuserId(), cancellationToken);
        }

        var settings = await _platformSettingsService.GetAsync(cancellationToken);
        return View("~/Views/superuser/Pages/Settings.cshtml", PlatformSettingsViewModel.From(settings));
    }

    [HttpGet("settings/security/account")]
    public async Task<IActionResult> AccountSettings(CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Settings";
        ViewData["Title"] = "Superadmin account";
        ViewData["SuperuserEmail"] = await _credentialValidator.GetSuperuserEmailAsync(GetCurrentSuperuserId(), cancellationToken);
        return View("~/Views/superuser/Pages/SuperuserAccountSettings.cshtml", new SuperuserAccountChangeViewModel());
    }

    [HttpPost("settings/security/account")]
    public async Task<IActionResult> SaveAccountSettings(
        SuperuserAccountChangeViewModel model,
        CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Settings";
        ViewData["Title"] = "Superadmin account";
        var userId = GetCurrentSuperuserId();
        ViewData["SuperuserEmail"] = await _credentialValidator.GetSuperuserEmailAsync(userId, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View("~/Views/superuser/Pages/SuperuserAccountSettings.cshtml", model);
        }

        try
        {
            var passwordChanged = await _credentialValidator.UpdateAccountAsync(
                userId,
                model.CurrentPassword,
                model.NewEmail,
                model.NewPassword,
                model.ConfirmNewPassword,
                cancellationToken);
            TempData["Success"] = "Superadmin account updated.";
            if (passwordChanged)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            return RedirectToAction(nameof(AccountSettings));
        }
        catch (UnauthorizedAccessException exception)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), exception.Message);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update the Superadmin account.");
            ModelState.AddModelError(string.Empty, "The Superadmin account could not be updated. Check the database connection and try again.");
        }

        return View("~/Views/superuser/Pages/SuperuserAccountSettings.cshtml", model);
    }

    [HttpPost("settings/{section}")]
    public async Task<IActionResult> SaveSettings(string section, PlatformSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!SettingSections.Contains(section))
        {
            return NotFound();
        }

        ViewData["Section"] = "Settings";
        ViewData["Title"] = SettingSectionTitles[section];
        ViewData["SettingsSection"] = section;
        if (!SettingSectionFields.TryGetValue(section, out var sectionFields))
        {
            return NotFound();
        }

        var preservedBindingErrors = new List<(string Key, string Message)>();
        foreach (var entry in ModelState)
        {
            if (!sectionFields.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var error in entry.Value.Errors)
            {
                if (error.Exception is not null)
                {
                    preservedBindingErrors.Add((entry.Key, error.Exception.Message));
                }
            }
        }

        ModelState.Clear();
        foreach (var (key, message) in preservedBindingErrors)
        {
            ModelState.AddModelError(key, message);
        }

        ValidateSettingsSection(model, sectionFields);

        if (!ModelState.IsValid)
        {
            return View("~/Views/superuser/Pages/Settings.cshtml", model);
        }

        try
        {
            var settings = await _platformSettingsService.GetAsync(cancellationToken);
            model.ApplySection(settings, section);
            await _platformSettingsService.UpdateSectionAsync(settings, section, cancellationToken);
            TempData["Success"] = $"{SettingSectionTitles[section]} settings saved.";
            return RedirectToAction(nameof(Settings), new { section });
        }

        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("~/Views/superuser/Pages/Settings.cshtml", model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to save platform settings.");
            ModelState.AddModelError(string.Empty, "Settings could not be saved. Check the database connection and try again.");
            return View("~/Views/superuser/Pages/Settings.cshtml", model);
        }
    }

    private void ValidateSettingsSection(PlatformSettingsViewModel model, IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            var property = typeof(PlatformSettingsViewModel).GetProperty(field);
            if (property is null)
            {
                throw new InvalidOperationException($"Settings field '{field}' is not defined on the view model.");
            }

            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(model) { MemberName = field };
            Validator.TryValidateProperty(property.GetValue(model), validationContext, validationResults);
            foreach (var validationResult in validationResults)
            {
                ModelState.AddModelError(field, validationResult.ErrorMessage ?? $"{field} is invalid.");
            }
        }

    }

    private Guid GetCurrentSuperuserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new InvalidOperationException("The authenticated Superadmin identity is invalid. Sign in again.");
        }

        return parsedUserId;
    }

    [AllowAnonymous]
    [HttpGet("/su/login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var model = new SuperuserLoginViewModel { ReturnUrl = returnUrl };
        return View("~/Views/superuser/Pages/Login.cshtml", model);
    }

    [AllowAnonymous]
    [HttpPost("/su/login")]
    public async Task<IActionResult> Login(SuperuserLoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/superuser/Pages/Login.cshtml", model);
        }

        var user = await _credentialValidator.ValidateAsync(model.Id, model.Password, HttpContext.RequestAborted);
        if (user is null)
        {
            model.ErrorMessage = "Login ID or password is incorrect.";
            return View("~/Views/superuser/Pages/Login.cshtml", model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.LoginId ?? user.Email ?? user.Id.ToString()),
            new Claim(ClaimTypes.Role, "Superuser")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl!)
            : RedirectToAction(nameof(Index));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task<SuperuserDashboardViewModel> GetDashboardModel(CancellationToken cancellationToken)
    {
        try
        {
            var organizations = await _organizationService.GetOrganizationsAsync(cancellationToken);
            return new SuperuserDashboardViewModel(organizations, true, null);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to load organizations for the Superuser dashboard.");
            var message = "Organization data is unavailable. Check the database connection and try again.";
            return new SuperuserDashboardViewModel([], false, message);
        }
    }
}

[Route("super-admin")]
[AllowAnonymous]
public class SuperuserLegacyRoutesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => Redirect("/su/");

    [HttpGet("login")]
    public IActionResult Login() => Redirect("/su/login");

    [HttpGet("organization")]
    public IActionResult Organizations() => Redirect("/su/organization");

    [HttpGet("projects")]
    public IActionResult Projects() => Redirect("/su/projects");

    [HttpGet("users")]
    public IActionResult Users() => Redirect("/su/users");
}