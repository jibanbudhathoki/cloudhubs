using CloudHubs.Domain.Projects;

namespace CloudHubs.Application.Projects;

public class PlatformProjectService
{
    private readonly IPlatformProjectRepository _repository;

    public PlatformProjectService(IPlatformProjectRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyCollection<PlatformProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<PlatformProject?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<PlatformProject> CreateProjectAsync(string name, string slug, string description, string rootPath, string templateId, bool isActive, CancellationToken cancellationToken = default)
    {
        var normalized = Validate(name, slug, description, rootPath, templateId);
        if (await _repository.SlugExistsAsync(normalized.Slug, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("That platform project slug is already in use.");
        }

        return await _repository.AddAsync(new PlatformProject
        {
            Name = normalized.Name,
            Slug = normalized.Slug,
            Description = normalized.Description,
            RootPath = normalized.RootPath,
            TemplateId = normalized.TemplateId,
            IsActive = isActive
        }, cancellationToken);
    }

    public async Task<PlatformProject?> UpdateProjectAsync(Guid id, string name, string slug, string description, string rootPath, string templateId, bool isActive, CancellationToken cancellationToken = default)
    {
        var normalized = Validate(name, slug, description, rootPath, templateId);
        var project = await _repository.GetByIdAsync(id, cancellationToken);
        if (project is null) return null;
        if (await _repository.SlugExistsAsync(normalized.Slug, id, cancellationToken))
        {
            throw new InvalidOperationException("That platform project slug is already in use.");
        }

        project.Name = normalized.Name;
        project.Slug = normalized.Slug;
        project.Description = normalized.Description;
        project.RootPath = normalized.RootPath;
        project.TemplateId = normalized.TemplateId;
        project.IsActive = isActive;
        project.UpdatedAt = DateTime.UtcNow;
        return await _repository.UpdateAsync(project, cancellationToken);
    }

    public Task<bool> DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _repository.DeleteAsync(id, cancellationToken);
    }

    private static (string Name, string Slug, string Description, string RootPath, string TemplateId) Validate(
        string name,
        string slug,
        string description,
        string rootPath,
        string templateId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Project name is required.", nameof(name));
        var normalizedSlug = NormalizeSlug(string.IsNullOrWhiteSpace(slug) ? name : slug);
        if (string.IsNullOrWhiteSpace(normalizedSlug)) throw new ArgumentException("Project slug is required.", nameof(slug));
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Project root path is required.", nameof(rootPath));
        if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("Project template is required.", nameof(templateId));
        return (name.Trim(), normalizedSlug, description.Trim(), rootPath.Trim(), templateId.Trim());
    }

    private static string NormalizeSlug(string value)
    {
        var slug = string.Concat(value.Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-'));
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }
}