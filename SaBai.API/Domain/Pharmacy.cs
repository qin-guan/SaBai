namespace SaBai.API.Domain;

public sealed class MedicationActiveIngredient : ISyncEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

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

public sealed class MedicationBrand : ISyncEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public required string Name { get; set; }

    public Guid ActiveIngredientId { get; set; }

    public MedicationActiveIngredient ActiveIngredient { get; set; } = null!;

    public string? Remarks { get; set; }

    public ICollection<MedicationStock> Stocks { get; set; } = [];

    public ICollection<Order> Orders { get; set; } = [];
}

public sealed class MedicationStock : ISyncEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid MedicationBrandId { get; set; }

    public MedicationBrand MedicationBrand { get; set; } = null!;

    public int Quantity { get; set; }

    public DateTimeOffset Expiry { get; set; }

    public required string Location { get; set; }

    public StockStatus StockStatus { get; set; } = StockStatus.Active;

    public string? Remarks { get; set; }

    public ICollection<StockChange> Changes { get; set; } = [];
}

public sealed class StockChange : ISyncEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid StockId { get; set; }

    public MedicationStock Stock { get; set; } = null!;

    public StockChangeField Field { get; set; }

    public required string PreviousValue { get; set; }

    public required string NewValue { get; set; }

    public Guid UserId { get; set; }

    public StaffUser User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}

public sealed class Order : ISyncEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;

    public Guid MedicationBrandId { get; set; }

    public MedicationBrand MedicationBrand { get; set; } = null!;

    [PhiData]
    public required string DosageInstructions { get; set; }

    [PhiData]
    public string? Remarks { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public int Quantity { get; set; }

    public Guid? ApprovedById { get; set; }

    public StaffUser? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}

public sealed class OrderItem
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid StockChangeId { get; set; }

    public StockChange StockChange { get; set; } = null!;
}
