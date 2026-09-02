using AutoFlow.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Web.Data;

public class AutoFlowDbContext : DbContext
{
    public AutoFlowDbContext(DbContextOptions<AutoFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<ServiceRecord> ServiceRecords => Set<ServiceRecord>();

    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<JobOrder> JobOrders => Set<JobOrder>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<Billing> Billings => Set<Billing>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<JobOrderPart> JobOrderParts => Set<JobOrderPart>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Supplier>().ToTable("Supplier");

        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.Customer)
            .WithMany(c => c.Vehicles)
            .HasForeignKey(v => v.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceRecord>()
            .Property(sr => sr.ServiceDate)
            .HasColumnType("timestamp without time zone");

        modelBuilder.Entity<ServiceRecord>()
            .HasOne(sr => sr.Vehicle)
            .WithMany(v => v.ServiceRecords)
            .HasForeignKey(sr => sr.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Appointment>()
            .Property(a => a.AppointmentDate)
            .HasColumnType("timestamp without time zone");

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Vehicle)
            .WithMany()
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobOrder>()
                .HasOne(j => j.Vehicle)
                .WithMany()
                .HasForeignKey(j => j.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobOrder>()
                .HasOne(j => j.Appointment)
                .WithMany()
                .HasForeignKey(j => j.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);

                modelBuilder.Entity<JobOrderPart>()
                .HasOne(jp => jp.JobOrder)
                .WithMany(j => j.JobOrderParts)
                .HasForeignKey(jp => jp.JobOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobOrderPart>()
                .HasOne(jp => jp.Part)
                .WithMany()
                .HasForeignKey(jp => jp.PartId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Billing>()
                .HasOne(b => b.JobOrder)
                .WithOne()
                .HasForeignKey<Billing>(b => b.JobOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Billing>()
                .HasIndex(b => b.JobOrderId)
                .IsUnique();

            modelBuilder.Entity<Billing>()
                .HasIndex(b => b.InvoiceNumber)
                .IsUnique();

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Billing)
                .WithMany(b => b.Payments)
                .HasForeignKey(p => p.BillingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Part>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Parts)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
    }
}