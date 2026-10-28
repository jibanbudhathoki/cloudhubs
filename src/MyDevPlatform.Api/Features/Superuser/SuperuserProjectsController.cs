using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDevPlatform.Application.Organizations;
using MyDevPlatform.Application.Projects;

namespace MyDevPlatform.Api.Features.Superuser;

[Route("su/projects")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserProjectsController : Controller
{
    private readonly PlatformProjectService _projectService;
    private readonly PlatformSettingsService _platformSettingsService;
    private readonly ILogger<SuperuserProjectsController> _logger;

    public SuperuserProjectsController(
        PlatformProjectService projectService,
        PlatformSettingsService platformSettingsService,
        ILogger<SuperuserProjectsController> logger)
    {
        _projectService = projectService;
        _platformSettingsService = platformSettingsService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? query, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Projects";
        try
        {
            var projects = await _projectService.GetProjectsAsync(cancellationToken);
            var normalizedQuery = query?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                projects = projects.Where(project =>
                    project.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || project.Slug.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || project.TemplateId.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || project.Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            return View("~/Views/superuser/Pages/Projects.cshtml", new PlatformProjectIndexViewModel { Projects = projects, Query = normalizedQuery });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to load platform projects.");
            return View("~/Views/superuser/Pages/Projects.cshtml", new PlatformProjectIndexViewModel
            {
                IsDataAvailable = false,
                ErrorMessage = "Platform project data is unavailable. Apply database migrations and check the database connection."
            });
        }
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Projects";
        ViewData["Title"] = "Create platform project";
        var settings = await _platformSettingsService.GetAsync(cancellationToken);
        return View("~/Views/superuser/Pages/PlatformProjectForm.cshtml", new PlatformProjectFormViewModel
        {
            TemplateId = settings.DefaultTemplateId
        });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(PlatformProjectFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Projects";
        ViewData["Title"] = "Create platform project";
        if (!ModelState.IsValid) return View("~/Views/superuser/Pages/PlatformProjectForm.cshtml", model);

        try
        {
            await _projectService.CreateProjectAsync(model.Name, model.Slug, model.Description, model.RootPath, model.TemplateId, model.IsActive, cancellationToken);
            TempData["Success"] = "Platform project created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("~/Views/superuser/Pages/PlatformProjectForm.cshtml", model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create platform project {ProjectName}.", model.Name);
            ModelState.AddModelError(string.Empty, "The project could not be saved. Apply the database migration and check the connection.");
            return View("~/Views/superuser/Pages/PlatformProjectForm.cshtml", model);
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetProjectAsync(id, cancellationToken);
        if (project is null) return NotFound();
        ViewData["Section"] = "Projects";
        ViewData["Title"] = "Edit platform project";
        return View("~/Views/superuser/Pages/PlatformProjectForm.cshtml", new PlatformProjectFormViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Slug = project.Slug,
            Description = project.Description,
            RootPath = project.RootPath,
            TemplateId = project.TemplateId,
            IsActive = project.IsActive
        });
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, PlatformProjectFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Projects";
        ViewData["Title"] = "Edit platform project";
        model.Id = id;
        if (!ModelState.IsValid) return View("~/Views/superuser/Pages/PlatformProjectForm.cshtml", model);

        try
        {
            var updated = await _projectService.UpdateProjectAsync(id, model.Name, model.Slug, model.Description, model.RootPath, model.TemplateId, model.IsActive, cancellationToken);
            if (updated is null) return NotFound();
            TempData["Success"] = "Platform project updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("~/Views/superuser/Pages/PlatformProjectForm.cshtml", model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update platform project {ProjectId}.", id);
            ModelState.AddModelError(string.Empty, "The project could not be saved. Apply the database migration and check the connection.");
            return View("~/Views/superuser/Pages/PlatformProjectForm.cshtml", model);
        }
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _projectService.DeleteProjectAsync(id, cancellationToken)) return NotFound();
            TempData["Success"] = "Platform project deleted.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to delete platform project {ProjectId}.", id);
            TempData["Error"] = "The project could not be deleted. Check the database connection and try again.";
        }
        return RedirectToAction(nameof(Index));
    }
}