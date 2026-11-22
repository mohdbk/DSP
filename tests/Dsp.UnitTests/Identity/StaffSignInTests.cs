using Dsp.Identity.Application;
using Dsp.Identity.Domain;
using Dsp.Identity.Infrastructure.Staff;

namespace Dsp.UnitTests.Identity;

public class StaffAccountTests
{
    [Fact]
    public void Account_locks_after_repeated_failures_and_unlocks_after_the_lockout()
    {
        var account = StaffAccount.Create("Khalid ", "Khalid", "Officer", "k@example.gov.ae", "hash");
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < StaffAccount.MaxFailedAttempts; i++) account.RecordFailedAttempt(now);

        Assert.True(account.IsLockedOut(now));
        Assert.False(account.IsLockedOut(now + StaffAccount.LockoutDuration + TimeSpan.FromSeconds(1)));
        Assert.Equal("khalid", account.Username);
    }

    [Fact]
    public void Successful_sign_in_resets_failures()
    {
        var account = StaffAccount.Create("noura", "Noura", "Reviewer", "n@example.gov.ae", "hash");
        account.RecordFailedAttempt(DateTimeOffset.UtcNow);
        account.RecordSignIn(DateTimeOffset.UtcNow);

        Assert.Equal(0, account.FailedAttempts);
        Assert.NotNull(account.LastSignInAt);
    }

    [Fact]
    public void Staff_map_to_the_staff_role_with_a_local_provider()
    {
        var user = StaffAccount.Create("noura", "Noura Al Ketbi", "Senior Reviewer", "n@example.gov.ae", "hash").ToPlatformUser();

        Assert.Equal(PlatformRole.Staff, user.Role);
        Assert.Equal("staff:noura", user.Subject);
        Assert.Equal("local", user.Provider);
    }
}

public class LocalStaffAuthenticatorTests
{
    private readonly PasswordService _passwords = new();
    private readonly Accounts _accounts = new();
    private readonly LocalStaffAuthenticator _authenticator;

    public LocalStaffAuthenticatorTests()
    {
        _accounts.Items.Add(StaffAccount.Create("khalid", "Khalid Al Hammadi", "Licensing Officer",
            "k@example.gov.ae", _passwords.Hash("Correct-Horse-1")));
        _authenticator = new LocalStaffAuthenticator(_accounts, _passwords);
    }

    [Fact]
    public async Task Correct_password_signs_in()
    {
        var user = await _authenticator.AuthenticateAsync(" KHALID ", "Correct-Horse-1", default);

        Assert.NotNull(user);
        Assert.Equal("staff:khalid", user.Subject);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_are_rejected()
    {
        Assert.Null(await _authenticator.AuthenticateAsync("khalid", "wrong", default));
        Assert.Null(await _authenticator.AuthenticateAsync("nobody", "Correct-Horse-1", default));
        Assert.Equal(1, _accounts.Items[0].FailedAttempts);
    }

    [Fact]
    public async Task Locked_account_is_rejected_even_with_the_right_password()
    {
        for (var i = 0; i < StaffAccount.MaxFailedAttempts; i++)
            await _authenticator.AuthenticateAsync("khalid", "wrong", default);

        Assert.Null(await _authenticator.AuthenticateAsync("khalid", "Correct-Horse-1", default));
    }

    [Fact]
    public void Passwords_are_hashed_not_stored()
    {
        var hash = _passwords.Hash("secret-value");

        Assert.DoesNotContain("secret-value", hash);
        Assert.True(_passwords.Verify(hash, "secret-value"));
        Assert.False(_passwords.Verify(hash, "other"));
    }

    private sealed class Accounts : IStaffAccountRepository
    {
        public List<StaffAccount> Items { get; } = [];
        public Task<StaffAccount?> FindByUsernameAsync(string username, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(a => a.Username == username));
        public Task<bool> AnyAsync(CancellationToken ct) => Task.FromResult(Items.Count > 0);
        public Task AddAsync(StaffAccount account, CancellationToken ct) { Items.Add(account); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
