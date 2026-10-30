using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Organizations;

namespace CloudHubs.Api.Features.Superuser;

[Route("su/purchases")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public sealed class SuperuserPurchasesController(
    AccountSubscriptionService accountSubscriptionService,
    ILogger<SuperuserPurchasesController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Purchases";
        ViewData["Title"] = "Subscription purchase requests";
        try
        {
            return View("~/Views/superuser/Pages/Purchases.cshtml", await accountSubscriptionService.GetPurchasesAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load subscription purchase requests.");
            return Problem("Subscription purchase requests could not be loaded.");
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var token = await accountSubscriptionService.ApprovePurchaseAsync(id, cancellationToken);
            if (token is null)
            {
                TempData["Error"] = "This purchase request is no longer pending.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SignupLink"] = Url.Action(
                "Complete",
                "SubscriptionSignup",
                new { purchaseId = id, token },
                Request.Scheme);
            TempData["Success"] = "Payment approved. Copy the one-time signup link and send it to the customer. It expires in seven days.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to approve subscription purchase {PurchaseId}.", id);
            TempData["Error"] = "The purchase request could not be approved. Try again or check the database connection.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var rejected = await accountSubscriptionService.RejectPurchaseAsync(id, cancellationToken);
            TempData[rejected ? "Success" : "Error"] = rejected
                ? "Purchase request rejected."
                : "This purchase request is no longer pending.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to reject subscription purchase {PurchaseId}.", id);
            TempData["Error"] = "The purchase request could not be rejected.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/reissue-link")]
    public async Task<IActionResult> ReissueSignupLink(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var token = await accountSubscriptionService.ReissueCompletionTokenAsync(id, cancellationToken);
            if (token is null)
            {
                TempData["Error"] = "A signup link can only be reissued for a payment-approved request.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SignupLink"] = Url.Action(
                "Complete",
                "SubscriptionSignup",
                new { purchaseId = id, token },
                Request.Scheme);
            TempData["Success"] = "A new one-time signup link was generated. Any previously issued link is now invalid.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to reissue signup link for purchase {PurchaseId}.", id);
            TempData["Error"] = "The signup link could not be reissued.";
        }

        return RedirectToAction(nameof(Index));
    }
}
