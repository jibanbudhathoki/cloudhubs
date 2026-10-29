using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Organizations;

namespace CloudHubs.Api.Features.Superuser;

[Route("su/plans")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserPlansController(
    SubscriptionPlanService planService,
    ILogger<SuperuserPlansController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Plans";
        try
        {
            return View("~/Views/superuser/Pages/Plans.cshtml", new SubscriptionPlanIndexViewModel
            {
                Plans = await planService.GetPlansAsync(cancellationToken: cancellationToken),
                OrganizationCounts = await planService.GetOrganizationCountsAsync(cancellationToken)
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to load subscription plans.");
            return View("~/Views/superuser/Pages/Plans.cshtml", new SubscriptionPlanIndexViewModel
            {
                IsDataAvailable = false,
                ErrorMessage = "Subscription plans are unavailable. Check the database connection and try again."
            });
        }
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Section"] = "Plans";
        ViewData["Title"] = "Create subscription plan";
        return View("~/Views/superuser/Pages/PlanForm.cshtml", new SubscriptionPlanFormViewModel());
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(SubscriptionPlanFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Plans";
        ViewData["Title"] = "Create subscription plan";
        if (!ModelState.IsValid) return View("~/Views/superuser/Pages/PlanForm.cshtml", model);

        try
        {
            await planService.CreatePlanAsync(model.ToPlan(), cancellationToken);
            TempData["Success"] = "Subscription plan created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("~/Views/superuser/Pages/PlanForm.cshtml", model);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to create subscription plan {PlanName}.", model.Name);
            ModelState.AddModelError(string.Empty, "The plan could not be saved. Check the database connection and try again.");
            return View("~/Views/superuser/Pages/PlanForm.cshtml", model);
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var plan = await planService.GetPlanAsync(id, cancellationToken);
        if (plan is null) return NotFound();
        ViewData["Section"] = "Plans";
        ViewData["Title"] = "Edit subscription plan";
        return View("~/Views/superuser/Pages/PlanForm.cshtml", SubscriptionPlanFormViewModel.From(plan));
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, SubscriptionPlanFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        ViewData["Section"] = "Plans";
        ViewData["Title"] = "Edit subscription plan";
        if (!ModelState.IsValid) return View("~/Views/superuser/Pages/PlanForm.cshtml", model);

        try
        {
            if (await planService.UpdatePlanAsync(id, model.ToPlan(), cancellationToken) is null) return NotFound();
            TempData["Success"] = "Subscription plan updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("~/Views/superuser/Pages/PlanForm.cshtml", model);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to update subscription plan {PlanId}.", id);
            ModelState.AddModelError(string.Empty, "The plan could not be saved. Check the database connection and try again.");
            return View("~/Views/superuser/Pages/PlanForm.cshtml", model);
        }
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await planService.DeletePlanAsync(id, cancellationToken)) return NotFound();
            TempData["Success"] = "Subscription plan deleted.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to delete subscription plan {PlanId}.", id);
            TempData["Error"] = "The plan could not be deleted. Check the database connection and try again.";
        }

        return RedirectToAction(nameof(Index));
    }
}
