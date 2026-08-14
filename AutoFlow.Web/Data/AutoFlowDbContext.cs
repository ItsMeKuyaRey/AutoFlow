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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.Customer)
            .WithMany(c => c.Vehicles)
            .HasForeignKey(v => v.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}