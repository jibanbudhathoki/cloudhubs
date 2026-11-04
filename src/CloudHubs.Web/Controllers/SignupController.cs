using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.Application.Modules.Subscriptions.Services;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Web.Security;
using CloudHubs.Web.ViewModels.Subscriptions;

namespace CloudHubs.Web.Controllers;

[Route("signup")]
[AutoValidateAntiforgeryToken]
public sealed class SignupController(
    AccountSubscriptionService accountSubscriptionService,
    PlatformSettingsService platformSettingsService,
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    ILogger<SignupController> logger) : Controller
{
    [HttpGet("")]
    [EnableRateLimiting("tenant-login")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View("Index", new SubscriptionSignupViewModel
        {
            Plans = await accountSubscriptionService.GetActivePlansAsync(cancellationToken)
        });
    }

    [HttpPost("")]
    [EnableRateLimiting("tenant-login")]
    public async Task<IActionResult> Index(
        SubscriptionSignupViewModel model,
        CancellationToken cancellationToken)
    {
        model.Plans = await accountSubscriptionService.GetActivePlansAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        try
        {
            var purchase = await accountSubscriptionService.CreateManualPurchaseAsync(
                model.Name,
                model.Email,
                model.PlanId,
                cancellationToken);
            var settings = await platformSettingsService.GetAsync(cancellationToken);
            return View("Pending", new SubscriptionPurchasePendingViewModel
            {
                PurchaseId = purchase.Id,
                Name = purchase.Name,
                Email = purchase.Email,
                PlanName = purchase.PlanName,
                Currency = purchase.Currency,
                Price = purchase.Price,
                BillingInterval = purchase.BillingInterval.ToString(),
                SupportEmail = settings.SupportEmail
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Index", model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to submit a subscription purchase request for {Email}.", model.Email);
            ModelState.AddModelError(string.Empty, "Your purchase request could not be submitted. Please try again later.");
            return View("Index", model);
        }
    }

    [HttpGet("complete")]
    [EnableRateLimiting("tenant-login")]
    public async Task<IActionResult> Complete(Guid purchaseId, string token, CancellationToken cancellationToken)
    {
        if (!await accountSubscriptionService.IsCompletionTokenValidAsync(purchaseId, token, cancellationToken))
        {
            return View("CompletionUnavailable");
        }

        var purchase = await accountSubscriptionService.GetPurchaseAsync(purchaseId, cancellationToken);
        if (purchase is null)
        {
            return View("CompletionUnavailable");
        }

        return View("Complete", new SubscriptionCompletionViewModel
        {
            PurchaseId = purchaseId,
            Token = token,
            Name = purchase.Name,
            Email = purchase.Email,
            ExistingAccount = await userRepository.GetByEmailAsync(purchase.Email, cancellationToken) is not null
        });
    }

    [HttpPost("complete")]
    [EnableRateLimiting("tenant-login")]
    public async Task<IActionResult> Complete(
        SubscriptionCompletionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!await accountSubscriptionService.IsCompletionTokenValidAsync(
                model.PurchaseId, model.Token, cancellationToken))
        {
            return View("CompletionUnavailable");
        }

        var purchase = await accountSubscriptionService.GetPurchaseAsync(model.PurchaseId, cancellationToken);
        if (purchase is null)
        {
            return View("CompletionUnavailable");
        }
        var existingUser = await userRepository.GetByEmailAsync(purchase.Email, cancellationToken);
        model.Name = purchase.Name;
        model.Email = purchase.Email;
        model.ExistingAccount = existingUser is not null;

        if (!model.ExistingAccount
            && !string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
        }
        if (!ModelState.IsValid)
        {
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View("Complete", model);
        }

        var settings = await platformSettingsService.GetAsync(cancellationToken);
        if (!model.ExistingAccount && model.Password.Length < settings.MinimumPasswordLength)
        {
            ModelState.AddModelError(
                nameof(model.Password),
                $"Password must be at least {settings.MinimumPasswordLength} characters long.");
        }
        if (!model.ExistingAccount && settings.RequirePasswordDigit && !model.Password.Any(char.IsAsciiDigit))
        {
            ModelState.AddModelError(nameof(model.Password), "Password must contain at least one digit.");
        }
        if (!model.ExistingAccount && model.Password.Length > 128)
        {
            ModelState.AddModelError(nameof(model.Password), "Password must be 128 characters or fewer.");
        }
        if (!ModelState.IsValid)
        {
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View("Complete", model);
        }

        try
        {
            var passwordHash = model.ExistingAccount
                ? null
                : passwordHasher.HashPassword(new User(), model.Password);
            var accountSubscription = await accountSubscriptionService.CompletePurchaseAsync(
                model.PurchaseId,
                model.Token,
                purchase.Name,
                passwordHash,
                cancellationToken);
            if (accountSubscription is null)
            {
                return View("CompletionUnavailable");
            }

            var user = accountSubscription.OwnerUser;
            await HttpContext.SignInAsync(
                TenantCookieDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                    new Claim(ClaimTypes.Role, "OrganizationMember")
                ], TenantCookieDefaults.AuthenticationScheme)),
                new AuthenticationProperties { IsPersistent = false });

            logger.LogInformation("Account signup completed for user {UserId}.", user.Id);
            TempData["Success"] = "Your paid account is ready. Create your first organization.";
            return Redirect("/organization/create");
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View("Complete", model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to complete account signup for purchase {PurchaseId}.", model.PurchaseId);
            ModelState.AddModelError(string.Empty, "Account setup could not be completed. Please contact support.");
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View("Complete", model);
        }
    }
}
