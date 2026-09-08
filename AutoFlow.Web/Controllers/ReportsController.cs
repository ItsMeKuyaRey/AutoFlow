using AutoFlow.Web.Data;
using AutoFlow.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly AutoFlowDbContext _context;

    public ReportsController(AutoFlowDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(
            now.Year,
            now.Month,
            1);

        var nextMonthStart = monthStart.AddMonths(1);

        // Load existing AutoFlow records.
        var billings = await _context.Billings
            .Include(b => b.JobOrder)
                .ThenInclude(j => j!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .Include(b => b.Payments)
            .AsNoTracking()
            .ToListAsync();

        var jobOrders = await _context.JobOrders
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .AsNoTracking()
            .ToListAsync();

        var appointments = await _context.Appointments
            .Include(a => a.Vehicle)
                .ThenInclude(v => v!.Customer)
            .AsNoTracking()
            .ToListAsync();

        var parts = await _context.Parts
            .Include(p => p.Supplier)
            .AsNoTracking()
            .ToListAsync();

        // Billing totals.
        var todayBillings = billings
            .Where(b =>
                b.IssuedAt.Date == today)
            .ToList();

        var monthBillings = billings
            .Where(b =>
                b.IssuedAt >= monthStart &&
                b.IssuedAt < nextMonthStart)
            .ToList();

        var outstandingPayments = billings
            .Where(b =>
                !string.Equals(
                    b.Status,
                    "Paid",
                    StringComparison.OrdinalIgnoreCase))
            .Sum(b =>
                Math.Max(
                    0,
                    b.TotalAmount - b.AmountPaid));

        // Repair status.
        var completedRepairs = jobOrders
            .Count(j =>
                string.Equals(
                    j.Status,
                    "Completed",
                    StringComparison.OrdinalIgnoreCase));

        var activeRepairJobs = jobOrders
            .Count(j =>
                !string.Equals(
                    j.Status,
                    "Completed",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    j.Status,
                    "Cancelled",
                    StringComparison.OrdinalIgnoreCase));

        // Appointment statistics.
        var appointmentRows = appointments
            .OrderByDescending(a => a.AppointmentDate)
            .Take(10)
            .Select(a => new ReportRowViewModel
            {
                Label =
                    a.Vehicle?.Customer == null
                        ? "Unknown Customer"
                        : $"{a.Vehicle.Customer.FirstName} {a.Vehicle.Customer.LastName}",

                SecondaryLabel =
                    a.Vehicle == null
                        ? "Unknown Vehicle"
                        : $"{a.Vehicle.Make} {a.Vehicle.Model} - {a.Vehicle.PlateNumber}",

                Count = 1,
                Status = a.Status,
                Date = a.AppointmentDate
            })
            .ToList();

        // Daily sales report.
        var dailySales = todayBillings
            .GroupBy(b => b.IssuedAt.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new ReportRowViewModel
            {
                Label = g.Key.ToString("MMM dd, yyyy"),
                SecondaryLabel = "Daily Sales",
                Amount = g.Sum(b => b.TotalAmount),
                Count = g.Count(),
                Date = g.Key
            })
            .ToList();

        // Monthly sales report.
        var monthlySales = monthBillings
            .GroupBy(b =>
                new
                {
                    b.IssuedAt.Year,
                    b.IssuedAt.Month
                })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(g => new ReportRowViewModel
            {
                Label = new DateTime(
                    g.Key.Year,
                    g.Key.Month,
                    1).ToString("MMMM yyyy"),

                SecondaryLabel = "Monthly Sales",

                Amount = g.Sum(b => b.TotalAmount),

                Count = g.Count(),

                Date = new DateTime(
                    g.Key.Year,
                    g.Key.Month,
                    1)
            })
            .ToList();

        // Completed repair report.
        var completedRepairRows = jobOrders
            .Where(j =>
                string.Equals(
                    j.Status,
                    "Completed",
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(j => j.JobOrderDate)
            .Take(10)
            .Select(j => new ReportRowViewModel
            {
                Label =
                    $"Job Order #{j.JobOrderId}",

                SecondaryLabel =
                    j.Vehicle?.Customer == null
                        ? "Unknown Customer"
                        : $"{j.Vehicle.Customer.FirstName} {j.Vehicle.Customer.LastName}",

                Amount = j.TotalCost,

                Count = 1,

                Status = j.Status,

                Date = j.JobOrderDate
            })
            .ToList();

        // Active repair report.
        var activeRepairRows = jobOrders
            .Where(j =>
                !string.Equals(
                    j.Status,
                    "Completed",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    j.Status,
                    "Cancelled",
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(j => j.JobOrderDate)
            .Take(10)
            .Select(j => new ReportRowViewModel
            {
                Label =
                    $"Job Order #{j.JobOrderId}",

                SecondaryLabel =
                    j.Vehicle?.Customer == null
                        ? "Unknown Customer"
                        : $"{j.Vehicle.Customer.FirstName} {j.Vehicle.Customer.LastName}",

                Amount = j.TotalCost,

                Count = 1,

                Status = j.Status,

                Date = j.JobOrderDate
            })
            .ToList();

        // Low stock report.
        var lowStockPartRows = parts
            .Where(p => p.StockQuantity <= 5)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.Name)
            .Take(10)
            .Select(p => new ReportRowViewModel
            {
                Label = p.Name,

                SecondaryLabel =
                    string.IsNullOrWhiteSpace(p.PartNumber)
                        ? "No part number"
                        : p.PartNumber,

                Count = p.StockQuantity,

                Status =
                    p.StockQuantity == 0
                        ? "Out of Stock"
                        : "Low Stock"
            })
            .ToList();

        var viewModel = new ReportsViewModel
        {
            TotalSalesToday =
                todayBillings.Sum(b => b.TotalAmount),

            TotalSalesThisMonth =
                monthBillings.Sum(b => b.TotalAmount),

            OutstandingPayments =
                outstandingPayments,

            CompletedRepairs =
                completedRepairs,

            ActiveRepairJobs =
                activeRepairJobs,

            TotalAppointments =
                appointments.Count,

            TotalParts =
                parts.Count,

            LowStockParts =
                parts.Count(p => p.StockQuantity <= 5),

            DailySales =
                dailySales,

            MonthlySales =
                monthlySales,

            CompletedRepairRows =
                completedRepairRows,

            ActiveRepairRows =
                activeRepairRows,

            AppointmentRows =
                appointmentRows,

            LowStockPartRows =
                lowStockPartRows
        };

        return View(viewModel);
    }
}