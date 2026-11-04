using CloudHubs.Application.Modules.Platform.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class PlatformSettingsRepositoryStub : IPlatformSettingsRepository
{
    public PlatformSettings Settings { get; init; } = new();

    public Task<PlatformSettings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Settings);

    public Task<PlatformSettings> UpdateAsync(PlatformSettings settings, CancellationToken cancellationToken = default) =>
        Task.FromResult(settings);
}
