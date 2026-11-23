using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MinhaGaragem.Application;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Infrastructure;

public sealed class User : IdentityUser<Guid>;

public sealed class GarageDbContext(
    DbContextOptions<GarageDbContext> options,
    ICurrentUser currentUser
) : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IGarageDb
{
    public DbSet<Mechanic> Mechanics => Set<Mechanic>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleComponent> Components => Set<VehicleComponent>();
    public DbSet<VehicleSubcomponent> Subcomponents => Set<VehicleSubcomponent>();
    public DbSet<MaintenanceRecordSubcomponent> RecordSubcomponents =>
        Set<MaintenanceRecordSubcomponent>();
    public DbSet<MaintenanceRecord> Records => Set<MaintenanceRecord>();
    public DbSet<MaintenanceItem> Items => Set<MaintenanceItem>();
    public DbSet<MaintenancePayment> Payments => Set<MaintenancePayment>();
    public DbSet<MaintenanceSchedule> Schedules => Set<MaintenanceSchedule>();
    public DbSet<MaintenanceAttachment> Attachments => Set<MaintenanceAttachment>();
    public DbSet<VehicleMileageHistory> MileageHistory => Set<VehicleMileageHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Mechanic>(e =>
        {
            e.HasQueryFilter(m => m.UserId == currentUser.Id);
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new
            {
                m.UserId,
                m.IsArchived,
                m.Name,
            });
            e.Property(m => m.Name).HasMaxLength(120);
            e.Property(m => m.Workshop).HasMaxLength(200);
            e.Property(m => m.Specialty).HasMaxLength(120);
            e.Property(m => m.Phone).HasMaxLength(40);
            e.Property(m => m.Email).HasMaxLength(254);
            e.Property(m => m.Address).HasMaxLength(300);
            e.Property(m => m.Notes).HasMaxLength(4000);
        });
        b.Entity<Vehicle>(e =>
        {
            e.HasQueryFilter(v => v.UserId == currentUser.Id);
            e.HasIndex(v => new { v.UserId, v.CreatedAt });
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(v => v.ConcurrencyStamp).IsConcurrencyToken();
            e.Property(v => v.Manufacturer).HasMaxLength(80);
            e.Property(v => v.Model).HasMaxLength(100);
            e.Property(v => v.Version).HasMaxLength(120);
            e.Property(v => v.Color).HasMaxLength(80);
            e.Property(v => v.Nickname).HasMaxLength(80);
            e.Property(v => v.Plate).HasMaxLength(12);
            e.Property(v => v.ColorHex).HasMaxLength(7);
            e.ToTable(t =>
                t.HasCheckConstraint(
                    "CK_Vehicle_ColorHex",
                    "\"ColorHex\" IS NULL OR \"ColorHex\" ~ '^#[0-9a-fA-F]{6}$'"
                )
            );
            e.ToTable(t =>
                t.HasCheckConstraint(
                    "CK_Vehicle_Mileage",
                    "\"Mileage\" IS NULL OR (\"Mileage\" >= 0 AND \"Mileage\" <= 10000000)"
                )
            );
        });
        b.Entity<VehicleComponent>(e =>
        {
            e.HasQueryFilter(c => c.Vehicle.UserId == currentUser.Id);
            e.HasOne(c => c.Vehicle).WithMany().HasForeignKey(c => c.VehicleId);
            e.HasIndex(c => new { c.VehicleId, c.Key }).IsUnique();
            e.HasAlternateKey(c => new { c.Id, c.VehicleId });
            e.Property(c => c.Key).HasMaxLength(40);
            e.Property(c => c.Name).HasMaxLength(80);
        });
        b.Entity<VehicleSubcomponent>(e =>
        {
            e.HasQueryFilter(s => s.Component.Vehicle.UserId == currentUser.Id);
            e.HasOne(s => s.Component)
                .WithMany(c => c.Subcomponents)
                .HasForeignKey(s => new { s.VehicleComponentId, s.VehicleId })
                .HasPrincipalKey(c => new { c.Id, c.VehicleId })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasAlternateKey(s => new
            {
                s.Id,
                s.VehicleComponentId,
                s.VehicleId,
            });
            e.HasIndex(s => new { s.VehicleComponentId, s.Key }).IsUnique();
            e.Property(s => s.Key).HasMaxLength(40);
            e.Property(s => s.Name).HasMaxLength(80);
        });
        b.Entity<MaintenanceRecord>(e =>
        {
            e.HasQueryFilter(r => r.Vehicle.UserId == currentUser.Id);
            e.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId);
            e.HasAlternateKey(r => new { r.Id, r.VehicleId });
            e.HasIndex(r => new { r.VehicleId, r.Date });
            e.HasOne(r => r.PrimaryComponent)
                .WithMany()
                .HasForeignKey(r => new { r.PrimaryComponentId, r.VehicleId })
                .HasPrincipalKey(c => new { c.Id, c.VehicleId })
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(r => r.Mechanic)
                .WithMany(m => m.Records)
                .HasForeignKey(r => r.MechanicId)
                .OnDelete(DeleteBehavior.SetNull);
            e.Property(r => r.MechanicName).HasMaxLength(120);
            e.Ignore(r => r.PartsCost);
            e.Ignore(r => r.TotalCost);
            e.Property(r => r.LaborCost).HasPrecision(16, 2);
            e.Ignore(r => r.BaseCost);
            e.Ignore(r => r.InterestCost);
            e.Ignore(r => r.PendingPaymentCount);
            e.Property(r => r.PackageTotal).HasPrecision(16, 2);
            e.ToTable(t =>
                t.HasCheckConstraint(
                    "CK_Record_PackageTotal",
                    "\"PackageTotal\" IS NULL OR (\"PackageTotal\" >= 0 AND \"PackageTotal\" <= 100000000 AND \"LaborCost\" = 0)"
                )
            );
            e.Property(r => r.Description).HasMaxLength(300);
            e.Property(r => r.Workshop).HasMaxLength(200);
            e.Property(r => r.Notes).HasMaxLength(4000);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Record_Cost", "\"LaborCost\" >= 0");
                t.HasCheckConstraint(
                    "CK_Record_Mileage",
                    "\"Mileage\" IS NULL OR \"Mileage\" >= 0"
                );
            });
        });
        b.Entity<MaintenanceRecordComponent>(e =>
        {
            e.HasQueryFilter(c => c.Record.Vehicle.UserId == currentUser.Id);
            e.HasKey(c => new { c.MaintenanceRecordId, c.VehicleComponentId });
            e.HasAlternateKey(c => new
            {
                c.MaintenanceRecordId,
                c.VehicleComponentId,
                c.VehicleId,
            });
            e.HasOne(c => c.Record)
                .WithMany(r => r.Components)
                .HasForeignKey(c => new { c.MaintenanceRecordId, c.VehicleId })
                .HasPrincipalKey(r => new { r.Id, r.VehicleId });
            e.HasOne(c => c.Component)
                .WithMany()
                .HasForeignKey(c => new { c.VehicleComponentId, c.VehicleId })
                .HasPrincipalKey(c => new { c.Id, c.VehicleId })
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MaintenanceRecordSubcomponent>(e =>
        {
            e.HasQueryFilter(s => s.Record.Vehicle.UserId == currentUser.Id);
            e.HasKey(s => new { s.MaintenanceRecordId, s.VehicleSubcomponentId });
            e.HasOne(s => s.Record)
                .WithMany(r => r.Subcomponents)
                .HasForeignKey(s => new { s.MaintenanceRecordId, s.VehicleId })
                .HasPrincipalKey(r => new { r.Id, r.VehicleId })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.ParentLink)
                .WithMany()
                .HasForeignKey(s => new
                {
                    s.MaintenanceRecordId,
                    s.VehicleComponentId,
                    s.VehicleId,
                })
                .HasPrincipalKey(c => new
                {
                    c.MaintenanceRecordId,
                    c.VehicleComponentId,
                    c.VehicleId,
                })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Subcomponent)
                .WithMany()
                .HasForeignKey(s => new
                {
                    s.VehicleSubcomponentId,
                    s.VehicleComponentId,
                    s.VehicleId,
                })
                .HasPrincipalKey(s => new
                {
                    s.Id,
                    s.VehicleComponentId,
                    s.VehicleId,
                })
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MaintenanceItem>(e =>
        {
            e.HasQueryFilter(i => i.Record.Vehicle.UserId == currentUser.Id);
            e.HasOne(i => i.Record)
                .WithMany(r => r.Items)
                .HasForeignKey(i => i.MaintenanceRecordId);
            e.Property(i => i.UnitCost).HasPrecision(16, 2);
            e.Property(i => i.Name).HasMaxLength(160);
            e.Ignore(i => i.Total);
            e.ToTable(t =>
                t.HasCheckConstraint("CK_Item_Values", "\"Quantity\" > 0 AND \"UnitCost\" >= 0")
            );
        });
        b.Entity<MaintenancePayment>(e =>
        {
            e.HasQueryFilter(p => p.Record.Vehicle.UserId == currentUser.Id);
            e.HasOne(p => p.Record)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.MaintenanceRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.MaintenanceRecordId, p.Scope }).IsUnique();
            e.Property(p => p.TotalCharged).HasPrecision(16, 2);
            e.ToTable(t =>
                t.HasCheckConstraint(
                    "CK_Payment_Values",
                    "\"Scope\" BETWEEN 0 AND 2 AND \"Method\" BETWEEN 0 AND 4 AND (\"Installments\" IS NULL OR \"Installments\" BETWEEN 1 AND 120) AND (\"TotalCharged\" IS NULL OR \"TotalCharged\" BETWEEN 0 AND 100000000)"
                )
            );
        });
        b.Entity<MaintenanceSchedule>(e =>
        {
            e.HasQueryFilter(s => s.Vehicle.UserId == currentUser.Id);
            e.HasOne(s => s.Vehicle).WithMany().HasForeignKey(s => s.VehicleId);
            e.HasOne(s => s.Component)
                .WithMany()
                .HasForeignKey(s => new { s.VehicleComponentId, s.VehicleId })
                .HasPrincipalKey(c => new { c.Id, c.VehicleId })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Subcomponent)
                .WithMany()
                .HasForeignKey(s => new
                {
                    s.VehicleSubcomponentId,
                    s.VehicleComponentId,
                    s.VehicleId,
                })
                .HasPrincipalKey(s => new
                {
                    s.Id,
                    s.VehicleComponentId,
                    s.VehicleId,
                })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new
            {
                s.VehicleId,
                s.Completed,
                s.DueDate,
            });
            e.Property(s => s.Description).HasMaxLength(300);
            e.ToTable(t =>
                t.HasCheckConstraint(
                    "CK_Schedule_Target",
                    "(\"DueDate\" IS NOT NULL OR \"DueMileage\" IS NOT NULL) AND (\"DueMileage\" IS NULL OR \"DueMileage\" >= 0)"
                )
            );
        });
        b.Entity<MaintenanceAttachment>(e =>
        {
            e.HasQueryFilter(a => a.Record.Vehicle.UserId == currentUser.Id);
            e.HasOne(a => a.Record)
                .WithMany(r => r.Attachments)
                .HasForeignKey(a => a.MaintenanceRecordId);
            e.Property(a => a.FileName).HasMaxLength(255);
            e.Property(a => a.StorageKey).HasMaxLength(80);
            e.Property(a => a.ContentType).HasMaxLength(100);
        });
        b.Entity<VehicleMileageHistory>(e =>
        {
            e.HasQueryFilter(h => h.Vehicle.UserId == currentUser.Id);
            e.HasOne(h => h.Vehicle).WithMany().HasForeignKey(h => h.VehicleId);
            e.HasIndex(h => new { h.VehicleId, h.RecordedAt });
            e.ToTable(t => t.HasCheckConstraint("CK_History_Mileage", "\"Mileage\" >= 0"));
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (
            var entry in ChangeTracker
                .Entries<Vehicle>()
                .Where(e => e.State == EntityState.Modified)
        )
            entry.Entity.ConcurrencyStamp = Guid.NewGuid();
        return base.SaveChangesAsync(cancellationToken);
    }
}
