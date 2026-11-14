namespace SaBai.API.Endpoints.MedicationIngredients.Update.Put;

/// <summary>Updated medication active ingredient.</summary>
public sealed record Response(
    Guid Id,
    string Name,
    string UnitOfMeasurement,
    int FallBelow,
    string? Remarks
);
