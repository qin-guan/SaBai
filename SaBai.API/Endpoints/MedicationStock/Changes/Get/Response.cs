using System.Text.Json.Serialization;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Changes.Get;

/// <summary>What happened to a stock batch in one change-log row.</summary>
public enum StockChangeKind
{
    /// <summary>The batch was entered into stock.</summary>
    [JsonStringEnumMemberName("created")]
    Created,

    /// <summary>The batch was created by splitting units off another batch (<c>ParentStockId</c>).</summary>
    [JsonStringEnumMemberName("split_in")]
    SplitIn,

    /// <summary>Units of this batch were moved into batches split off it.</summary>
    [JsonStringEnumMemberName("split_out")]
    SplitOut,

    /// <summary>Units were handed out for an order; <c>Dispensing</c> says to whom.</summary>
    [JsonStringEnumMemberName("dispensed")]
    Dispensed,

    /// <summary>The quantity was corrected by hand.</summary>
    [JsonStringEnumMemberName("adjustment")]
    Adjustment,

    /// <summary>The location, status or remarks were edited.</summary>
    [JsonStringEnumMemberName("edit")]
    Edit,
}

/// <summary>
/// One row of a stock batch's change log, newest first. A row is one field changing from
/// <c>PreviousValue</c> to <c>NewValue</c>; entering a batch writes a row per field at the same
/// <c>CreatedAt</c>. For quantity rows <c>QuantityChange</c> is the difference (negative when units
/// left the batch) and <c>QuantityAfter</c> the quantity on hand after it; both are null for other
/// fields. <c>UserName</c> is the staff member who made the change. <c>Dispensing</c> is set for
/// <c>kind</c> "dispensed" only.
/// </summary>
public sealed record Response(
    Guid Id,
    Guid StockId,
    StockChangeKind Kind,
    StockChangeField Field,
    string PreviousValue,
    string NewValue,
    int? QuantityChange,
    int? QuantityAfter,
    Guid UserId,
    string UserName,
    DateTimeOffset CreatedAt,
    DispensingDetails? Dispensing
);

/// <summary>
/// The order a dispensing was for and its patient. The units taken from this batch are the row's
/// <c>QuantityChange</c>; <c>OrderQuantity</c> is the whole order, which may span several batches.
/// <c>PatientDisplayId</c> is the patient's card id (village code and number).
/// </summary>
public sealed record DispensingDetails(
    Guid OrderId,
    int OrderQuantity,
    Guid ConsultId,
    Guid VisitId,
    DateTimeOffset VisitDate,
    Guid PatientId,
    string PatientName,
    string PatientDisplayId
);
