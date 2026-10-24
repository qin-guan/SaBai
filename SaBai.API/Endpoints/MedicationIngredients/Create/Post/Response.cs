namespace SaBai.API.Endpoints.MedicationIngredients.Create.Post;

/// <summary>Created medication active ingredient.</summary>
public sealed record Response(
    int Id,
    string Name,
    string UnitOfMeasurement,
    int FallBelow,
    string? Remarks
);
