using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using MyDevPlatform.Application.Organizations;
using System.Security.Claims;

namespace MyDevPlatform.Api.Features.Superuser;

[Route("su")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserController : Controller
{
    private readonly OrganizationService _organizationService;
    private readonly ILogger<SuperuserController> _logger;
    private readonly SuperuserCredentialValidator _credentialValidator;
    private readonly PlatformSettingsService _platformSettingsService;

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

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Settings";
        ViewData["Title"] = "Settings";
        var settings = await _platformSettingsService.GetAsync(cancellationToken);
        return View("~/Views/superuser/Pages/Settings.cshtml", PlatformSettingsViewModel.From(settings));
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Settings(PlatformSettingsViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Settings";
        ViewData["Title"] = "Settings";
        if (!ModelState.IsValid)
        {
            return View("~/Views/superuser/Pages/Settings.cshtml", model);
        }

        try
        {
            await _platformSettingsService.UpdateAsync(model.ToSettings(), cancellationToken);
            TempData["Success"] = "Platform settings saved.";
            return RedirectToAction(nameof(Settings));
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