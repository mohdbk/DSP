using Dsp.Identity.Application;
using Microsoft.AspNetCore.Identity;

namespace Dsp.Identity.Infrastructure.Staff;

/// <summary>PBKDF2 hashing via ASP.NET Core Identity's PasswordHasher.</summary>
public sealed class PasswordService : IPasswordService
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();

    public string Hash(string password) => Hasher.HashPassword(User, password);

    public bool Verify(string hash, string password) =>
        Hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed;
}
