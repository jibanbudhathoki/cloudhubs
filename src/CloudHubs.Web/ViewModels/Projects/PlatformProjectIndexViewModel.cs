using CloudHubs.Domain.Entities;

namespace CloudHubs.Web.ViewModels.Projects;

public class PlatformProjectIndexViewModel
{
    public IReadOnlyCollection<PlatformProject> Projects { get; init; } = [];
    public string? Query { get; init; }
    public bool IsDataAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
}
