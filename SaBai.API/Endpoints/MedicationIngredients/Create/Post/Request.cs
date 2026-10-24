namespace SaBai.API.Endpoints.MedicationIngredients.Create.Post;

/// <summary>New medication active ingredient.</summary>
public sealed record Request
{
    public required string Name { get; init; }

    public required string UnitOfMeasurement { get; init; }

    public int FallBelow { get; init; }

    public string? Remarks { get; init; }
}
