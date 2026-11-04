using CloudHubs.Application.Modules.Users.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Users;

public class UserServiceTests
{
    [Fact]
    public async Task UserCreationNormalizesEmailAndValidatesRole()
    {
        var repository = new UserRepositoryStub();
        var service = new UserService(repository);

        await service.CreateUserAsync(
            "Platform User",
            "USER@EXAMPLE.COM",
            "hashed-password",
            "Developer",
            true);

        Assert.Equal("user@example.com", repository.AddedUser?.Email);
        Assert.Equal(repository.AddedUser?.Id.ToString(), repository.AddedUser?.LoginId);
        Assert.Equal("hashed-password", repository.AddedUser?.PasswordHash);
        Assert.Equal(UserRole.Developer, repository.AddedUser?.Role);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateUserAsync(
                "Invalid Role",
                "invalid@example.com",
                "hashed-password",
                "Superuser",
                true));
    }

    [Fact]
    public async Task SuperuserAccountsCannotBeManagedThroughRegularUserCrud()
    {
        var superuser = new User
        {
            Name = "Platform Superuser",
            Email = "admin@example.com",
            Role = UserRole.Superuser
        };
        var repository = new UserRepositoryStub
        {
            ExistingUser = superuser
        };
        var service = new UserService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateUserAsync(
                "Another Superuser",
                "other@example.com",
                "hashed-password",
                "Superuser",
                true));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(superuser.Id, "Platform Superuser", "admin@example.com", "Viewer", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteUserAsync(superuser.Id));
    }
}
