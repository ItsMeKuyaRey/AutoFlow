using AutoFlow.Web.Data;
using AutoFlow.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Web.Controllers;

public class CustomerFollowUpsController : Controller
{
    private readonly AutoFlowDbContext _context;

    public CustomerFollowUpsController(AutoFlowDbContext context)
    {
        _context = context;
    }

    // GET: CustomerFollowUps
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? followUpType)
    {
        var query = _context.CustomerFollowUps
            .AsNoTracking()
            .Include(f => f.Customer)
            .Include(f => f.Vehicle)
            .Include(f => f.ServiceRecord)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(f =>
                f.Subject.Contains(searchTerm) ||
                (f.Notes != null &&
                 f.Notes.Contains(searchTerm)) ||
                (f.Customer != null &&
                 (
                     f.Customer.FirstName.Contains(searchTerm) ||
                     f.Customer.LastName.Contains(searchTerm) ||
                     (f.Customer.Email != null &&
                      f.Customer.Email.Contains(searchTerm)) ||
                     (f.Customer.Phone != null &&
                      f.Customer.Phone.Contains(searchTerm))
                 )) ||
                (f.Vehicle != null &&
                 (
                     f.Vehicle.PlateNumber.Contains(searchTerm) ||
                     f.Vehicle.Make.Contains(searchTerm) ||
                     f.Vehicle.Model.Contains(searchTerm)
                 )));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(
                f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(followUpType))
        {
            query = query.Where(
                f => f.FollowUpType == followUpType);
        }

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.FollowUpType = followUpType;

        ViewBag.Statuses = new SelectList(
            new[]
            {
                "Pending",
                "Contacted",
                "Completed",
                "Cancelled"
            },
            status);

        ViewBag.FollowUpTypes = new SelectList(
            new[]
            {
                "Service Follow-up",
                "Maintenance Reminder",
                "Customer Satisfaction",
                "Repair Update",
                "Payment Reminder",
                "General Follow-up"
            },
            followUpType);

        var followUps = await query
            .OrderBy(f => f.Status == "Pending" ? 0 : 1)
            .ThenBy(f => f.FollowUpDate)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync();

        return View(followUps);
    }

    // GET: CustomerFollowUps/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var followUp = await _context.CustomerFollowUps
            .AsNoTracking()
            .Include(f => f.Customer)
            .Include(f => f.Vehicle)
            .Include(f => f.ServiceRecord)
            .FirstOrDefaultAsync(
                f => f.FollowUpId == id);

        if (followUp == null)
        {
            return NotFound();
        }

        return View(followUp);
    }

    // GET: CustomerFollowUps/Create
    public async Task<IActionResult> Create()
    {
        var followUp = new CustomerFollowUp
        {
            FollowUpDate = GetDatabaseDateTime(),
            Status = "Pending",
            FollowUpType = "Service Follow-up"
        };

        await LoadFormDataAsync(
            selectedStatus: followUp.Status,
            selectedFollowUpType: followUp.FollowUpType);

        return View(followUp);
    }

    // POST: CustomerFollowUps/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(
            "CustomerId," +
            "VehicleId," +
            "ServiceRecordId," +
            "FollowUpDate," +
            "FollowUpType," +
            "Subject," +
            "Notes," +
            "Status," +
            "NextFollowUpDate")]
        CustomerFollowUp followUp)
    {
        if (!await CustomerExistsAsync(
                followUp.CustomerId))
        {
            ModelState.AddModelError(
                "CustomerId",
                "The selected customer does not exist.");
        }

        if (!await VehicleBelongsToCustomerAsync(
                followUp.VehicleId,
                followUp.CustomerId))
        {
            ModelState.AddModelError(
                "VehicleId",
                "The selected vehicle does not belong to the selected customer.");
        }

        if (followUp.ServiceRecordId.HasValue &&
            !await ServiceRecordBelongsToVehicleAsync(
                followUp.ServiceRecordId.Value,
                followUp.VehicleId))
        {
            ModelState.AddModelError(
                "ServiceRecordId",
                "The selected service record does not belong to the selected vehicle.");
        }

        if (string.IsNullOrWhiteSpace(followUp.Subject))
        {
            ModelState.AddModelError(
                "Subject",
                "Subject is required.");
        }

        if (string.IsNullOrWhiteSpace(followUp.FollowUpType))
        {
            ModelState.AddModelError(
                "FollowUpType",
                "Follow-up type is required.");
        }

        if (string.IsNullOrWhiteSpace(followUp.Status))
        {
            ModelState.AddModelError(
                "Status",
                "Status is required.");
        }

        if (followUp.NextFollowUpDate.HasValue &&
            followUp.NextFollowUpDate.Value < followUp.FollowUpDate)
        {
            ModelState.AddModelError(
                "NextFollowUpDate",
                "Next follow-up must be on or after the follow-up date.");
        }

        if (ModelState.IsValid)
        {
            followUp.FollowUpDate =
                NormalizeDatabaseDateTime(
                    followUp.FollowUpDate);

            followUp.NextFollowUpDate =
                NormalizeNullableDatabaseDateTime(
                    followUp.NextFollowUpDate);

            followUp.CreatedAt =
                GetDatabaseDateTime();

            followUp.UpdatedAt =
                GetDatabaseDateTime();

            _context.CustomerFollowUps.Add(
                followUp);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Customer follow-up created successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        await LoadFormDataAsync(
            followUp.CustomerId,
            followUp.VehicleId,
            followUp.ServiceRecordId,
            followUp.Status,
            followUp.FollowUpType);

        return View(followUp);
    }

    // GET: CustomerFollowUps/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var followUp =
            await _context.CustomerFollowUps
                .FirstOrDefaultAsync(
                    f => f.FollowUpId == id);

        if (followUp == null)
        {
            return NotFound();
        }

        await LoadFormDataAsync(
            followUp.CustomerId,
            followUp.VehicleId,
            followUp.ServiceRecordId,
            followUp.Status,
            followUp.FollowUpType);

        return View(followUp);
    }

    // POST: CustomerFollowUps/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind(
            "FollowUpId," +
            "CustomerId," +
            "VehicleId," +
            "ServiceRecordId," +
            "FollowUpDate," +
            "FollowUpType," +
            "Subject," +
            "Notes," +
            "Status," +
            "NextFollowUpDate," +
            "CreatedAt")]
        CustomerFollowUp followUp)
    {
        if (id != followUp.FollowUpId)
        {
            return NotFound();
        }

        if (!await CustomerExistsAsync(
                followUp.CustomerId))
        {
            ModelState.AddModelError(
                "CustomerId",
                "The selected customer does not exist.");
        }

        if (!await VehicleBelongsToCustomerAsync(
                followUp.VehicleId,
                followUp.CustomerId))
        {
            ModelState.AddModelError(
                "VehicleId",
                "The selected vehicle does not belong to the selected customer.");
        }

        if (followUp.ServiceRecordId.HasValue &&
            !await ServiceRecordBelongsToVehicleAsync(
                followUp.ServiceRecordId.Value,
                followUp.VehicleId))
        {
            ModelState.AddModelError(
                "ServiceRecordId",
                "The selected service record does not belong to the selected vehicle.");
        }

        if (string.IsNullOrWhiteSpace(followUp.Subject))
        {
            ModelState.AddModelError(
                "Subject",
                "Subject is required.");
        }

        if (string.IsNullOrWhiteSpace(followUp.FollowUpType))
        {
            ModelState.AddModelError(
                "FollowUpType",
                "Follow-up type is required.");
        }

        if (string.IsNullOrWhiteSpace(followUp.Status))
        {
            ModelState.AddModelError(
                "Status",
                "Status is required.");
        }

        if (followUp.NextFollowUpDate.HasValue &&
            followUp.NextFollowUpDate.Value < followUp.FollowUpDate)
        {
            ModelState.AddModelError(
                "NextFollowUpDate",
                "Next follow-up must be on or after the follow-up date.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var existing =
                    await _context.CustomerFollowUps
                        .FirstOrDefaultAsync(
                            f => f.FollowUpId == id);

                if (existing == null)
                {
                    return NotFound();
                }

                existing.CustomerId =
                    followUp.CustomerId;

                existing.VehicleId =
                    followUp.VehicleId;

                existing.ServiceRecordId =
                    followUp.ServiceRecordId;

                existing.FollowUpDate =
                    NormalizeDatabaseDateTime(
                        followUp.FollowUpDate);

                existing.FollowUpType =
                    followUp.FollowUpType.Trim();

                existing.Subject =
                    followUp.Subject.Trim();

                existing.Notes =
                    string.IsNullOrWhiteSpace(
                        followUp.Notes)
                        ? null
                        : followUp.Notes.Trim();

                existing.Status =
                    followUp.Status.Trim();

                existing.NextFollowUpDate =
                    NormalizeNullableDatabaseDateTime(
                        followUp.NextFollowUpDate);

                existing.UpdatedAt =
                    GetDatabaseDateTime();

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Customer follow-up updated successfully.";

                return RedirectToAction(
                    nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await CustomerFollowUpExistsAsync(
                        followUp.FollowUpId))
                {
                    return NotFound();
                }

                throw;
            }
        }

        await LoadFormDataAsync(
            followUp.CustomerId,
            followUp.VehicleId,
            followUp.ServiceRecordId,
            followUp.Status,
            followUp.FollowUpType);

        return View(followUp);
    }

    // GET: CustomerFollowUps/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var followUp =
            await _context.CustomerFollowUps
                .AsNoTracking()
                .Include(f => f.Customer)
                .Include(f => f.Vehicle)
                .Include(f => f.ServiceRecord)
                .FirstOrDefaultAsync(
                    f => f.FollowUpId == id);

        if (followUp == null)
        {
            return NotFound();
        }

        return View(followUp);
    }

    // POST: CustomerFollowUps/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int id)
    {
        var followUp =
            await _context.CustomerFollowUps
                .FindAsync(id);

        if (followUp == null)
        {
            return NotFound();
        }

        _context.CustomerFollowUps.Remove(
            followUp);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Customer follow-up deleted successfully.";

        return RedirectToAction(
            nameof(Index));
    }

    // Load dropdown data for Create and Edit
    private async Task LoadFormDataAsync(
        int? selectedCustomerId = null,
        int? selectedVehicleId = null,
        int? selectedServiceRecordId = null,
        string? selectedStatus = null,
        string? selectedFollowUpType = null)
    {
        var customers =
            await _context.Customers
                .AsNoTracking()
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToListAsync();

        var customerOptions =
            customers.Select(c => new
            {
                c.CustomerId,
                DisplayName =
                    c.LastName +
                    ", " +
                    c.FirstName
            })
            .ToList();

        ViewBag.CustomerId =
            new SelectList(
                customerOptions,
                "CustomerId",
                "DisplayName",
                selectedCustomerId);

        var vehiclesQuery =
            _context.Vehicles
                .AsNoTracking()
                .Include(v => v.Customer)
                .Where(v => !v.IsArchived);

        if (selectedCustomerId.HasValue)
        {
            vehiclesQuery =
                vehiclesQuery.Where(
                    v => v.CustomerId ==
                         selectedCustomerId.Value);
        }

        var vehicles =
            await vehiclesQuery
                .OrderBy(v => v.Make)
                .ThenBy(v => v.Model)
                .ThenBy(v => v.PlateNumber)
                .ToListAsync();

        var vehicleOptions =
            vehicles.Select(v => new
            {
                v.VehicleId,
                DisplayName =
                    v.Make +
                    " " +
                    v.Model +
                    " · " +
                    v.PlateNumber
            })
            .ToList();

        ViewBag.VehicleId =
            new SelectList(
                vehicleOptions,
                "VehicleId",
                "DisplayName",
                selectedVehicleId);

        var serviceRecordsQuery =
            _context.ServiceRecords
                .AsNoTracking()
                .Include(sr => sr.Vehicle)
                .AsQueryable();

        if (selectedVehicleId.HasValue)
        {
            serviceRecordsQuery =
                serviceRecordsQuery.Where(
                    sr => sr.VehicleId ==
                          selectedVehicleId.Value);
        }

        var serviceRecords =
            await serviceRecordsQuery
                .OrderByDescending(
                    sr => sr.ServiceDate)
                .ToListAsync();

        var serviceRecordOptions =
            serviceRecords.Select(sr => new
            {
                sr.ServiceRecordId,
                DisplayName =
                    sr.ServiceDate.ToString(
                        "MMM dd, yyyy") +
                    " · " +
                    (
                        string.IsNullOrWhiteSpace(
                            sr.Complaint)
                            ? "Service record"
                            : sr.Complaint
                    )
            })
            .ToList();

        ViewBag.ServiceRecordId =
            new SelectList(
                serviceRecordOptions,
                "ServiceRecordId",
                "DisplayName",
                selectedServiceRecordId);

        ViewBag.Statuses =
            new SelectList(
                new[]
                {
                    "Pending",
                    "Contacted",
                    "Completed",
                    "Cancelled"
                },
                selectedStatus);

        ViewBag.FollowUpTypes =
            new SelectList(
                new[]
                {
                    "Service Follow-up",
                    "Maintenance Reminder",
                    "Customer Satisfaction",
                    "Repair Update",
                    "Payment Reminder",
                    "General Follow-up"
                },
                selectedFollowUpType);
    }

    // Check whether the selected customer exists
    private async Task<bool> CustomerExistsAsync(
        int customerId)
    {
        return await _context.Customers
            .AsNoTracking()
            .AnyAsync(
                c => c.CustomerId == customerId);
    }

    // Check whether the follow-up record exists
    private async Task<bool> CustomerFollowUpExistsAsync(
        int id)
    {
        return await _context.CustomerFollowUps
            .AsNoTracking()
            .AnyAsync(
                f => f.FollowUpId == id);
    }

    // Make sure the selected vehicle belongs to the selected customer
    private async Task<bool> VehicleBelongsToCustomerAsync(
        int vehicleId,
        int customerId)
    {
        return await _context.Vehicles
            .AsNoTracking()
            .AnyAsync(
                v =>
                    v.VehicleId == vehicleId &&
                    v.CustomerId == customerId &&
                    !v.IsArchived);
    }

    // Make sure the selected service record belongs to the selected vehicle
    private async Task<bool> ServiceRecordBelongsToVehicleAsync(
        int serviceRecordId,
        int vehicleId)
    {
        return await _context.ServiceRecords
            .AsNoTracking()
            .AnyAsync(
                sr =>
                    sr.ServiceRecordId ==
                    serviceRecordId &&
                    sr.VehicleId ==
                    vehicleId);
    }

    // PostgreSQL timestamp without time zone helper
    private static DateTime GetDatabaseDateTime()
    {
        var now = DateTime.Now;

        return new DateTime(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            now.Minute,
            0,
            DateTimeKind.Unspecified);
    }

    // Normalize form DateTime values for PostgreSQL
    private static DateTime NormalizeDatabaseDateTime(
        DateTime value)
    {
        return new DateTime(
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute,
            0,
            DateTimeKind.Unspecified);
    }

    // Normalize nullable form DateTime values for PostgreSQL
    private static DateTime? NormalizeNullableDatabaseDateTime(
        DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return NormalizeDatabaseDateTime(
            value.Value);
    }
}