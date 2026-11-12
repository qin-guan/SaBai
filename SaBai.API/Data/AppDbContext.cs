using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Domain;

namespace SaBai.API.Data;

/// <summary>
/// Clinic data plus the ASP.NET Core Identity store (staff users, roles, two-factor
/// tokens) and the Data Protection key ring that signs the session cookies.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<StaffUser, IdentityRole<Guid>, Guid>(options),
        IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<VillageCode> VillageCodes => Set<VillageCode>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Visit> Visits => Set<Visit>();

    public DbSet<Vitals> Vitals => Set<Vitals>();

    public DbSet<Eyesight> Eyesights => Set<Eyesight>();

    public DbSet<DentalConsult> DentalConsults => Set<DentalConsult>();

    public DbSet<PubertyAssessment> PubertyAssessments => Set<PubertyAssessment>();

    public DbSet<ChildVitals> ChildVitals => Set<ChildVitals>();

    public DbSet<Consult> Consults => Set<Consult>();

    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();

    public DbSet<Referral> Referrals => Set<Referral>();

    public DbSet<MedicationActiveIngredient> MedicationActiveIngredients =>
        Set<MedicationActiveIngredient>();

    public DbSet<MedicationBrand> MedicationBrands => Set<MedicationBrand>();

    public DbSet<MedicationStock> MedicationStocks => Set<MedicationStock>();

    public DbSet<StockChange> StockChanges => Set<StockChange>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<StoredFile> Files => Set<StoredFile>();

    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();

    public DbSet<StaffPushSubscription> PushSubscriptions => Set<StaffPushSubscription>();

    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampRecordedAt();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        StampRecordedAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Stamps when a station record (vitals, eyesight, dental) was first saved,
    /// whichever endpoint created it, so station throughput can be measured.
    /// </summary>
    private void StampRecordedAt()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Added))
        {
            switch (entry.Entity)
            {
                case Vitals { RecordedAt: null } vitals:
                    vitals.RecordedAt = now;
                    break;
                case Eyesight { RecordedAt: null } eyesight:
                    eyesight.RecordedAt = now;
                    break;
                case DentalConsult { RecordedAt: null } dental:
                    dental.RecordedAt = now;
                    break;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StaffUser>(entity =>
        {
            // Keep the original table so consult/order foreign keys stay as they are.
            entity.ToTable("StaffUsers");
            entity.Property(x => x.UserName).HasMaxLength(150);
            entity.Property(x => x.NormalizedUserName).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(255);
            // Email is optional, but two accounts may not share one.
            entity.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
            entity.Property(x => x.Nickname).HasMaxLength(255);
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
            // Reuse the reserved columns so no migration is required; see Patient docs.
            entity.Property(x => x.PhotoStoredFileName).HasColumnName("PatientImagePublicId");
            entity.Property(x => x.LatestFaceId).HasColumnName("RekognitionFaceId");
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.IdentificationNumber);
            entity
                .HasOne(x => x.VillageCode)
                .WithMany(x => x.Patients)
                .HasForeignKey(x => x.VillageCodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.Property(x => x.Status).HasMaxLength(100);
            entity.HasIndex(x => x.Date);
            entity
                .HasOne(x => x.Patient)
                .WithMany(x => x.Visits)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.VillageCode)
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
            entity
                .HasOne(x => x.Visit)
                .WithOne(x => x.Vitals)
                .HasForeignKey<Vitals>(x => x.VisitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Eyesight>(entity =>
        {
            entity.HasIndex(x => x.VisitId).IsUnique();
            entity
                .HasOne(x => x.Visit)
                .WithOne(x => x.Eyesight)
                .HasForeignKey<Eyesight>(x => x.VisitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DentalConsult>(entity =>
        {
            entity.HasIndex(x => x.VisitId).IsUnique();
            entity
                .HasOne(x => x.Visit)
                .WithOne(x => x.DentalConsult)
                .HasForeignKey<DentalConsult>(x => x.VisitId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.ImageFile)
                .WithMany()
                .HasForeignKey(x => x.ImageFileId)
                .OnDelete(DeleteBehavior.SetNull);

            // Store every examination/history choice by name, like the other enums here.
            foreach (
                var property in typeof(DentalConsult)
                    .GetProperties()
                    .Where(p =>
                        (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).IsEnum
                    )
            )
            {
                entity.Property(property.Name).HasConversion<string>().HasMaxLength(32);
            }
        });

        modelBuilder.Entity<PubertyAssessment>(entity =>
        {
            entity.HasIndex(x => x.VitalsId).IsUnique();
            entity
                .HasOne(x => x.Vitals)
                .WithOne(x => x.Puberty)
                .HasForeignKey<PubertyAssessment>(x => x.VitalsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChildVitals>(entity =>
        {
            entity.HasIndex(x => x.VitalsId).IsUnique();
            entity
                .HasOne(x => x.Vitals)
                .WithOne(x => x.ChildVitals)
                .HasForeignKey<ChildVitals>(x => x.VitalsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Consult>(entity =>
        {
            entity
                .HasOne(x => x.Visit)
                .WithMany(x => x.Consults)
                .HasForeignKey(x => x.VisitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.Doctor)
                .WithMany()
                .HasForeignKey(x => x.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Diagnosis>(entity =>
        {
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(64);
            entity
                .HasOne(x => x.Consult)
                .WithMany(x => x.Diagnoses)
                .HasForeignKey(x => x.ConsultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Referral>(entity =>
        {
            entity.Property(x => x.ReferralState).HasConversion<string>().HasMaxLength(32);
            entity
                .HasOne(x => x.Consult)
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
            entity
                .HasOne(x => x.ActiveIngredient)
                .WithMany(x => x.Brands)
                .HasForeignKey(x => x.ActiveIngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicationStock>(entity =>
        {
            entity.Property(x => x.Location).HasMaxLength(255);
            entity.Property(x => x.StockStatus).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.StockStatus);
            entity
                .HasOne(x => x.MedicationBrand)
                .WithMany(x => x.Stocks)
                .HasForeignKey(x => x.MedicationBrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockChange>(entity =>
        {
            entity.Property(x => x.Field).HasConversion<string>().HasMaxLength(32);
            entity
                .HasOne(x => x.Stock)
                .WithMany(x => x.Changes)
                .HasForeignKey(x => x.StockId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.Status);
            entity.ToTable(table =>
                table.HasCheckConstraint("ck_orders_quantity_positive", "\"Quantity\" > 0")
            );
            entity
                .HasOne(x => x.Consult)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.ConsultId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.MedicationBrand)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.MedicationBrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(x => new { x.OrderId, x.StockChangeId });
            entity
                .HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.StockChange)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.StockChangeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StoredFile>(entity =>
        {
            entity.ToTable("Files");
            entity.Property(x => x.FileName).HasMaxLength(255);
            entity.Property(x => x.StoredFileName).HasMaxLength(255);
            entity.HasIndex(x => new { x.PatientId, x.IsDeleted });
            entity
                .HasOne(x => x.Patient)
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

        modelBuilder.Entity<StaffPushSubscription>(entity =>
        {
            entity.ToTable("PushSubscriptions");
            entity.Property(x => x.Endpoint).HasMaxLength(2048);
            entity.Property(x => x.P256dhKey).HasMaxLength(128);
            entity.Property(x => x.AuthSecret).HasMaxLength(64);
            // A device has one subscription; whoever subscribed last owns it.
            entity.HasIndex(x => x.Endpoint).IsUnique();
            entity
                .HasOne(x => x.StaffUser)
                .WithMany()
                .HasForeignKey(x => x.StaffUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.Property(x => x.Action).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.UserName).HasMaxLength(150);
            entity.Property(x => x.EntityType).HasMaxLength(100);
            entity.Property(x => x.EntityId).HasMaxLength(200);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.Request).HasMaxLength(500);
            entity.Property(x => x.Details).HasMaxLength(500);
            // Stored as a jsonb array on the row; never queried on its own.
            entity.OwnsMany(x => x.Changes, changes => changes.ToJson());
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
            entity.HasIndex(x => x.UserId);
        });
    }
}
