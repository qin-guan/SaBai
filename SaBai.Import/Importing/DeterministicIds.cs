using System.Security.Cryptography;
using System.Text;

namespace SaBai.Import.Importing;

/// <summary>
/// Ids for imported rows, derived from the upstream table and upstream id. The same upstream row
/// always gets the same id, so a second run updates or skips instead of duplicating, and foreign
/// keys are computed rather than looked up.
/// </summary>
/// <remarks>
/// The ids have the UUIDv7 layout SaBai uses everywhere, because many lists here order by id as
/// creation order (broadcast paging with <c>since</c>, audit log, tie-breaks). The 48-bit
/// timestamp is <see cref="Epoch"/> plus the upstream serial id in milliseconds, so imported rows
/// keep upstream's creation order among themselves and sort before every row created in SaBai
/// (its ids carry the real time). The other 74 bits come from SHA-256 of <c>table:id</c>. Nothing
/// that upstream can edit goes into an id, so it can never change between runs.
/// </remarks>
public static class DeterministicIds
{
    /// <summary>Timestamp of upstream id 0. Never change it: ids would no longer match.</summary>
    public static readonly DateTimeOffset Epoch = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static Guid For(string table, string upstreamId) => Build(table, upstreamId, 0);

    private static Guid Build(string table, string upstreamId, long ordinal)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{table}:{upstreamId}"));
        var ms = Epoch.ToUnixTimeMilliseconds() + ordinal;
        Span<byte> bytes = stackalloc byte[16];
        for (var i = 0; i < 6; i++)
        {
            bytes[i] = (byte)(ms >> (8 * (5 - i)));
        }

        hash.AsSpan(0, 10).CopyTo(bytes[6..]);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70); // version 7
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
        return new Guid(bytes, bigEndian: true);
    }

    public static Guid For(string table, int upstreamId) =>
        Build(table, upstreamId.ToString(), Math.Max(0, upstreamId));

    public static Guid For(string table, Guid upstreamId) => For(table, upstreamId.ToString("D"));

    public static Guid VillageCode(int id) => For("village_codes", id);
    public static Guid Patient(int id) => For("patients", id);
    public static Guid Visit(int id) => For("visits", id);
    public static Guid Vitals(int id) => For("vitals", id);
    public static Guid Puberty(int id) => For("puberty", id);
    public static Guid Eyesight(int id) => For("eyesight", id);
    public static Guid Consult(int id) => For("consults", id);
    public static Guid Diagnosis(int id) => For("diagnosis", id);
    public static Guid Referral(int id) => For("referrals", id);
    public static Guid ReferralPartner(Guid id) => For("referral_partners", id);
    public static Guid Ingredient(int id) => For("medication_active_ingredients", id);
    public static Guid Brand(int id) => For("medication_brands", id);
    public static Guid Stock(int id) => For("medication_stock", id);
    public static Guid Broadcast(int id) => For("broadcasts", id);
    public static Guid File(int id) => For("files", id);
    public static Guid User(Guid id) => For("users", id);

    /// <summary>Object name part of an imported patient photo's key.</summary>
    public static Guid PatientPhoto(int patientId) => For("patient_photo", patientId);

    /// <summary>The consent recorded with <c>--record-upstream-face-consent</c>.</summary>
    public static Guid FaceConsent(int patientId) => For("face_consent", patientId);

    /// <summary>The staff account imported rows are attributed to.</summary>
    public static readonly Guid ImportUser = For("sabai_import", "system");
}
