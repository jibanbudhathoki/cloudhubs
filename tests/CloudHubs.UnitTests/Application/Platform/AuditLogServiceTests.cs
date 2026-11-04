using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Platform;

public class AuditLogServiceTests
{
    [Fact]
    public async Task AuditLogServicePersistsEventData()
    {
        var repository = new AuditLogRepositoryStub();
        var service = new AuditLogService(repository);

        await service.RecordAsync("actor-id", "Superuser", "POST /su/users/create", "users", "", null, "Response status: 302.");

        Assert.Equal("Superuser", repository.AddedEntry?.ActorName);
        Assert.Equal("POST /su/users/create", repository.AddedEntry?.Action);
        Assert.Equal("Response status: 302.", repository.AddedEntry?.Details);
    }
}
