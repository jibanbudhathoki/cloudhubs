using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Organizations;
using CloudHubs.Application.Users;
using CloudHubs.Domain.Users;

namespace CloudHubs.Api.Features.Superuser;

[Route("su/users")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserUsersController : Controller
{
    private readonly UserService _userService;
    private readonly ILogger<SuperuserUsersController> _logger;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly PlatformSettingsService _platformSettingsService;

    public SuperuserUsersController(
        UserService userService,
        ILogger<SuperuserUsersController> logger,
        IPasswordHasher<User> passwordHasher,
        PlatformSettingsService platformSettingsService)
    {
        _userService = userService;
        _logger = logger;
        _passwordHasher = passwordHasher;
        _platformSettingsService = platformSettingsService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? query, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Users";
        try
        {
            var users = await _userService.GetUsersAsync(cancellationToken);
            var normalizedQuery = query?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                users = users.Where(user =>
                    user.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || (user.Email?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (user.LoginId?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                    || user.Role.ToString().Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            return View("~/Views/superuser/Pages/Users.cshtml", new UserIndexViewModel { Users = users, Query = normalizedQuery });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to load platform users.");
            return View("~/Views/superuser/Pages/Users.cshtml", new UserIndexViewModel
            {
                IsDataAvailable = false,
                ErrorMessage = "User data is unavailable. Check the database connection and try again."
            });
        }
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Section"] = "Users";
        ViewData["Title"] = "Create user";
        return View("~/Views/superuser/Pages/UserForm.cshtml", new UserFormViewModel());
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(UserFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Users";
        ViewData["Title"] = "Create user";
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "A password is required.");
        }
        if (!ModelState.IsValid) return View("~/Views/superuser/Pages/UserForm.cshtml", ClearPassword(model));

        try
        {
            var settings = await _platformSettingsService.GetAsync(cancellationToken);
            if (model.Password.Length < settings.MinimumPasswordLength)
            {
                ModelState.AddModelError(
                    nameof(model.Password),
                    $"The password must be at least {settings.MinimumPasswordLength} characters long.");
                return View("~/Views/superuser/Pages/UserForm.cshtml", ClearPassword(model));
            }
            if (settings.RequirePasswordDigit && !model.Password.Any(char.IsAsciiDigit))
            {
                ModelState.AddModelError(nameof(model.Password), "The password must contain at least one digit.");
                return View("~/Views/superuser/Pages/UserForm.cshtml", ClearPassword(model));
            }

            var passwordHash = _passwordHasher.HashPassword(new User(), model.Password);
            await _userService.CreateUserAsync(
                model.Name,
                model.Email,
                passwordHash,
                model.Role,
                model.IsActive,
                cancellationToken);
            TempData["Success"] = "User created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("~/Views/superuser/Pages/UserForm.cshtml", ClearPassword(model));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create platform user {UserEmail}.", model.Email);
            ModelState.AddModelError(string.Empty, "The user could not be saved. Check the database connection and try again.");
            return View("~/Views/superuser/Pages/UserForm.cshtml", ClearPassword(model));
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserAsync(id, cancellationToken);
        if (user is null) return NotFound();
        if (user.Role == UserRole.Superuser) return Forbid();
        ViewData["Section"] = "Users";
        ViewData["Title"] = "Edit user";
        return View("~/Views/superuser/Pages/UserForm.cshtml", new UserFormViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        });
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, UserFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Users";
        ViewData["Title"] = "Edit user";
        model.Id = id;
        var existingUser = await _userService.GetUserAsync(id, cancellationToken);
        if (existingUser is null) return NotFound();
        if (existingUser.Role == UserRole.Superuser) return Forbid();
        if (!ModelState.IsValid) return View("~/Views/superuser/Pages/UserForm.cshtml", model);

        try
        {
            var updated = await _userService.UpdateUserAsync(id, model.Name, model.Email, model.Role, model.IsActive, cancellationToken);
            if (updated is null) return NotFound();
            TempData["Success"] = "User updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("~/Views/superuser/Pages/UserForm.cshtml", model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update platform user {UserId}.", id);
            ModelState.AddModelError(string.Empty, "The user could not be saved. Check the database connection and try again.");
            return View("~/Views/superuser/Pages/UserForm.cshtml", model);
        }
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _userService.DeleteUserAsync(id, cancellationToken)) return NotFound();
            TempData["Success"] = "User deleted.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to delete platform user {UserId}.", id);
            TempData["Error"] = "The user could not be deleted. Check the database connection and try again.";
        }
        return RedirectToAction(nameof(Index));
    }

    private static UserFormViewModel ClearPassword(UserFormViewModel model)
    {
        model.Password = string.Empty;
        return model;
    }
}