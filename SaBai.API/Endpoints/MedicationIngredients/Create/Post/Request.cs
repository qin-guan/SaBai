namespace SaBai.API.Endpoints.MedicationIngredients.Create.Post;

/// <summary>New medication active ingredient.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public required string Name { get; init; }

    public required string UnitOfMeasurement { get; init; }

    public int FallBelow { get; init; }

    public string? Remarks { get; init; }
}
