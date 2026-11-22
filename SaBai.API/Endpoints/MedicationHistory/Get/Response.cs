namespace SaBai.API.Endpoints.MedicationHistory.Get;

/// <summary>A dispensed order (<c>kind</c> "order") or a quantity change made by hand (create, adjustment, split; <c>kind</c> "adjustment"), newest first.</summary>
public sealed record Response(
    string Kind,
    string ActorName,
    string PatientName,
    int QuantityChanged,
    int QuantityRemaining,
    DateTimeOffset Date
);
