using AutoFlow.Web.Data;
using AutoFlow.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private static readonly string[] ReportTypes =
    {
        "Monthly Sales",
        "Daily Sales",
        "Outstanding Payments",
        "Completed Repairs",
        "Active Repair Jobs",
        "Appointment Statistics",
        "Parts Inventory",
        "Low Stock Parts"
    };

    private readonly AutoFlowDbContext _context;

    public ReportsController(AutoFlowDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? report, DateTime? startDate, DateTime? endDate)
    {
        var now = DateTime.Now;
        var defaultStart = new DateTime(now.Year, now.Month, 1);
        var defaultEnd = defaultStart.AddMonths(1).AddDays(-1);

        var selectedReport = ReportTypes.Contains(report ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? ReportTypes.First(x => string.Equals(x, report, StringComparison.OrdinalIgnoreCase))
            : "Monthly Sales";

        var start = (startDate ?? defaultStart).Date;
        var end = (endDate ?? defaultEnd).Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var endExclusive = end.AddDays(1);

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

        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var todayBillings = billings.Where(b => b.IssuedAt.Date >= today && b.IssuedAt.Date < tomorrow).ToList();
        var monthBillings = billings.Where(b => b.IssuedAt >= monthStart && b.IssuedAt < nextMonthStart).ToList();
        var selectedBillings = billings.Where(b => b.IssuedAt >= start && b.IssuedAt < endExclusive).ToList();
        var selectedJobs = jobOrders.Where(j => j.JobOrderDate >= start && j.JobOrderDate < endExclusive).ToList();
        var selectedAppointments = appointments.Where(a => a.AppointmentDate >= start && a.AppointmentDate < endExclusive).ToList();

        var outstandingPayments = billings
            .Where(b => !string.Equals(b.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            .Sum(b => Math.Max(0, b.TotalAmount - b.AmountPaid));

        var completedRepairs = jobOrders.Count(j => string.Equals(j.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        var activeRepairJobs = jobOrders.Count(j =>
            !string.Equals(j.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(j.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));

        var dailySales = todayBillings
            .GroupBy(b => b.IssuedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new ReportRowViewModel
            {
                Label = g.Key.ToString("MMM dd, yyyy"),
                SecondaryLabel = "Daily Sales",
                Amount = g.Sum(b => b.TotalAmount),
                Count = g.Count(),
                Date = g.Key
            }).ToList();

        var monthlySales = monthBillings
            .GroupBy(b => new { b.IssuedAt.Year, b.IssuedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new ReportRowViewModel
            {
                Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                SecondaryLabel = "Monthly Sales",
                Amount = g.Sum(b => b.TotalAmount),
                Count = g.Count(),
                Date = new DateTime(g.Key.Year, g.Key.Month, 1)
            }).ToList();

        var completedRepairRows = selectedJobs
            .Where(j => string.Equals(j.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(j => j.JobOrderDate)
            .Take(50)
            .Select(JobRow)
            .ToList();

        var activeRepairRows = selectedJobs
            .Where(j => !string.Equals(j.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(j.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(j => j.JobOrderDate)
            .Take(50)
            .Select(JobRow)
            .ToList();

        var appointmentRows = selectedAppointments
            .OrderByDescending(a => a.AppointmentDate)
            .Take(50)
            .Select(a => new ReportRowViewModel
            {
                Label = a.Vehicle?.Customer == null ? "Unknown Customer" : $"{a.Vehicle.Customer.FirstName} {a.Vehicle.Customer.LastName}",
                SecondaryLabel = a.Vehicle == null ? "Unknown Vehicle" : $"{a.Vehicle.Make} {a.Vehicle.Model} - {a.Vehicle.PlateNumber}",
                Customer = a.Vehicle?.Customer == null ? "Unknown Customer" : $"{a.Vehicle.Customer.FirstName} {a.Vehicle.Customer.LastName}",
                Vehicle = a.Vehicle == null ? "Unknown Vehicle" : $"{a.Vehicle.Make} {a.Vehicle.Model} - {a.Vehicle.PlateNumber}",
                Count = 1,
                Status = a.Status,
                Date = a.AppointmentDate
            }).ToList();

        var lowStockPartRows = parts
            .Where(p => p.StockQuantity <= 5)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.Name)
            .Take(50)
            .Select(p => new ReportRowViewModel
            {
                Label = p.Name,
                SecondaryLabel = string.IsNullOrWhiteSpace(p.PartNumber) ? "No part number" : p.PartNumber,
                Count = p.StockQuantity,
                Status = p.StockQuantity == 0 ? "Out of Stock" : "Low Stock"
            }).ToList();

        var reportRows = new List<ReportRowViewModel>();
        var chartLabels = new List<string>();
        var chartValues = new List<decimal>();
        var chartTitle = selectedReport;
        var chartSubtitle = $"{start:MMM d, yyyy} – {end:MMM d, yyyy}";
        var summaryLabel = "Total Sales";
        decimal reportTotal = 0;
        decimal laborRevenue = 0;
        decimal partsRevenue = 0;

        switch (selectedReport)
        {
            case "Monthly Sales":
                // One point per day keeps the graph useful for the exact selected month/date range.
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    var dayBillings = selectedBillings.Where(b => b.IssuedAt.Date == date).ToList();
                    chartLabels.Add(date.ToString("MMM d"));
                    chartValues.Add(dayBillings.Sum(b => b.TotalAmount));
                }

                reportRows = selectedBillings
                    .OrderByDescending(b => b.IssuedAt)
                    .Take(100)
                    .Select(BillingRow)
                    .ToList();
                reportTotal = selectedBillings.Sum(b => b.TotalAmount);
                laborRevenue = selectedBillings.Sum(b => b.LaborCost);
                partsRevenue = selectedBillings.Sum(b => b.PartsCost);
                summaryLabel = "Total Sales";
                break;

            case "Daily Sales":
                foreach (var date in DateRange(start, end))
                {
                    var dayBillings = selectedBillings.Where(b => b.IssuedAt.Date == date).ToList();
                    chartLabels.Add(date.ToString("MMM d"));
                    chartValues.Add(dayBillings.Sum(b => b.TotalAmount));
                }

                reportRows = selectedBillings.OrderByDescending(b => b.IssuedAt).Take(100).Select(BillingRow).ToList();
                reportTotal = selectedBillings.Sum(b => b.TotalAmount);
                laborRevenue = selectedBillings.Sum(b => b.LaborCost);
                partsRevenue = selectedBillings.Sum(b => b.PartsCost);
                summaryLabel = "Total Sales";
                break;

            case "Outstanding Payments":
                var outstanding = selectedBillings
                    .Where(b => !string.Equals(b.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Select(b => new { Billing = b, Balance = Math.Max(0, b.TotalAmount - b.AmountPaid) })
                    .Where(x => x.Balance > 0)
                    .OrderByDescending(x => x.Balance)
                    .ToList();

                foreach (var group in outstanding.GroupBy(x => x.Billing.IssuedAt.Date).OrderBy(g => g.Key))
                {
                    chartLabels.Add(group.Key.ToString("MMM d"));
                    chartValues.Add(group.Sum(x => x.Balance));
                }

                reportRows = outstanding.Take(100).Select(x =>
                {
                    var row = BillingRow(x.Billing);
                    row.Amount = x.Balance;
                    return row;
                }).ToList();
                reportTotal = outstanding.Sum(x => x.Balance);
                summaryLabel = "Outstanding Balance";
                chartTitle = "Outstanding Payments";
                break;

            case "Completed Repairs":
                reportRows = completedRepairRows;
                foreach (var group in completedRepairRows.Where(r => r.Date.HasValue).GroupBy(r => r.Date!.Value.Date).OrderBy(g => g.Key))
                {
                    chartLabels.Add(group.Key.ToString("MMM d"));
                    chartValues.Add(group.Count());
                }
                reportTotal = completedRepairRows.Count;
                summaryLabel = "Completed Repairs";
                break;

            case "Active Repair Jobs":
                reportRows = activeRepairRows;
                foreach (var group in activeRepairRows.Where(r => r.Date.HasValue).GroupBy(r => r.Date!.Value.Date).OrderBy(g => g.Key))
                {
                    chartLabels.Add(group.Key.ToString("MMM d"));
                    chartValues.Add(group.Count());
                }
                reportTotal = activeRepairRows.Count;
                summaryLabel = "Active Jobs";
                break;

            case "Appointment Statistics":
                reportRows = appointmentRows;
                foreach (var group in appointmentRows.Where(r => r.Date.HasValue).GroupBy(r => r.Date!.Value.Date).OrderBy(g => g.Key))
                {
                    chartLabels.Add(group.Key.ToString("MMM d"));
                    chartValues.Add(group.Count());
                }
                reportTotal = appointmentRows.Count;
                summaryLabel = "Appointments";
                break;

            case "Parts Inventory":
                reportRows = parts
                    .OrderBy(p => p.Name)
                    .Take(100)
                    .Select(p => new ReportRowViewModel
                    {
                        Label = p.Name,
                        SecondaryLabel = string.IsNullOrWhiteSpace(p.PartNumber) ? "No part number" : p.PartNumber,
                        Count = p.StockQuantity,
                        Status = p.StockQuantity == 0 ? "Out of Stock" : p.StockQuantity <= 5 ? "Low Stock" : "In Stock"
                    }).ToList();
                foreach (var item in reportRows.Take(20))
                {
                    chartLabels.Add(item.Label.Length > 12 ? item.Label[..12] : item.Label);
                    chartValues.Add(item.Count);
                }
                reportTotal = parts.Sum(p => p.StockQuantity);
                summaryLabel = "Units in Stock";
                chartTitle = "Parts Inventory";
                break;

            case "Low Stock Parts":
                reportRows = lowStockPartRows;
                foreach (var item in reportRows)
                {
                    chartLabels.Add(item.Label.Length > 12 ? item.Label[..12] : item.Label);
                    chartValues.Add(item.Count);
                }
                reportTotal = lowStockPartRows.Count;
                summaryLabel = "Low Stock Parts";
                chartTitle = "Low Stock Parts";
                break;
        }

        var viewModel = new ReportsViewModel
        {
            SelectedReport = selectedReport,
            StartDate = start,
            EndDate = end,
            ReportDescription = "Generate database-driven reports from AutoFlow's operational records.",
            ChartTitle = chartTitle,
            ChartSubtitle = chartSubtitle,
            SummaryLabel = summaryLabel,
            TotalSalesToday = todayBillings.Sum(b => b.TotalAmount),
            TotalSalesThisMonth = monthBillings.Sum(b => b.TotalAmount),
            OutstandingPayments = outstandingPayments,
            CompletedRepairs = completedRepairs,
            ActiveRepairJobs = activeRepairJobs,
            TotalAppointments = appointments.Count,
            TotalParts = parts.Count,
            LowStockParts = parts.Count(p => p.StockQuantity <= 5),
            ReportTotal = reportTotal,
            LaborRevenue = laborRevenue,
            PartsRevenue = partsRevenue,
            ReportTransactionCount = reportRows.Count,
            AverageTicket = reportRows.Count > 0 ? reportTotal / reportRows.Count : 0,
            ChartLabels = chartLabels,
            ChartValues = chartValues,
            ReportRows = reportRows,
            DailySales = dailySales,
            MonthlySales = monthlySales,
            CompletedRepairRows = completedRepairRows,
            ActiveRepairRows = activeRepairRows,
            AppointmentRows = appointmentRows,
            LowStockPartRows = lowStockPartRows
        };

        ViewBag.ReportTypes = ReportTypes;
        return View(viewModel);
    }

    private static IEnumerable<DateTime> DateRange(DateTime start, DateTime end)
    {
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static ReportRowViewModel BillingRow(AutoFlow.Web.Models.Billing billing)
    {
        var customer = billing.JobOrder?.Vehicle?.Customer;
        var vehicle = billing.JobOrder?.Vehicle;
        return new ReportRowViewModel
        {
            Label = billing.InvoiceNumber,
            Reference = billing.InvoiceNumber,
            SecondaryLabel = billing.JobOrder == null ? "Billing record" : $"Job Order #{billing.JobOrderId}",
            Customer = customer == null ? "Unknown Customer" : $"{customer.FirstName} {customer.LastName}",
            Vehicle = vehicle == null ? "Unknown Vehicle" : $"{vehicle.Make} {vehicle.Model}",
            Amount = billing.TotalAmount,
            LaborAmount = billing.LaborCost,
            PartsAmount = billing.PartsCost,
            Count = 1,
            Status = billing.Status,
            Date = billing.IssuedAt
        };
    }

    private static ReportRowViewModel JobRow(AutoFlow.Web.Models.JobOrder job)
    {
        var customer = job.Vehicle?.Customer;
        return new ReportRowViewModel
        {
            Label = $"JO-{job.JobOrderId:0000}",
            Reference = $"JO-{job.JobOrderId:0000}",
            SecondaryLabel = job.Description ?? "Repair job order",
            Customer = customer == null ? "Unknown Customer" : $"{customer.FirstName} {customer.LastName}",
            Vehicle = job.Vehicle == null ? "Unknown Vehicle" : $"{job.Vehicle.Make} {job.Vehicle.Model}",
            Amount = job.TotalCost,
            LaborAmount = job.LaborCost,
            PartsAmount = job.PartsCost,
            Count = 1,
            Status = job.Status,
            Date = job.JobOrderDate
        };
    }
}
