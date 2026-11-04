using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class UserRepositoryStub : IUserRepository
{
    public User? AddedUser { get; private set; }
    public User? ExistingUser { get; init; }
    public IReadOnlyCollection<User> Users { get; init; } = [];

    public Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Users);
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult((User?)new[] { AddedUser, ExistingUser }.Concat(Users).FirstOrDefault(user => user?.Id == id));
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult((User?)new[] { AddedUser, ExistingUser }.Concat(Users)
            .FirstOrDefault(user => string.Equals(user?.Email, email, StringComparison.OrdinalIgnoreCase)));
    public Task<User?> GetByLoginIdAsync(string loginId, CancellationToken cancellationToken = default) =>
        Task.FromResult((User?)new[] { AddedUser, ExistingUser }.Concat(Users)
            .FirstOrDefault(user => string.Equals(user?.LoginId, loginId, StringComparison.OrdinalIgnoreCase)));
    public Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        AddedUser = user;
        return Task.FromResult(user);
    }

    public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.FromResult(user);
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
