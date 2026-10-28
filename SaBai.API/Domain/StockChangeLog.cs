using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaBai.API.Domain;

/// <summary>
/// Builds the <see cref="StockChange"/> audit rows for a stock lot being created or edited,
/// one row per field that changed.
/// </summary>
public static class StockChangeLog
{
    /// <summary>Snapshot of the audited fields of a lot before it is edited.</summary>
    public readonly record struct Snapshot(
        int Quantity,
        string Location,
        StockStatus StockStatus,
        string? Remarks
    )
    {
        public static Snapshot Of(MedicationStock stock) =>
            new(stock.Quantity, stock.Location, stock.StockStatus, stock.Remarks);
    }

    /// <summary>Rows for a new lot: its starting quantity, plus any location, status and remarks.</summary>
    public static IEnumerable<StockChange> ForCreate(
        MedicationStock stock,
        Guid userId,
        DateTimeOffset at
    )
    {
        yield return Change(stock, StockChangeField.Quantity, "0", Format(stock.Quantity), userId, at);
        if (!string.IsNullOrEmpty(stock.Location))
        {
            yield return Change(stock, StockChangeField.Location, "", stock.Location, userId, at);
        }

        yield return Change(stock, StockChangeField.StockStatus, "", Format(stock.StockStatus), userId, at);
        if (!string.IsNullOrEmpty(stock.Remarks))
        {
            yield return Change(stock, StockChangeField.Remarks, "", stock.Remarks, userId, at);
        }
    }

    /// <summary>Rows for each audited field that differs between <paramref name="before"/> and the lot now.</summary>
    public static IEnumerable<StockChange> ForUpdate(
        Snapshot before,
        MedicationStock stock,
        Guid userId,
        DateTimeOffset at
    )
    {
        if (before.Location != stock.Location)
        {
            yield return Change(stock, StockChangeField.Location, before.Location, stock.Location, userId, at);
        }

        if (before.Quantity != stock.Quantity)
        {
            yield return Change(
                stock,
                StockChangeField.Quantity,
                Format(before.Quantity),
                Format(stock.Quantity),
                userId,
                at
            );
        }

        if (before.StockStatus != stock.StockStatus)
        {
            yield return Change(
                stock,
                StockChangeField.StockStatus,
                Format(before.StockStatus),
                Format(stock.StockStatus),
                userId,
                at
            );
        }

        if (before.Remarks != stock.Remarks)
        {
            yield return Change(
                stock,
                StockChangeField.Remarks,
                before.Remarks ?? "",
                stock.Remarks ?? "",
                userId,
                at
            );
        }
    }

    private static StockChange Change(
        MedicationStock stock,
        StockChangeField field,
        string previous,
        string next,
        Guid userId,
        DateTimeOffset at
    ) =>
        new()
        {
            Stock = stock,
            Field = field,
            PreviousValue = previous,
            NewValue = next,
            UserId = userId,
            CreatedAt = at,
        };

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static readonly JsonSerializerOptions EnumJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The status's wire name (e.g. "active"), matching what the API returns.</summary>
    private static string Format(StockStatus status) =>
        JsonSerializer.Serialize(status, EnumJson).Trim('"');
}
