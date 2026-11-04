using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.Application.Modules.Projects.Services;
using CloudHubs.Web.ViewModels.Projects;

namespace CloudHubs.Web.Areas.Superuser.Controllers;

[Area("Superuser")]
[Route("su/projects")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class ProjectsController : Controller
{
    private readonly PlatformProjectService _projectService;
    private readonly PlatformSettingsService _platformSettingsService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        PlatformProjectService projectService,
        PlatformSettingsService platformSettingsService,
        ILogger<ProjectsController> logger)
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

            return View("Projects", new PlatformProjectIndexViewModel { Projects = projects, Query = normalizedQuery });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Unable to load platform projects.");
            return View("Projects", new PlatformProjectIndexViewModel
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
        return View("PlatformProjectForm", new PlatformProjectFormViewModel
        {
            TemplateId = settings.DefaultTemplateId
        });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(PlatformProjectFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = "Projects";
        ViewData["Title"] = "Create platform project";
        if (!ModelState.IsValid) return View("PlatformProjectForm", model);

        try
        {
            await _projectService.CreateProjectAsync(model.Name, model.Slug, model.Description, model.RootPath, model.TemplateId, model.IsActive, cancellationToken);
            TempData["Success"] = "Platform project created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("PlatformProjectForm", model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unable to create platform project {ProjectName}.", model.Name);
            ModelState.AddModelError(string.Empty, "The project could not be saved. Apply the database migration and check the connection.");
            return View("PlatformProjectForm", model);
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetProjectAsync(id, cancellationToken);
        if (project is null) return NotFound();
        ViewData["Section"] = "Projects";
        ViewData["Title"] = "Edit platform project";
        return View("PlatformProjectForm", new PlatformProjectFormViewModel
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
        if (!ModelState.IsValid) return View("PlatformProjectForm", model);

        try
        {
            var updated = await _projectService.UpdateProjectAsync(id, model.Name, model.Slug, model.Description, model.RootPath, model.TemplateId, model.IsActive, cancellationToken);
            if (updated is null) return NotFound();
            TempData["Success"] = "Platform project updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("PlatformProjectForm", model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unable to update platform project {ProjectId}.", id);
            ModelState.AddModelError(string.Empty, "The project could not be saved. Apply the database migration and check the connection.");
            return View("PlatformProjectForm", model);
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unable to delete platform project {ProjectId}.", id);
            TempData["Error"] = "The project could not be deleted. Check the database connection and try again.";
        }
        return RedirectToAction(nameof(Index));
    }
}
