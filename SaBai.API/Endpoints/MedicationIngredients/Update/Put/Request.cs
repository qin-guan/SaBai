namespace SaBai.API.Endpoints.MedicationIngredients.Update.Put;

/// <summary>Full replacement of a medication active ingredient.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public required string Name { get; init; }

    public required string UnitOfMeasurement { get; init; }

    public int FallBelow { get; init; }

    public string? Remarks { get; init; }
}
