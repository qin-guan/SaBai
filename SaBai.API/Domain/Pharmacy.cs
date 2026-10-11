namespace SaBai.API.Domain;

public sealed class MedicationActiveIngredient
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string UnitOfMeasurement { get; set; }

    public int FallBelow { get; set; }

    public string? Remarks { get; set; }

    public ICollection<MedicationBrand> Brands { get; set; } = [];
}

public sealed class MedicationBrand
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int ActiveIngredientId { get; set; }

    public MedicationActiveIngredient ActiveIngredient { get; set; } = null!;

    public string? Remarks { get; set; }

    public ICollection<MedicationStock> Stocks { get; set; } = [];

    public ICollection<Order> Orders { get; set; } = [];
}

public sealed class MedicationStock
{
    public int Id { get; set; }

    public int MedicationBrandId { get; set; }

    public MedicationBrand MedicationBrand { get; set; } = null!;

    public int Quantity { get; set; }

    public DateTimeOffset Expiry { get; set; }

    public required string Location { get; set; }

    public StockStatus StockStatus { get; set; } = StockStatus.Active;

    public string? Remarks { get; set; }

    public ICollection<StockChange> Changes { get; set; } = [];
}

public sealed class StockChange
{
    public int Id { get; set; }

    public int StockId { get; set; }

    public MedicationStock Stock { get; set; } = null!;

    public StockChangeField Field { get; set; }

    public required string PreviousValue { get; set; }

    public required string NewValue { get; set; }

    public Guid UserId { get; set; }

    public StaffUser User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}

public sealed class Order
{
    public int Id { get; set; }

    public int ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;

    public int MedicationBrandId { get; set; }

    public MedicationBrand MedicationBrand { get; set; } = null!;

    public required string DosageInstructions { get; set; }

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
    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int StockChangeId { get; set; }

    public StockChange StockChange { get; set; } = null!;
}
