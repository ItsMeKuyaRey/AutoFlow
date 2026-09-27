using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;
using AutoFlow.Web.ViewModels;

namespace AutoFlow.Web.Controllers;

public class ServiceRecordsController : Controller
{
    private static readonly Regex JobOrderMarkerRegex =
        new(@"Generated from Job Order #(\d+)\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AutoFlowDbContext _context;

    public ServiceRecordsController(AutoFlowDbContext context)
    {
        _context = context;
    }

    // Service History is a historical/read-only view of completed repair work.
    // The normal source of these records is JobOrdersController when a job is completed.
    // This small sync also backfills older completed jobs that were completed before the
    // automatic service-record workflow was in place.
    public async Task<IActionResult> Index(string? open, int? id)
    {
        await BackfillCompletedServiceHistoryAsync();

        var serviceRecords = await _context.ServiceRecords
            .AsNoTracking()
            .Include(s => s.Vehicle)
                .ThenInclude(v => v.Customer)
            .Where(s =>
                s.Status.Equals("Completed") ||
                s.Status.Equals("Archived"))
            .OrderByDescending(s => s.ServiceDate)
            .ToListAsync();

        var jobOrderIds = serviceRecords
            .Select(s => ExtractJobOrderId(s.Notes))
            .Where(idValue => idValue.HasValue)
            .Select(idValue => idValue!.Value)
            .Distinct()
            .ToList();

        var jobOrders = await _context.JobOrders
            .AsNoTracking()
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .Where(j => jobOrderIds.Contains(j.JobOrderId))
            .ToDictionaryAsync(j => j.JobOrderId);

        var items = serviceRecords
            .Select(record => new ServiceHistoryItemViewModel
            {
                ServiceRecord = record,
                JobOrder = ExtractJobOrderId(record.Notes) is int jobId && jobOrders.TryGetValue(jobId, out var job)
                    ? job
                    : null
            })
            .ToList();

        var page = new ServiceHistoryPageViewModel
        {
            Records = items.Where(i => !i.IsArchived).ToList(),
            ArchivedRecords = items.Where(i => i.IsArchived).ToList(),
            Open = open ?? string.Empty
        };

        if (id.HasValue &&
            (string.Equals(open, "details", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(open, "archive", StringComparison.OrdinalIgnoreCase)))
        {
            page.DrawerRecord = items.FirstOrDefault(i => i.ServiceRecordId == id.Value);
        }

        return View(page);
    }

    // Compatibility route: Service History records are now generated automatically.
    public IActionResult Create() => RedirectToAction(nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ServiceRecord serviceRecord) => RedirectToAction(nameof(Index));

    // Compatibility route: corrections should be made from the originating Repair Job Order.
    public IActionResult Edit(int? id) =>
        id.HasValue
            ? RedirectToAction(nameof(Index), new { open = "details", id })
            : RedirectToAction(nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ServiceRecord serviceRecord) =>
        RedirectToAction(nameof(Index), new { open = "details", id });

    // Kept for existing links/bookmarks. Details now uses the Service History drawer.
    public IActionResult Details(int? id) =>
        id.HasValue
            ? RedirectToAction(nameof(Index), new { open = "details", id })
            : RedirectToAction(nameof(Index));

    public IActionResult Delete(int? id) =>
        id.HasValue
            ? RedirectToAction(nameof(Index), new { open = "archive", id })
            : RedirectToAction(nameof(Index));

    public IActionResult Archive(int? id) =>
        id.HasValue
            ? RedirectToAction(nameof(Index), new { open = "archive", id })
            : RedirectToAction(nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveConfirmed(int id)
    {
        var serviceRecord = await _context.ServiceRecords.FindAsync(id);

        if (serviceRecord == null)
        {
            return NotFound();
        }

        serviceRecord.Status = "Archived";
        serviceRecord.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Service record archived successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteConfirmed(int id) => ArchiveConfirmed(id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var serviceRecord = await _context.ServiceRecords.FindAsync(id);

        if (serviceRecord == null)
        {
            return NotFound();
        }

        if (serviceRecord.Status.Equals("Archived", StringComparison.OrdinalIgnoreCase))
        {
            serviceRecord.Status = "Completed";
            serviceRecord.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Service record restored successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task BackfillCompletedServiceHistoryAsync()
    {
        var completedJobs = await _context.JobOrders
            .AsNoTracking()
            .Where(j =>
                !j.IsArchived &&
                j.Status.Equals("Completed"))
            .Select(j => new
            {
                j.JobOrderId,
                j.VehicleId,
                j.JobOrderDate,
                j.Description,
                j.Diagnosis,
                j.Notes
            })
            .ToListAsync();

        if (completedJobs.Count == 0)
        {
            return;
        }

        var existingMarkers = await _context.ServiceRecords
            .AsNoTracking()
            .Where(s => s.Notes != null)
            .Select(s => s.Notes!)
            .ToListAsync();

        var existingMarkerSet = existingMarkers
            .Select(ExtractJobOrderId)
            .Where(idValue => idValue.HasValue)
            .Select(idValue => idValue!.Value)
            .ToHashSet();

        var vehicles = await _context.Vehicles
            .AsNoTracking()
            .Where(v => completedJobs.Select(j => j.VehicleId).Contains(v.VehicleId))
            .ToDictionaryAsync(v => v.VehicleId);

        foreach (var job in completedJobs)
        {
            if (existingMarkerSet.Contains(job.JobOrderId) || !vehicles.TryGetValue(job.VehicleId, out var vehicle))
            {
                continue;
            }

            var marker = $"Generated from Job Order #{job.JobOrderId}.";
            var notes = string.IsNullOrWhiteSpace(job.Notes)
                ? marker
                : $"{marker} {job.Notes.Trim()}";

            _context.ServiceRecords.Add(new ServiceRecord
            {
                VehicleId = job.VehicleId,
                ServiceDate = DateTime.SpecifyKind(job.JobOrderDate, DateTimeKind.Unspecified),
                Mileage = vehicle.Mileage,
                Complaint = job.Description,
                Diagnosis = job.Diagnosis,
                Status = "Completed",
                Notes = notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync();
        }
    }

    private static int? ExtractJobOrderId(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var match = JobOrderMarkerRegex.Match(notes);
        return match.Success && int.TryParse(match.Groups[1].Value, out var jobOrderId)
            ? jobOrderId
            : null;
    }
}
