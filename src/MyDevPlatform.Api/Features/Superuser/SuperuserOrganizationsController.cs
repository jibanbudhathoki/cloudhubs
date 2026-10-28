using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDevPlatform.Application.Organizations;
using MyDevPlatform.Contracts.Organizations;

namespace MyDevPlatform.Api.Features.Superuser;

[Route("su/organization")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserOrganizationsController : Controller
{
    private readonly OrganizationService _organizationService;
    private readonly OrganizationCreatedNotificationSender _notificationSender;
    private readonly ILogger<SuperuserOrganizationsController> _logger;

    public SuperuserOrganizationsController(
        OrganizationService organizationService,
        OrganizationCreatedNotificationSender notificationSender,
        ILogger<SuperuserOrganizationsController> logger)
    {
        _organizationService = organizationService;
        _notificationSender = notificationSender;
        _logger = logger;
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
    public IActionResult Create()
    {
        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Create organization";
        return View("~/Views/superuser/Pages/OrganizationForm.cshtml", new OrganizationFormViewModel());
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(OrganizationFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Create organization";
        if (!ModelState.IsValid) return View("~/Views/superuser/Pages/OrganizationForm.cshtml", model);

        try
        {
            var organization = await _organizationService.CreateOrganizationAsync(
                new CreateOrganizationRequest(model.Name, model.Email, model.Slug, string.Empty, string.Empty),
                cancellationToken);
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
            return View("~/Views/superuser/Pages/OrganizationForm.cshtml", model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create organization {OrganizationName}.", model.Name);
            ModelState.AddModelError(string.Empty, "The organization could not be saved. Check the database connection and try again.");
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
}