using Microsoft.AspNetCore.Identity;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Web.Security;

public sealed class PasswordHashingService(IPasswordHasher<User> passwordHasher) : IPasswordHashingService
{
    public string HashPassword(User user, string password) => passwordHasher.HashPassword(user, password);

    public bool VerifyPassword(User user, string hashedPassword, string providedPassword) =>
        passwordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword) != PasswordVerificationResult.Failed;
}

