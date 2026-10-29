using CloudHubs.Application.Users;
using CloudHubs.Domain.Users;

namespace CloudHubs.Application.Users;

public class UserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyCollection<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<User> CreateUserAsync(
        string name,
        string email,
        string passwordHash,
        string role,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var normalized = Validate(name, email, role);
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A password is required.", nameof(passwordHash));
        }
        if (await _repository.EmailExistsAsync(normalized.Email, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }
        var user = new User
        {
            Name = normalized.Name,
            Email = normalized.Email,
            PasswordHash = passwordHash,
            Role = normalized.Role,
            IsActive = isActive
        };
        user.LoginId = user.Id.ToString();

        return await _repository.AddAsync(user, cancellationToken);
    }

    public async Task<User?> UpdateUserAsync(Guid id, string name, string email, string role, bool isActive, CancellationToken cancellationToken = default)
    {
        var normalized = Validate(name, email, role);
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        if (user is null) return null;
        if (user.Role == UserRole.Superuser)
        {
            throw new InvalidOperationException("Superuser accounts are managed through the configured bootstrap credentials.");
        }
        if (await _repository.EmailExistsAsync(normalized.Email, id, cancellationToken))
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        user.Name = normalized.Name;
        user.Email = normalized.Email;
        user.Role = normalized.Role;
        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        return await _repository.UpdateAsync(user, cancellationToken);
    }

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        if (user is null) return false;
        if (user.Role == UserRole.Superuser)
        {
            throw new InvalidOperationException("Superuser accounts are managed through the configured bootstrap credentials.");
        }

        return await _repository.DeleteAsync(id, cancellationToken);
    }

    private static (string Name, string Email, UserRole Role) Validate(string name, string email, string role)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("User name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ArgumentException("A valid email is required.", nameof(email));
        if (!Enum.TryParse<UserRole>(role, true, out var parsedRole) || !Enum.IsDefined(parsedRole))
        {
            throw new ArgumentException("Choose a valid user role.", nameof(role));
        }
        if (parsedRole == UserRole.Superuser)
        {
            throw new ArgumentException("Superuser accounts are provisioned through the configured bootstrap credentials.", nameof(role));
        }

        return (name.Trim(), email.Trim().ToLowerInvariant(), parsedRole);
    }

}