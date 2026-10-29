using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Organizations;
using CloudHubs.Domain.Organizations;

namespace CloudHubs.Infrastructure.Persistence;

public sealed class PlatformSettingsRepository(CloudHubsDbContext dbContext) : IPlatformSettingsRepository
{
    public async Task<PlatformSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.PlatformSettings.SingleOrDefaultAsync(
            item => item.Id == PlatformSettings.SingletonId,
            cancellationToken);
        if (settings is not null)
            return settings;

        settings = new PlatformSettings();
        dbContext.PlatformSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<PlatformSettings> UpdateAsync(PlatformSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.Id != PlatformSettings.SingletonId)
            throw new InvalidOperationException("Only the platform settings record can be updated.");

        dbContext.PlatformSettings.Update(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
