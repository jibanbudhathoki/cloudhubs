using CloudHubs.Domain.Entities;

namespace CloudHubs.Application.Modules.Platform.Interfaces;

public interface IPlatformSettingsRepository
{
    Task<PlatformSettings> GetAsync(CancellationToken cancellationToken = default);
    Task<PlatformSettings> UpdateAsync(PlatformSettings settings, CancellationToken cancellationToken = default);
}
