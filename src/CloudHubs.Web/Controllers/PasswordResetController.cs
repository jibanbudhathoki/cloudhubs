using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Web.ViewModels.Auth;

namespace CloudHubs.Web.Controllers;

[AllowAnonymous]
[AutoValidateAntiforgeryToken]
public sealed class PasswordResetController(
    IPasswordResetService passwordResetService,
    IWebHostEnvironment environment,
    ILogger<PasswordResetController> logger) : Controller
{
    [HttpGet("/forgot-password")]
    public IActionResult ForgotPassword()
    {
        return View("ForgotPassword", new ForgotPasswordViewModel());
    }

    [HttpPost("/forgot-password")]
    [EnableRateLimiting("tenant-login")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("ForgotPassword", model);
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await passwordResetService.RequestPasswordResetAsync(
            model.Identifier,
            baseUrl,
            cancellationToken);

        model.IsSubmitted = true;
        model.ConfirmationMessage = result.Message;

        // In Development, surface the generated reset link directly for seamless local testing
        if (environment.IsDevelopment() && !string.IsNullOrEmpty(result.ResetLink))
        {
            model.DevResetLink = result.ResetLink;
        }

        logger.LogInformation("Password reset processed for identifier: {Identifier}", model.Identifier);
        return View("ForgotPassword", model);
    }

    [HttpGet("/reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromQuery] string? email,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return View("ResetPassword", new ResetPasswordViewModel
            {
                IsValidToken = false,
                ErrorMessage = "The password reset link is invalid or missing required parameters."
            });
        }

        var isValid = await passwordResetService.ValidateResetTokenAsync(email, token, cancellationToken);
        if (!isValid)
        {
            return View("ResetPassword", new ResetPasswordViewModel
            {
                Email = email,
                Token = token,
                IsValidToken = false,
                ErrorMessage = "This password reset link has expired or is invalid. Please request a new one."
            });
        }

        return View("ResetPassword", new ResetPasswordViewModel
        {
            Email = email,
            Token = token,
            IsValidToken = true
        });
    }

    [HttpPost("/reset-password")]
    [EnableRateLimiting("tenant-login")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.IsValidToken = true;
            return View("ResetPassword", model);
        }

        var result = await passwordResetService.ResetPasswordAsync(
            model.Email,
            model.Token,
            model.Password,
            model.ConfirmPassword,
            cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Password reset failed.");
            model.IsValidToken = true;
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View("ResetPassword", model);
        }

        TempData["Success"] = "Your password has been reset successfully. Please sign in with your new password.";
        return Redirect("/login");
    }
}

