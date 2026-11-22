namespace SaBai.API.Endpoints.MedicationStock.Splits.Post;

/// <summary>Split completed: the parent keeps only its reserved units (zero when none were reserved).</summary>
public sealed record Response(bool Success);
