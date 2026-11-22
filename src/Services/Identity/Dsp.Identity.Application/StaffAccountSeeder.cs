using Dsp.Identity.Domain;
using Microsoft.Extensions.Logging;

namespace Dsp.Identity.Application;

/// <summary>Creates the initial back-office accounts on first start, if none exist.</summary>
public sealed class StaffAccountSeeder(
    IStaffAccountRepository accounts,
    IPasswordService passwords,
    ILogger<StaffAccountSeeder> logger)
{
    public async Task SeedAsync(string? initialPassword, CancellationToken ct)
    {
        if (await accounts.AnyAsync(ct)) return;
        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            logger.LogWarning("No staff accounts exist and Staff:InitialPassword is not set; skipping seed.");
            return;
        }

        var hash = passwords.Hash(initialPassword);
        await accounts.AddAsync(StaffAccount.Create("khalid", "Khalid Al Hammadi", "Licensing Officer",
            "khalid.alhammadi@example.gov.ae", hash), ct);
        await accounts.AddAsync(StaffAccount.Create("noura", "Noura Al Ketbi", "Senior Reviewer",
            "noura.alketbi@example.gov.ae", hash), ct);
        await accounts.SaveChangesAsync(ct);
        logger.LogInformation("Seeded initial staff accounts");
    }
}
