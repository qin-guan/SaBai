namespace SaBai.API.Endpoints.MedicationStock.Changes.Get;

/// <summary>The stock batch whose change log should be listed.</summary>
public sealed record Request
{
    public Guid Id { get; init; }

    /// <summary>
    /// True lists only what was dispensed from the batch (<c>kind</c> "dispensed"); omitted or
    /// false lists every change.
    /// </summary>
    public bool? DispensedOnly { get; init; }
}
