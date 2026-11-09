using CloudHubs.Domain.Entities;

namespace CloudHubs.Application.Modules.Users.Interfaces;

public interface IPasswordHashingService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string hashedPassword, string providedPassword);
}

