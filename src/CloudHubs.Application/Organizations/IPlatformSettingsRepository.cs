using CloudHubs.Domain.Organizations;

namespace CloudHubs.Application.Organizations;

public interface IPlatformSettingsRepository
{
    Task<PlatformSettings> GetAsync(CancellationToken cancellationToken = default);
    Task<PlatformSettings> UpdateAsync(PlatformSettings settings, CancellationToken cancellationToken = default);
}
