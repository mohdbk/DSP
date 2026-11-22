using Dsp.Identity.Application;
using Dsp.Identity.Domain;

namespace Dsp.Identity.Infrastructure.Staff;

/// <summary>Username and password accounts stored in the Identity database. AD replaces this later.</summary>
public sealed class LocalStaffAuthenticator(IStaffAccountRepository accounts, IPasswordService passwords)
    : IStaffAuthenticator
{
    public string Name => "local";

    public async Task<PlatformUser?> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        var account = await accounts.FindByUsernameAsync(username.Trim().ToLowerInvariant(), ct);
        var now = DateTimeOffset.UtcNow;

        if (account is null || !account.IsActive)
        {
            passwords.Verify(DummyHash.Value, password); // keep timing similar for unknown users
            return null;
        }

        if (account.IsLockedOut(now)) return null;

        if (!passwords.Verify(account.PasswordHash, password))
        {
            account.RecordFailedAttempt(now);
            await accounts.SaveChangesAsync(ct);
            return null;
        }

        account.RecordSignIn(now);
        await accounts.SaveChangesAsync(ct);
        return account.ToPlatformUser();
    }

    private static class DummyHash
    {
        public static readonly string Value = new PasswordService().Hash(Guid.NewGuid().ToString());
    }
}
