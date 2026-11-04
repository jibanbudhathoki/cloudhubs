using CloudHubs.Application.Modules.Organizations.DTOs;

namespace CloudHubs.Web.ViewModels.Platform;

public record SuperuserDashboardViewModel(
    IReadOnlyCollection<OrganizationSummaryDto> Organizations,
    bool IsApiAvailable,
    string? ErrorMessage,
    string? SearchTerm = null);
