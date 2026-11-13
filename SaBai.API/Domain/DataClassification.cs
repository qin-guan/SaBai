using Microsoft.Extensions.Compliance.Classification;

namespace SaBai.API.Domain;

/// <summary>
/// HIPAA data classification taxonomy built on <see cref="Microsoft.Extensions.Compliance.Classification.DataClassification"/>.
/// </summary>
public static class HipaaDataTaxonomy
{
    public static string TaxonomyName => typeof(HipaaDataTaxonomy).FullName!;

    public static DataClassification Phi { get; } = new(TaxonomyName, nameof(Phi));
}

/// <summary>
/// Marks a property, field, or parameter as HIPAA Protected Health Information (PHI)
/// using the standard .NET <see cref="DataClassificationAttribute"/>.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter,
    AllowMultiple = false
)]
public sealed class PhiDataAttribute() : DataClassificationAttribute(HipaaDataTaxonomy.Phi);
