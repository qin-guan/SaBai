using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
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
    /// <summary>
    /// The current request, if any. The context is pooled and cannot take request services in
    /// its constructor; the accessor reads the request from an AsyncLocal, as the audit
    /// interceptor does.
    /// </summary>
    private static readonly HttpContextAccessor CurrentRequest = new();

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

    public DbSet<ReferralPartner> ReferralPartners => Set<ReferralPartner>();

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

    public DbSet<PatientConsent> PatientConsents => Set<PatientConsent>();

    public DbSet<DisclosureLog> DisclosureLogs => Set<DisclosureLog>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<StaffUserTag> StaffUserTags => Set<StaffUserTag>();

    public DbSet<PatientTag> PatientTags => Set<PatientTag>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampRecordedAt();
        StampTrackedEntities();
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException ex) when (IsPrimaryKeyViolation(ex))
        {
            throw new DuplicateKeyException(ex);
        }
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        StampRecordedAt();
        StampTrackedEntities();
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsPrimaryKeyViolation(ex))
        {
            throw new DuplicateKeyException(ex);
        }
    }

    /// <summary>
    /// An insert collided with an existing primary key. Postgres names primary-key constraints
    /// <c>PK_*</c> (EF Core's convention); other unique indexes stay ordinary unique violations
    /// that endpoints turn into their own messages.
    /// </summary>
    private static bool IsPrimaryKeyViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: { } constraint,
        }
        && constraint.StartsWith("PK_", StringComparison.Ordinal);

    /// <summary>
    /// Stamps when a station record (vitals, eyesight, dental) was first saved,
    /// whichever endpoint created it, so station throughput can be measured.
    /// </summary>
    private void StampRecordedAt()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
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
                    case Referral referral when referral.CreatedAt == default:
                        referral.CreatedAt = now;
                        break;
                    case ReferralPartner partner when partner.CreatedAt == default:
                        partner.CreatedAt = now;
                        break;
                    case Tag tag:
                        if (tag.CreatedAt == default)
                            tag.CreatedAt = now;
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Stamps <see cref="ITrackedEntity.UpdatedAt"/> and <see cref="ITrackedEntity.UpdatedByUserId"/>
    /// on every tracked record being inserted or changed, after checking the request's
    /// <c>If-Unmodified-Since</c> precondition (see <see cref="CheckUnmodifiedSince"/>).
    /// Only entries the change tracker already sees as Added or Modified are touched, so an
    /// unchanged record never becomes Modified because of the stamp itself.
    /// </summary>
    private void StampTrackedEntities()
    {
        var http = CurrentRequest.HttpContext;
        var userId = AuditActor.UserIdOf(http);
        // Entries() runs DetectChanges, so property edits made since loading show as Modified.
        var entries = ChangeTracker
            .Entries<ITrackedEntity>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (entries.Count == 0)
        {
            return;
        }

        CheckUnmodifiedSince(http, userId, entries);

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in entries)
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedByUserId = userId;
            }
        }
    }

    /// <summary>
    /// When the request carries <c>If-Unmodified-Since</c> (a queued offline write being
    /// replayed), refuses to change or delete any tracked record that a different user changed
    /// after that time. Inserts are never checked, and neither are records whose last change
    /// was made by the same user or by no signed-in user.
    /// </summary>
    private static void CheckUnmodifiedSince(
        HttpContext? http,
        Guid? userId,
        IEnumerable<EntityEntry<ITrackedEntity>> entries
    )
    {
        if (http?.Request.GetTypedHeaders().IfUnmodifiedSince is not { } since)
        {
            return;
        }

        var sinceSeconds = since.ToUnixTimeSeconds();
        foreach (var entry in entries)
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var changedAt = entry.Property(x => x.UpdatedAt).OriginalValue;
            var changedBy = entry.Property(x => x.UpdatedByUserId).OriginalValue;
            // HTTP dates have whole-second precision; compare at that precision.
            if (changedAt.ToUnixTimeSeconds() >= sinceSeconds && changedBy is { } by && by != userId)
            {
                var key = entry.Metadata.FindPrimaryKey()!.Properties.Select(p =>
                    entry.Property(p.Name).OriginalValue?.ToString()
                );
                throw new PreconditionFailedException(
                    entry.Metadata.ClrType.Name,
                    string.Join(",", key),
                    changedAt
                );
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Every clinic table is keyed by a UUIDv7 that the entity sets when it is constructed,
        // or that the client chose and sent in the create request. EF must store it as given and never
        // generate one: with a generated key, a new child reached through a navigation that
        // already has a key would be taken for an existing row and UPDATEd instead of INSERTed.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (
                entityType.ClrType.Namespace != typeof(Patient).Namespace
                || entityType.ClrType == typeof(StaffUser)
            )
                continue;
            var key = entityType.FindPrimaryKey();
            if (key is { Properties: [{ ClrType: var keyType } keyProperty] } && keyType == typeof(Guid))
                keyProperty.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }

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
            // The card number is assigned by Postgres when the row is inserted, never by
            // clients (they may create the patient offline), and is never changed afterwards.
            entity.Property(x => x.Number).UseIdentityByDefaultColumn();
            entity.Property(x => x.Number).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            entity.HasIndex(x => x.Number).IsUnique();
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

        modelBuilder.Entity<ReferralPartner>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<Referral>(entity =>
        {
            entity.Property(x => x.ReferralState).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.GuardiansContacted).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ClassOfService).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.AmountPaidUsd).HasPrecision(10, 2);
            entity
                .HasOne(x => x.Consult)
                .WithMany(x => x.Referrals)
                .HasForeignKey(x => x.ConsultId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.ReferredTo)
                .WithMany(x => x.Referrals)
                .HasForeignKey(x => x.ReferredToId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.MemberInCharge)
                .WithMany()
                .HasForeignKey(x => x.MemberInChargeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.MemberOnRun)
                .WithMany()
                .HasForeignKey(x => x.MemberOnRunId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.MemberWhoPaid)
                .WithMany()
                .HasForeignKey(x => x.MemberWhoPaidId)
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

        modelBuilder.Entity<PatientConsent>(entity =>
        {
            entity.Property(x => x.ConsentType).HasConversion<string>().HasMaxLength(64);
            entity.HasIndex(x => new { x.PatientId, x.ConsentType });
            entity
                .HasOne(x => x.Patient)
                .WithMany()
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DisclosureLog>(entity =>
        {
            entity.Property(x => x.Recipient).HasMaxLength(255);
            entity.Property(x => x.Purpose).HasMaxLength(255);
            entity.HasIndex(x => new { x.PatientId, x.DisclosedAt });
            entity
                .HasOne(x => x.Patient)
                .WithMany()
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(TagSync.MaxTagLength);
            entity.Property(x => x.NormalizedName).HasMaxLength(TagSync.MaxTagLength);
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<StaffUserTag>(entity =>
        {
            entity.HasKey(x => new { x.StaffUserId, x.TagId });
            entity
                .HasOne(x => x.StaffUser)
                .WithMany(x => x.UserTags)
                .HasForeignKey(x => x.StaffUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.Tag)
                .WithMany(x => x.StaffUsers)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatientTag>(entity =>
        {
            entity.HasKey(x => new { x.PatientId, x.TagId });
            entity
                .HasOne(x => x.Patient)
                .WithMany(x => x.PatientTags)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.Tag)
                .WithMany(x => x.Patients)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
