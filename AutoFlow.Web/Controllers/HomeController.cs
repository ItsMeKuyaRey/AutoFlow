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
        var totalRevenue = await _context.Billings.SumAsync(b => b.TotalAmount);

        var activeJobCount = await _context.JobOrders
            .CountAsync(j => j.Status != "Completed" && j.Status != "Cancelled");

        var pendingAppointmentCount = await _context.Appointments
            .CountAsync(a => a.Status == "Scheduled");

        var lowStockAlertCount = await _context.Parts
            .CountAsync(p => p.StockQuantity <= 5);

        var totalJobCount = await _context.JobOrders.CountAsync();

        var todayJobCount = await _context.JobOrders
            .CountAsync(j => j.JobOrderDate.Date == DateTime.UtcNow.Date);

        var rescheduledCount = await _context.Appointments
            .CountAsync(a => a.Status == "Rescheduled");

        var jobStatusCounts = await _context.JobOrders
            .GroupBy(j => j.Status)
            .Select(g => new JobStatusCount
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        var recentJobs = await _context.JobOrders
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.JobOrderParts)
            .OrderByDescending(j => j.JobOrderDate)
            .Take(5)
            .Select(j => new RecentJobDto
            {
                JobOrderId = j.JobOrderId,
                VehicleMake = j.Vehicle!.Make,
                VehicleModel = j.Vehicle.Model,
                PlateNumber = j.Vehicle.PlateNumber,
                CustomerName = j.Vehicle.Customer!.FirstName + " " + j.Vehicle.Customer.LastName,
                Description = j.Description,
                TotalCost = j.LaborCost + j.JobOrderParts.Sum(jp => jp.Quantity * jp.UnitPriceAtTimeOfUse),
                Status = j.Status
            })
            .ToListAsync();

        var followUpAppointments = await _context.Appointments
            .Include(a => a.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Where(a => a.Status == "Scheduled" || a.Status == "Rescheduled")
            .OrderBy(a => a.AppointmentDate)
            .Take(3)
            .ToListAsync();

        var followUps = followUpAppointments.Select(a =>
        {
            var customer = a.Vehicle?.Customer;
            var firstName = customer?.FirstName ?? "Unknown";
            var lastName = customer?.LastName ?? "";
            var initials = (firstName.Length > 0 ? firstName[0].ToString() : "") +
                           (lastName.Length > 0 ? lastName[0].ToString() : "");

            var colors = new[] { "blue", "purple", "green", "orange" };
            var colorIndex = (a.AppointmentId) % colors.Length;

            return new FollowUpDto
            {
                CustomerName = firstName + " " + lastName,
                VehicleInfo = (a.Vehicle?.Make ?? "") + " " + (a.Vehicle?.Model ?? "") +
                              (a.Vehicle?.Year != null ? " · " + a.Vehicle.Year : ""),
                Status = a.Status,
                AvatarInitials = initials.ToUpper(),
                AvatarColor = colors[colorIndex]
            };
        }).ToList();

        var activeBayCount = await _context.JobOrders
            .CountAsync(j => j.Status == "Open" || j.Status == "In Progress");

        var vm = new DashboardViewModel
        {
            TotalRevenue = totalRevenue,
            ActiveJobCount = activeJobCount,
            PendingAppointmentCount = pendingAppointmentCount,
            LowStockAlertCount = lowStockAlertCount,
            TotalJobCount = totalJobCount,
            TodayJobCount = todayJobCount,
            RescheduledAppointmentCount = rescheduledCount,
            JobStatusCounts = jobStatusCounts,
            RecentJobs = recentJobs,
            FollowUps = followUps,
            ActiveBayCount = activeBayCount,
            TotalBays = 4
        };

        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
