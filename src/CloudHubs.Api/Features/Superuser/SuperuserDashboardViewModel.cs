using CloudHubs.Contracts.Organizations;

namespace CloudHubs.Api.Features.Superuser;

public record SuperuserDashboardViewModel(
    IReadOnlyCollection<OrganizationSummaryDto> Organizations,
    bool IsApiAvailable,
    string? ErrorMessage,
    string? SearchTerm = null);