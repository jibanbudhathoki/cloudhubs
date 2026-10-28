using Microsoft.EntityFrameworkCore;
using MyDevPlatform.Application.Organizations;
using MyDevPlatform.Domain.Organizations;

namespace MyDevPlatform.Infrastructure.Persistence;

public sealed class PlatformSettingsRepository(MyDevPlatformDbContext dbContext) : IPlatformSettingsRepository
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
