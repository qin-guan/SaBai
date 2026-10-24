using System.Text.RegularExpressions;

namespace SaBai.API.Domain;

public static partial class EyesightRules
{
    public const string AcuityMessage =
        "Must be VA notation (e.g. 6/9, 6/9+2, 6/9-2, 6/120) or CF, HM, LP, NLP";

    public const string PinholeMessage =
        "Must be VA notation (e.g. 6/9, 6/9+2, 6/9-2, 6/120), CF, HM, LP, NLP, or NI";

    public static string? NormalizeAcuity(string? value) =>
        Normalize(value, AcuityPattern(), AcuityMessage);

    public static string? NormalizePinhole(string? value) =>
        Normalize(value, PinholePattern(), PinholeMessage);

    private static string? Normalize(string? value, Regex pattern, string message)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (!pattern.IsMatch(trimmed))
        {
            throw new BadRequestException(message);
        }

        return trimmed.ToUpperInvariant();
    }

    [GeneratedRegex(
        @"^(6/(7\.5|6|9|12|15|18|24|36|60|120)([+-]\d{1,2})?|CF|HM|LP|NLP)$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex AcuityPattern();

    [GeneratedRegex(
        @"^(6/(7\.5|6|9|12|15|18|24|36|60|120)([+-]\d{1,2})?|CF|HM|LP|NLP|NI)$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex PinholePattern();
}
