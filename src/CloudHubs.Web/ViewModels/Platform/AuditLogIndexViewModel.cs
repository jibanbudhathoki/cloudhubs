using CloudHubs.Application.Modules.Organizations.DTOs;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Web.ViewModels.Platform;

public class AuditLogIndexViewModel
{
    public IReadOnlyCollection<AuditLogEntry> Entries { get; init; } = [];
    public IReadOnlyCollection<OrganizationSummaryDto> Organizations { get; init; } = [];
    public string? Query { get; init; }
    public Guid? OrganizationId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}
