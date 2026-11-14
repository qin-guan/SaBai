using Microsoft.EntityFrameworkCore;
using SaBai.API.Data;

namespace SaBai.API.Sync;

/// <summary>
/// Ensures the Postgres logical replication publication used by PowerSync exists and
/// includes every synced clinic table. Safe to run on every startup.
/// </summary>
public static class PowerSyncPublication
{
    public const string PublicationName = "powersync";

    public static async Task EnsureAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        if (!db.Database.IsNpgsql())
        {
            return;
        }

        const string publicationSql = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'powersync') THEN
                    CREATE PUBLICATION powersync FOR TABLE
                        "VillageCodes",
                        "StaffUsers",
                        "Patients",
                        "Visits",
                        "Vitals",
                        "PubertyAssessments",
                        "ChildVitals",
                        "Eyesights",
                        "DentalConsults",
                        "Consults",
                        "Diagnoses",
                        "ReferralPartners",
                        "Referrals",
                        "Tags",
                        "PatientTags",
                        "MedicationActiveIngredients",
                        "MedicationBrands",
                        "MedicationStocks",
                        "StockChanges",
                        "Orders",
                        "Files";
                END IF;
            END
            $$;
            """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(publicationSql, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure PowerSync publication.");
        }

        try
        {
            var exists = await db
                .Database.SqlQueryRaw<int>(
                    "SELECT 1 AS \"Value\" FROM pg_database WHERE datname = 'powersync'"
                )
                .AnyAsync(ct);
            if (!exists)
            {
                await db.Database.ExecuteSqlRawAsync("CREATE DATABASE powersync;", ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure PowerSync storage database.");
        }
    }
}
