using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Platform;

public class PlatformSettingsServiceTests
{
    [Fact]
    public async Task PlatformSettingsRejectUnsafeOrIncompleteValues()
    {
        var service = new PlatformSettingsService(new PlatformSettingsRepositoryStub());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(new PlatformSettings { SessionTimeoutMinutes = 5 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(new PlatformSettings { NotifyOnOrganizationCreated = true }));

        var saved = await service.UpdateAsync(new PlatformSettings { BillingCurrency = "eur" });
        Assert.Equal("EUR", saved.BillingCurrency);
    }

    [Fact]
    public async Task PlatformSettingsSectionUpdateDoesNotValidateOrOverwriteOtherSections()
    {
        var existingSettings = new PlatformSettings
        {
            PlatformName = "Old name",
            NotificationEmail = "invalid-address",
            NotifyOnOrganizationCreated = true,
            DefaultPlanName = "Existing plan"
        };
        var repository = new PlatformSettingsRepositoryStub { Settings = existingSettings };
        var service = new PlatformSettingsService(repository);

        existingSettings.PlatformName = "  CloudHubs  ";
        var saved = await service.UpdateSectionAsync(existingSettings, "profile");

        Assert.Equal("CloudHubs", saved.PlatformName);
        Assert.Equal("invalid-address", saved.NotificationEmail);
        Assert.True(saved.NotifyOnOrganizationCreated);
        Assert.Equal("Existing plan", saved.DefaultPlanName);
    }

    [Fact]
    public async Task PlatformSettingsSectionUpdateValidatesOnlyTheSelectedSection()
    {
        var service = new PlatformSettingsService(new PlatformSettingsRepositoryStub
        {
            Settings = new PlatformSettings { NotificationEmail = "not-an-email" }
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSectionAsync(new PlatformSettings { SupportEmail = "not-an-email" }, "profile"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSectionAsync(new PlatformSettings
            {
                NotifyOnOrganizationCreated = true,
                NotificationEmail = ""
            }, "notifications"));
    }
}
