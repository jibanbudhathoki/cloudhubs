using Microsoft.EntityFrameworkCore;
using MyDevPlatform.Application.Users;
using MyDevPlatform.Domain.Users;

namespace MyDevPlatform.Infrastructure.Persistence;

public class UserRepository : IUserRepository
{
    private readonly MyDevPlatformDbContext _dbContext;

    public UserRepository(MyDevPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.AsNoTracking().OrderBy(user => user.Name).ToListAsync(cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AnyAsync(
            user => user.Email != null
                && user.Email.ToLower() == email
                && (!exceptId.HasValue || user.Id != exceptId.Value),
            cancellationToken);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync([id], cancellationToken);
        if (user is null) return false;
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}