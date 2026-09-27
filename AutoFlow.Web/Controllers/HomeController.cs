using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;
using AutoFlow.Web.ViewModels;

namespace AutoFlow.Web.Controllers;

public class HomeController : Controller
{
    private readonly AutoFlowDbContext _context;

    public HomeController(AutoFlowDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var todayDate = DateTime.Now.Date;
        var tomorrowDate = todayDate.AddDays(1);

        var weekStartDate =
            todayDate.AddDays(-(int)todayDate.DayOfWeek);

        var weekEndDate =
            weekStartDate.AddDays(7);

        var monthStartDate =
            new DateTime(
                todayDate.Year,
                todayDate.Month,
                1);

        var nextMonthDate =
            monthStartDate.AddMonths(1);

        // Core module counts.
        var totalCustomerCount =
            await _context.Customers
                .AsNoTracking()
                .CountAsync();

        var totalVehicleCount =
            await _context.Vehicles
                .AsNoTracking()
                .CountAsync();

        var totalAppointmentCount =
            await _context.Appointments
                .AsNoTracking()
                .CountAsync();

        var totalJobCount =
            await _context.JobOrders
                .AsNoTracking()
                .CountAsync();

        var totalPartCount =
            await _context.Parts
                .AsNoTracking()
                .CountAsync();

        var totalServiceRecordCount =
            await _context.ServiceRecords
                .AsNoTracking()
                .CountAsync();

        // Load appointment dates without sending DateTime parameters to PostgreSQL.
        var allAppointments =
            await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Customer)
                .ToListAsync();

        // Appointment metrics.
        var pendingAppointmentCount =
            allAppointments.Count(
                a => a.Status == "Scheduled");

        var todayAppointmentRecords =
            allAppointments
                .Where(a =>
                    a.AppointmentDate.Date >= todayDate &&
                    a.AppointmentDate.Date < tomorrowDate)
                .OrderBy(a => a.AppointmentDate)
                .Take(10)
                .ToList();

        var todayAppointmentCount =
            allAppointments.Count(
                a =>
                    a.AppointmentDate.Date >= todayDate &&
                    a.AppointmentDate.Date < tomorrowDate);

        var rescheduledCount =
            allAppointments.Count(
                a => a.Status == "Rescheduled");

        var scheduledThisWeek =
            allAppointments.Count(
                a =>
                    a.AppointmentDate.Date >= weekStartDate &&
                    a.AppointmentDate.Date < weekEndDate &&
                    (
                        a.Status == "Scheduled" ||
                        a.Status == "Rescheduled"
                    ));

        // Load job orders without sending DateTime parameters to PostgreSQL.
        var allJobOrders =
            await _context.JobOrders
                .AsNoTracking()
                .Include(j => j.Vehicle)
                    .ThenInclude(v => v!.Customer)
                .Include(j => j.JobOrderParts)
                .ToListAsync();

        // Repair job metrics.
        var activeJobCount =
            allJobOrders.Count(
                j =>
                    j.Status != "Completed" &&
                    j.Status != "Cancelled");

        var openJobCount =
            allJobOrders.Count(
                j => j.Status == "Open");

        var inProgressJobCount =
            allJobOrders.Count(
                j => j.Status == "In Progress");

        var completedJobCount =
            allJobOrders.Count(
                j => j.Status == "Completed");

        var todayJobCount =
            allJobOrders.Count(
                j =>
                    j.JobOrderDate.Date >= todayDate &&
                    j.JobOrderDate.Date < tomorrowDate);

        var completedThisMonth =
            allJobOrders.Count(
                j =>
                    j.Status == "Completed" &&
                    j.JobOrderDate.Date >= monthStartDate &&
                    j.JobOrderDate.Date < nextMonthDate);

        var jobStatusCounts =
            allJobOrders
                .GroupBy(j => j.Status)
                .Select(g => new JobStatusCount
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();

        // Inventory metrics.
        var lowStockAlertCount =
            await _context.Parts
                .AsNoTracking()
                .CountAsync(
                    p => p.StockQuantity <= 5);

        var totalStockQuantity =
            await _context.Parts
                .AsNoTracking()
                .SumAsync(
                    p => (int?)p.StockQuantity) ?? 0;

        var inventoryValue =
            await _context.Parts
                .AsNoTracking()
                .SumAsync(
                    p =>
                        (decimal?)
                        (
                            p.StockQuantity *
                            p.UnitPrice
                        )) ?? 0m;

        // Billing metrics.
        var totalBillingCount =
            await _context.Billings
                .AsNoTracking()
                .CountAsync();

        var totalBilled =
            await _context.Billings
                .AsNoTracking()
                .SumAsync(
                    b => (decimal?)b.TotalAmount) ?? 0m;

        var totalPaid =
            await _context.Billings
                .AsNoTracking()
                .SumAsync(
                    b => (decimal?)b.AmountPaid) ?? 0m;

        var outstandingAmount =
            Math.Max(
                0m,
                totalBilled - totalPaid);

        var outstandingInvoiceCount =
            await _context.Billings
                .AsNoTracking()
                .CountAsync(
                    b =>
                        b.TotalAmount > b.AmountPaid &&
                        b.Status != "Cancelled");

        var collectionRate =
            totalBilled > 0m
                ? Math.Round(
                    (totalPaid / totalBilled) * 100m,
                    2)
                : 0m;

        // Revenue represents money actually collected.
        var totalRevenue = totalPaid;

        // Recent repair jobs.
        var recentJobs =
            allJobOrders
                .OrderByDescending(
                    j => j.JobOrderDate)
                .Take(5)
                .Select(j => new RecentJobDto
                {
                    JobOrderId =
                        j.JobOrderId,

                    VehicleMake =
                        j.Vehicle != null
                            ? j.Vehicle.Make
                            : string.Empty,

                    VehicleModel =
                        j.Vehicle != null
                            ? j.Vehicle.Model
                            : string.Empty,

                    PlateNumber =
                        j.Vehicle != null
                            ? j.Vehicle.PlateNumber
                            : string.Empty,

                    CustomerName =
                        j.Vehicle?.Customer != null
                            ? (
                                j.Vehicle.Customer.FirstName +
                                " " +
                                j.Vehicle.Customer.LastName
                              ).Trim()
                            : "Unknown Customer",

                    Description =
                        j.Description,

                    TotalCost =
                        j.LaborCost +
                        (
                            j.JobOrderParts?.Sum(
                                jp =>
                                    jp.Quantity *
                                    jp.UnitPriceAtTimeOfUse)
                            ?? 0m
                        ),

                    Status =
                        j.Status
                })
                .ToList();

        // Today's appointments.
        var todayAppointments =
            todayAppointmentRecords
                .Select(a => new TodayAppointmentDto
                {
                    AppointmentId =
                        a.AppointmentId,

                    Time =
                        a.AppointmentDate
                            .ToString("hh:mm tt"),

                    CustomerName =
                        a.Vehicle?.Customer != null
                            ? (
                                a.Vehicle.Customer.FirstName +
                                " " +
                                a.Vehicle.Customer.LastName
                              ).Trim()
                            : "Unknown Customer",

                    VehicleInfo =
                        a.Vehicle != null
                            ? (
                                (
                                    a.Vehicle.Make +
                                    " " +
                                    a.Vehicle.Model
                                ).Trim() +
                                (
                                    a.Vehicle.Year != null
                                        ? " · " +
                                          a.Vehicle.Year
                                        : string.Empty
                                )
                              )
                            : "Unknown Vehicle",

                    Concern =
                        a.Reason ??
                        string.Empty,

                    Status =
                        a.Status
                })
                .ToList();

        // Active CRM follow-ups from the Customer Follow-ups module.
        var allCustomerFollowUps =
            await _context.CustomerFollowUps
                .AsNoTracking()
                .Include(f => f.Customer)
                .Include(f => f.Vehicle)
                .ToListAsync();

        var activeCustomerFollowUps =
            allCustomerFollowUps
                .Where(
                    f =>
                        f.Status != "Completed" &&
                        f.Status != "Cancelled")
                .OrderBy(
                    f => f.NextFollowUpDate ?? f.FollowUpDate)
                .ThenBy(
                    f => f.FollowUpDate)
                .Take(5)
                .ToList();

        var followUps =
            activeCustomerFollowUps
                .Select(f =>
                {
                    var customer =
                        f.Customer;

                    var firstName =
                        customer?.FirstName ??
                        "Unknown";

                    var lastName =
                        customer?.LastName ??
                        string.Empty;

                    var initials =
                        (
                            firstName.Length > 0
                                ? firstName[0].ToString()
                                : string.Empty
                        ) +
                        (
                            lastName.Length > 0
                                ? lastName[0].ToString()
                                : string.Empty
                        );

                    var colors =
                        new[]
                        {
                            "blue",
                            "purple",
                            "green",
                            "orange"
                        };

                    var colorIndex =
                        f.FollowUpId %
                        colors.Length;

                    return new FollowUpDto
                    {
                        CustomerName =
                            (
                                firstName +
                                " " +
                                lastName
                            ).Trim(),

                        VehicleInfo =
                            (
                                (
                                    f.Vehicle?.Make ??
                                    string.Empty
                                ) +
                                " " +
                                (
                                    f.Vehicle?.Model ??
                                    string.Empty
                                )
                            ).Trim() +
                            (
                                f.Vehicle?.Year != null
                                    ? " · " +
                                      f.Vehicle.Year
                                    : string.Empty
                            ),

                        Status =
                            f.Status,

                        AvatarInitials =
                            initials.ToUpperInvariant(),

                        AvatarColor =
                            colors[colorIndex]
                    };
                })
                .ToList();

        // Recent job activity.
        var recentJobActivities =
            allJobOrders
                .OrderByDescending(
                    j => j.UpdatedAt)
                .Take(5)
                .Select(j => new ActivityFeedDto
                {
                    ActivityType =
                        "Job Order",

                    Title =
                        "Job Order #" +
                        j.JobOrderId,

                    Description =
                        string.IsNullOrWhiteSpace(
                            j.Description)
                            ? "Repair job updated."
                            : j.Description,

                    Module =
                        "Repair Job Orders",

                    ActivityDate =
                        j.UpdatedAt,

                    ActivityColor =
                        "purple"
                })
                .ToList();

        // Recent appointment activity.
        var recentAppointmentActivities =
            allAppointments
                .OrderByDescending(
                    a => a.UpdatedAt)
                .Take(5)
                .Select(a => new ActivityFeedDto
                {
                    ActivityType =
                        "Appointment",

                    Title =
                        "Appointment #" +
                        a.AppointmentId,

                    Description =
                        string.IsNullOrWhiteSpace(
                            a.Reason)
                            ? "Appointment updated."
                            : a.Reason,

                    Module =
                        "Service Appointments",

                    ActivityDate =
                        a.UpdatedAt,

                    ActivityColor =
                        "blue"
                })
                .ToList();

        // Recent billing activity.
        var recentBillingActivities =
            await _context.Billings
                .AsNoTracking()
                .OrderByDescending(
                    b => b.UpdatedAt)
                .Take(5)
                .Select(b => new ActivityFeedDto
                {
                    ActivityType =
                        "Billing",

                    Title =
                        "Invoice " +
                        b.InvoiceNumber,

                    Description =
                        "Billing record updated.",

                    Module =
                        "Billing & Payments",

                    ActivityDate =
                        b.UpdatedAt,

                    ActivityColor =
                        "green"
                })
                .ToListAsync();

        var recentActivities =
            recentJobActivities
                .Concat(
                    recentAppointmentActivities)
                .Concat(
                    recentBillingActivities)
                .OrderByDescending(
                    a => a.ActivityDate)
                .Take(8)
                .ToList();

        // Seven-day appointment trend.
        var trendStartDate =
            todayDate.AddDays(-6);

        var appointmentTrend =
            Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var date =
                        trendStartDate.AddDays(offset);

                    var count =
                        allAppointments.Count(
                            a =>
                                a.AppointmentDate.Date ==
                                date.Date);

                    return new DashboardTrendPointDto
                    {
                        Date =
                            date,

                        Count =
                            count
                    };
                })
                .ToList();

        // Real reporting snapshot.
        var reportSnapshot =
            new List<ReportSnapshotDto>
            {
                new ReportSnapshotDto
                {
                    Label =
                        "Total Revenue",

                    Value =
                        totalRevenue,

                    Count =
                        totalBillingCount,

                    DisplayValue =
                        totalRevenue.ToString("C")
                },

                new ReportSnapshotDto
                {
                    Label =
                        "Outstanding",

                    Value =
                        outstandingAmount,

                    Count =
                        outstandingInvoiceCount,

                    DisplayValue =
                        outstandingAmount.ToString("C")
                },

                new ReportSnapshotDto
                {
                    Label =
                        "Completed Jobs",

                    Value =
                        completedThisMonth,

                    Count =
                        completedThisMonth,

                    DisplayValue =
                        completedThisMonth.ToString()
                },

                new ReportSnapshotDto
                {
                    Label =
                        "Inventory Value",

                    Value =
                        inventoryValue,

                    Count =
                        totalPartCount,

                    DisplayValue =
                        inventoryValue.ToString("C")
                }
            };

        // Active bays are represented by currently active repair jobs.
        var activeBayCount =
            allJobOrders.Count(
                j =>
                    j.Status == "Open" ||
                    j.Status == "In Progress");

        var vm =
            new DashboardViewModel
            {
                // Financial metrics.
                TotalRevenue =
                    totalRevenue,

                TotalBilled =
                    totalBilled,

                TotalPaid =
                    totalPaid,

                OutstandingAmount =
                    outstandingAmount,

                CollectionRate =
                    collectionRate,

                // Core module counts.
                TotalCustomerCount =
                    totalCustomerCount,

                TotalVehicleCount =
                    totalVehicleCount,

                TotalAppointmentCount =
                    totalAppointmentCount,

                TotalJobCount =
                    totalJobCount,

                TotalPartCount =
                    totalPartCount,

                TotalServiceRecordCount =
                    totalServiceRecordCount,

                // Operational metrics.
                ActiveJobCount =
                    activeJobCount,

                PendingAppointmentCount =
                    pendingAppointmentCount,

                LowStockAlertCount =
                    lowStockAlertCount,

                TodayJobCount =
                    todayJobCount,

                TodayAppointmentCount =
                    todayAppointmentCount,

                RescheduledAppointmentCount =
                    rescheduledCount,

                // Billing metrics.
                TotalBillingCount =
                    totalBillingCount,

                OutstandingInvoiceCount =
                    outstandingInvoiceCount,

                // Inventory metrics.
                TotalStockQuantity =
                    totalStockQuantity,

                InventoryValue =
                    inventoryValue,

                // Job status metrics.
                CompletedJobCount =
                    completedJobCount,

                OpenJobCount =
                    openJobCount,

                InProgressJobCount =
                    inProgressJobCount,

                // Scheduling metrics.
                ScheduledThisWeek =
                    scheduledThisWeek,

                CompletedThisMonth =
                    completedThisMonth,

                // Bay metrics.
                ActiveBayCount =
                    activeBayCount,

                TotalBays =
                    4,

                // Dashboard collections.
                JobStatusCounts =
                    jobStatusCounts,

                RecentJobs =
                    recentJobs,

                FollowUps =
                    followUps,

                TodayAppointments =
                    todayAppointments,

                RecentActivities =
                    recentActivities,

                AppointmentTrend =
                    appointmentTrend,

                ReportSnapshot =
                    reportSnapshot,

                // Part currently has no Category property.
                TopMovingCategory =
                    "Not available",

                TopMovingCategoryPercentage =
                    0
            };

        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Error()
    {
        return View(
            new ErrorViewModel
            {
                RequestId =
                    Activity.Current?.Id ??
                    HttpContext.TraceIdentifier
            });
    }
}