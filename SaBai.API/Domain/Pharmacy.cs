namespace SaBai.API.Domain;

public sealed class MedicationActiveIngredient : ITrackedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public required string Name { get; set; }

    public required string UnitOfMeasurement { get; set; }

    public int FallBelow { get; set; }

    public string? Remarks { get; set; }

    /// <summary>Optional RxNorm concept identifier for FHIR Medication interoperability.</summary>
    public string? RxNormCode { get; set; }

    /// <summary>Optional WHO ATC code for FHIR Medication interoperability.</summary>
    public string? AtcCode { get; set; }

    public ICollection<MedicationBrand> Brands { get; set; } = [];
}

public sealed class MedicationBrand : ITrackedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public required string Name { get; set; }

    public Guid ActiveIngredientId { get; set; }

    public MedicationActiveIngredient ActiveIngredient { get; set; } = null!;

    public string? Remarks { get; set; }

    public ICollection<MedicationStock> Stocks { get; set; } = [];
}

/// <summary>
/// One physical batch of a brand: what arrived together (or what was split off another batch),
/// with one expiry and one storage location.
/// </summary>
/// <remarks>
/// <see cref="Quantity"/> is what is physically on hand; <see cref="ReservedQuantity"/> of it is set
/// aside for pending orders (see <see cref="OrderAllocation"/>), and the rest,
/// <see cref="AvailableQuantity"/>, can be ordered, split off or adjusted away. The two quantities
/// only change through the methods below, which keep <c>0 &lt;= ReservedQuantity &lt;= Quantity</c>
/// (also enforced by check constraints). <see cref="Version"/> is the row version (Postgres
/// <c>xmin</c>): two requests that change the same batch at once can't both save.
/// </remarks>
public sealed class MedicationStock : ITrackedEntity
{
    private int _quantity;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public Guid MedicationBrandId { get; set; }

    public MedicationBrand MedicationBrand { get; set; } = null!;

    /// <summary>Units physically on hand, reserved ones included. Set when the batch is created.</summary>
    public int Quantity
    {
        get => _quantity;
        init => _quantity = value >= 0
            ? value
            : throw new BadRequestException("Stock quantity cannot be negative.");
    }

    /// <summary>Units of <see cref="Quantity"/> set aside for pending orders.</summary>
    public int ReservedQuantity { get; private set; }

    /// <summary>Units that are on hand and not reserved.</summary>
    public int AvailableQuantity => Quantity - ReservedQuantity;

    public DateTimeOffset Expiry { get; set; }

    public required string Location { get; set; }

    /// <summary>Set by hand; a batch holding reservations stays <see cref="StockStatus.Active"/>.</summary>
    public StockStatus StockStatus { get; private set; } = StockStatus.Active;

    public string? Remarks { get; set; }

    /// <summary>The batch this one was split off, if any.</summary>
    public Guid? ParentStockId { get; set; }

    public MedicationStock? ParentStock { get; set; }

    /// <summary>Batches split off this one.</summary>
    public ICollection<MedicationStock> SplitStocks { get; set; } = [];

    /// <summary>Row version (Postgres <c>xmin</c>) used as the optimistic concurrency token.</summary>
    public uint Version { get; set; }

    public ICollection<StockChange> Changes { get; set; } = [];

    /// <summary>Orders that reserved (or were dispensed) units of this batch.</summary>
    public ICollection<OrderAllocation> Allocations { get; set; } = [];

    /// <summary>A new batch with the given status (any status can be chosen for a batch with nothing reserved).</summary>
    public static MedicationStock Create(
        Guid id,
        Guid brandId,
        int quantity,
        DateTimeOffset expiry,
        string location,
        StockStatus status,
        string? remarks,
        Guid? parentStockId = null
    ) =>
        new()
        {
            Id = id,
            MedicationBrandId = brandId,
            Quantity = quantity,
            Expiry = expiry,
            Location = location,
            StockStatus = status,
            Remarks = remarks,
            ParentStockId = parentStockId,
        };

    /// <summary>Whether units of this batch may be reserved for a new order at <paramref name="at"/>.</summary>
    public bool IsOrderable(DateTimeOffset at) =>
        StockStatus == StockStatus.Active && Expiry >= at && AvailableQuantity > 0;

    /// <summary>Sets <paramref name="units"/> aside for a pending order.</summary>
    /// <exception cref="ConflictException">The batch is not active or has fewer units available.</exception>
    public void Reserve(int units)
    {
        RequirePositive(units);
        if (StockStatus != StockStatus.Active)
        {
            throw new ConflictException("Only active stock can be reserved.");
        }

        if (units > AvailableQuantity)
        {
            throw new ConflictException(
                $"Insufficient stock: {AvailableQuantity} of {units} available."
            );
        }

        ReservedQuantity += units;
    }

    /// <summary>Puts <paramref name="units"/> reserved by an order back into available stock.</summary>
    public void Release(int units)
    {
        RequirePositive(units);
        if (units > ReservedQuantity)
        {
            throw new InvalidOperationException(
                $"Cannot release {units} units of stock {Id}: only {ReservedQuantity} are reserved."
            );
        }

        ReservedQuantity -= units;
    }

    /// <summary>Hands out <paramref name="units"/> that an order reserved: they leave the shelf.</summary>
    public void Dispense(int units)
    {
        RequirePositive(units);
        if (units > ReservedQuantity)
        {
            throw new InvalidOperationException(
                $"Cannot dispense {units} units of stock {Id}: only {ReservedQuantity} are reserved."
            );
        }

        ReservedQuantity -= units;
        _quantity -= units;
    }

    /// <summary>A manual count correction. Reserved units can't be adjusted away.</summary>
    /// <exception cref="BadRequestException">The quantity would go below zero.</exception>
    /// <exception cref="ConflictException">The quantity would go below what is reserved.</exception>
    public void AdjustQuantity(int change)
    {
        var next = Quantity + change;
        if (next < 0)
        {
            throw new BadRequestException("Stock quantity cannot be negative.");
        }

        if (next < ReservedQuantity)
        {
            throw new ConflictException(
                $"Stock quantity cannot go below the {ReservedQuantity} units reserved for pending orders."
            );
        }

        _quantity = next;
    }

    /// <summary>Changes the status by hand. A batch holding reservations must stay active.</summary>
    public void ChangeStatus(StockStatus status)
    {
        if (status == StockStatus)
        {
            return;
        }

        if (ReservedQuantity > 0)
        {
            throw new ConflictException(
                $"Stock status cannot change while {ReservedQuantity} units are reserved for pending orders."
            );
        }

        StockStatus = status;
    }

    /// <summary>
    /// Moves <paramref name="units"/> out of this batch into batches split off it. Only available
    /// (unreserved) units can move; reserved ones stay here with their orders.
    /// </summary>
    public void SplitOff(int units)
    {
        RequirePositive(units);
        if (units > AvailableQuantity)
        {
            throw new ConflictException(
                $"Only the {AvailableQuantity} unreserved units can be split off this stock."
            );
        }

        _quantity -= units;
    }

    private static void RequirePositive(int units) =>
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(units);
}

public sealed class StockChange : ITrackedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public Guid StockId { get; set; }

    public MedicationStock Stock { get; set; } = null!;

    public StockChangeField Field { get; set; }

    public required string PreviousValue { get; set; }

    public required string NewValue { get; set; }

    public Guid UserId { get; set; }

    public StaffUser User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The order whose dispensing made this change; null for changes made by hand.</summary>
    public Guid? OrderId { get; set; }

    public Order? Order { get; set; }
}

/// <summary>
/// A prescription line placed during a consult: an amount of an active ingredient. The pharmacy's
/// choice of brand and batch is in <see cref="Allocations"/>. A pending order holds reservations on
/// those batches; it can be changed or cancelled until it is dispensed. See
/// <see cref="Data.OrderLifecycle"/>.
/// </summary>
public sealed class Order : ITrackedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public Guid ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;

    public Guid MedicationActiveIngredientId { get; set; }

    public MedicationActiveIngredient MedicationActiveIngredient { get; set; } = null!;

    [PhiData]
    public required string DosageInstructions { get; set; }

    [PhiData]
    public string? Remarks { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public int Quantity { get; set; }

    public Guid? DispensedById { get; set; }

    public StaffUser? DispensedBy { get; set; }

    public DateTimeOffset? DispensedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The batches this order's quantity is taken from. While pending they are reservations;
    /// once dispensed they record what was handed out. A cancelled order has none.
    /// </summary>
    public ICollection<OrderAllocation> Allocations { get; set; } = [];
}

/// <summary>Units of one stock batch reserved for (or dispensed to) an order.</summary>
public sealed class OrderAllocation
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid StockId { get; set; }

    public MedicationStock Stock { get; set; } = null!;

    public int Quantity { get; set; }
}
