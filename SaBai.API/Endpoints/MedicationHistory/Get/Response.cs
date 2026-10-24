namespace SaBai.API.Endpoints.MedicationHistory.Get;

/// <summary>An approved dispense or a catalog quantity adjustment, newest first.</summary>
public sealed record Response(
    string Kind,
    string ActorName,
    string PatientName,
    int QuantityChanged,
    int QuantityRemaining,
    DateTimeOffset Date);
