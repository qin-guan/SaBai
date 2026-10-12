using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Puberty;

internal static class PubertyRules
{
    public static void EnsureAges(
        bool? pubarche,
        int? pubarcheAge,
        bool? thelarche,
        int? thelarcheAge,
        bool? menarche,
        int? menarcheAge,
        bool? voiceChange,
        int? voiceChangeAge,
        bool? testicularGrowth,
        int? testicularGrowthAge
    )
    {
        Ensure(pubarche, pubarcheAge, "pubarche", "pubarcheAge");
        Ensure(thelarche, thelarcheAge, "thelarche", "thelarcheAge");
        Ensure(menarche, menarcheAge, "menarche", "menarcheAge");
        Ensure(voiceChange, voiceChangeAge, "voiceChange", "voiceChangeAge");
        Ensure(testicularGrowth, testicularGrowthAge, "testicularGrowth", "testicularGrowthAge");
    }

    private static void Ensure(bool? flag, int? age, string flagName, string ageName)
    {
        if (flag == false && age is not null)
        {
            throw new BadRequestException($"{ageName} must not be set when {flagName} is false");
        }
    }
}
