using Microsoft.EntityFrameworkCore;
using SaBai.API.Domain;

namespace SaBai.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();

    public DbSet<VillageCode> VillageCodes => Set<VillageCode>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Visit> Visits => Set<Visit>();

    public DbSet<Vitals> Vitals => Set<Vitals>();

    public DbSet<Eyesight> Eyesights => Set<Eyesight>();

    public DbSet<PubertyAssessment> PubertyAssessments => Set<PubertyAssessment>();

    public DbSet<ChildVitals> ChildVitals => Set<ChildVitals>();

    public DbSet<Consult> Consults => Set<Consult>();

    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();

    public DbSet<Referral> Referrals => Set<Referral>();

    public DbSet<MedicationActiveIngredient> MedicationActiveIngredients => Set<MedicationActiveIngredient>();

    public DbSet<MedicationBrand> MedicationBrands => Set<MedicationBrand>();

    public DbSet<MedicationStock> MedicationStocks => Set<MedicationStock>();

    public DbSet<StockChange> StockChanges => Set<StockChange>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<ClinicalFile> ClinicalFiles => Set<ClinicalFile>();

    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StaffUser>(entity =>
        {
            entity.HasIndex(x => x.WorkOsUserId).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.WorkOsUserId).HasMaxLength(255);
            entity.Property(x => x.Username).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.Property(x => x.Nickname).HasMaxLength(255);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<VillageCode>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(255);
            entity.Property(x => x.Name).HasMaxLength(255);
            entity.Property(x => x.ColorHex).HasMaxLength(7);
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(255);
            entity.Property(x => x.IdentificationNumber).HasMaxLength(255);
            entity.Property(x => x.ContactNo).HasMaxLength(255);
            entity.Property(x => x.Gender).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.IdentificationNumber);
            entity.HasOne(x => x.VillageCode)
                .WithMany(x => x.Patients)
                .HasForeignKey(x => x.VillageCodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.Property(x => x.Status).HasMaxLength(100);
            entity.HasIndex(x => x.Date);
            entity.HasOne(x => x.Patient)
                .WithMany(x => x.Visits)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.VillageCode)
                .WithMany(x => x.Visits)
                .HasForeignKey(x => x.VillageCodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Vitals>(entity =>
        {
            entity.HasIndex(x => x.VisitId).IsUnique();
            entity.Property(x => x.Height).HasPrecision(5, 2);
            entity.Property(x => x.Weight).HasPrecision(5, 2);
            entity.Property(x => x.Temperature).HasPrecision(4, 2);
            entity.Property(x => x.HemocueCount).HasPrecision(5, 2);
            entity.Property(x => x.BloodGlucoseNonFasting).HasPrecision(5, 2);
            entity.Property(x => x.BloodGlucoseFasting).HasPrecision(5, 2);
            entity.Property(x => x.Hba1c).HasPrecision(5, 2);
            entity.HasOne(x => x.Visit)
                .WithOne(x => x.Vitals)
                .HasForeignKey<Vitals>(x => x.VisitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Eyesight>(entity =>
        {
            entity.HasIndex(x => x.VisitId).IsUnique();
            entity.HasOne(x => x.Visit)
                .WithOne(x => x.Eyesight)
                .HasForeignKey<Eyesight>(x => x.VisitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PubertyAssessment>(entity =>
        {
            entity.HasIndex(x => x.VitalsId).IsUnique();
            entity.HasOne(x => x.Vitals)
                .WithOne(x => x.Puberty)
                .HasForeignKey<PubertyAssessment>(x => x.VitalsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChildVitals>(entity =>
        {
            entity.HasIndex(x => x.VitalsId).IsUnique();
            entity.HasOne(x => x.Vitals)
                .WithOne(x => x.ChildVitals)
                .HasForeignKey<ChildVitals>(x => x.VitalsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Consult>(entity =>
        {
            entity.HasOne(x => x.Visit)
                .WithMany(x => x.Consults)
                .HasForeignKey(x => x.VisitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Doctor)
                .WithMany()
                .HasForeignKey(x => x.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Diagnosis>(entity =>
        {
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(64);
            entity.HasOne(x => x.Consult)
                .WithMany(x => x.Diagnoses)
                .HasForeignKey(x => x.ConsultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Referral>(entity =>
        {
            entity.Property(x => x.ReferralState).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Consult)
                .WithMany(x => x.Referrals)
                .HasForeignKey(x => x.ConsultId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicationActiveIngredient>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(255);
            entity.Property(x => x.UnitOfMeasurement).HasMaxLength(255);
        });

        modelBuilder.Entity<MedicationBrand>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(255);
            entity.HasOne(x => x.ActiveIngredient)
                .WithMany(x => x.Brands)
                .HasForeignKey(x => x.ActiveIngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicationStock>(entity =>
        {
            entity.Property(x => x.Location).HasMaxLength(255);
            entity.Property(x => x.StockStatus).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.StockStatus);
            entity.HasOne(x => x.MedicationBrand)
                .WithMany(x => x.Stocks)
                .HasForeignKey(x => x.MedicationBrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockChange>(entity =>
        {
            entity.Property(x => x.Field).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Stock)
                .WithMany(x => x.Changes)
                .HasForeignKey(x => x.StockId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.Status);
            entity.ToTable(table => table.HasCheckConstraint("ck_orders_quantity_positive", "\"Quantity\" > 0"));
            entity.HasOne(x => x.Consult)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.ConsultId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.MedicationBrand)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.MedicationBrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(x => new { x.OrderId, x.StockChangeId });
            entity.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.StockChange)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.StockChangeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClinicalFile>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(255);
            entity.Property(x => x.StoredFileName).HasMaxLength(255);
            entity.HasIndex(x => new { x.PatientId, x.IsDeleted });
            entity.HasOne(x => x.Patient)
                .WithMany(x => x.Files)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Broadcast>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(255);
            entity.Property(x => x.CreatedBy).HasMaxLength(255);
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
        });
    }
}
