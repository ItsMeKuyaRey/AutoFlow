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
        string? followUpType,
        int page = 1,
        string? open = null,
        int? id = null)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        // Keep archived follow-ups in the main CRM list. Archiving changes the row
        // status instead of removing the record from the main page.
        var query = _context.CustomerFollowUps
            .AsNoTracking()
            .Include(f => f.Customer)
            .Include(f => f.Vehicle)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(f =>
                f.Subject.Contains(searchTerm) ||
                (f.Notes != null && f.Notes.Contains(searchTerm)) ||
                (f.Customer != null &&
                 (f.Customer.FirstName.Contains(searchTerm) ||
                  f.Customer.LastName.Contains(searchTerm) ||
                  (f.Customer.Email != null && f.Customer.Email.Contains(searchTerm)) ||
                  (f.Customer.Phone != null && f.Customer.Phone.Contains(searchTerm)))) ||
                (f.Vehicle != null &&
                 (f.Vehicle.PlateNumber.Contains(searchTerm) ||
                  f.Vehicle.Make.Contains(searchTerm) ||
                  f.Vehicle.Model.Contains(searchTerm))));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(followUpType))
        {
            query = query.Where(f => f.FollowUpType == followUpType);
        }

        var allFilteredFollowUps = await query
            .OrderBy(f => f.FollowUpDate)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync();

        var archivedFollowUps = await _context.CustomerFollowUps
            .AsNoTracking()
            .Include(f => f.Customer)
            .Include(f => f.Vehicle)
            .Where(f => f.Status == "Archived")
            .OrderByDescending(f => f.UpdatedAt)
            .ThenByDescending(f => f.FollowUpDate)
            .ToListAsync();

        ViewBag.ArchivedCount = archivedFollowUps.Count;
        ViewBag.ArchivedFollowUps = archivedFollowUps;

        var now = GetDatabaseDateTime();
        var today = now.Date;
        var activeStatuses = new[] { "Pending", "Contacted" };

        ViewBag.DueTodayCount = allFilteredFollowUps.Count(f =>
            f.FollowUpDate.Date == today &&
            activeStatuses.Contains(f.Status, StringComparer.OrdinalIgnoreCase));

        ViewBag.UpcomingCount = allFilteredFollowUps.Count(f =>
            f.NextFollowUpDate.HasValue &&
            f.NextFollowUpDate.Value.Date > today &&
            activeStatuses.Contains(f.Status, StringComparer.OrdinalIgnoreCase));

        ViewBag.OverdueCount = allFilteredFollowUps.Count(f =>
            f.FollowUpDate.Date < today &&
            activeStatuses.Contains(f.Status, StringComparer.OrdinalIgnoreCase));

        ViewBag.TotalCount = allFilteredFollowUps.Count;
        ViewBag.PageSize = pageSize;
        ViewBag.Page = page;
        ViewBag.PageCount = Math.Max(1, (int)Math.Ceiling(allFilteredFollowUps.Count / (double)pageSize));
        ViewBag.Search = search ?? string.Empty;
        ViewBag.Status = status ?? string.Empty;
        ViewBag.FollowUpType = followUpType ?? string.Empty;
        ViewBag.Open = open ?? string.Empty;
        ViewBag.OpenId = id;

        ViewBag.Statuses = new SelectList(
            new[] { "Pending", "Contacted", "Completed", "Cancelled" },
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

        var pageCount = (int)ViewBag.PageCount;
        if (page > pageCount)
        {
            page = pageCount;
            ViewBag.Page = page;
        }

        var followUps = allFilteredFollowUps
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        CustomerFollowUp? drawerFollowUp = null;
        if (id.HasValue &&
            (string.Equals(open, "details", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(open, "edit", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(open, "delete", StringComparison.OrdinalIgnoreCase)))
        {
            drawerFollowUp = await _context.CustomerFollowUps
                .AsNoTracking()
                .Include(f => f.Customer)
                .Include(f => f.Vehicle)
                                .FirstOrDefaultAsync(f => f.FollowUpId == id.Value);
        }

        if (string.Equals(open, "create", StringComparison.OrdinalIgnoreCase))
        {
            drawerFollowUp = new CustomerFollowUp
            {
                FollowUpDate = GetDatabaseDateTime(),
                Status = "Pending",
                FollowUpType = "Service Follow-up"
            };
        }

        ViewBag.DrawerFollowUp = drawerFollowUp;

        if (drawerFollowUp != null &&
            (string.Equals(open, "create", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(open, "edit", StringComparison.OrdinalIgnoreCase)))
        {
            await LoadFormDataAsync(
                drawerFollowUp.CustomerId == 0 ? null : drawerFollowUp.CustomerId,
                drawerFollowUp.VehicleId == 0 ? null : drawerFollowUp.VehicleId,
                drawerFollowUp.Status,
                drawerFollowUp.FollowUpType);
        }

        return View(followUps);
    }

    // Lightweight JSON endpoints used by the CRM drawer to keep vehicle and service selections connected.
    [HttpGet]
    public async Task<IActionResult> VehiclesForCustomer(int customerId)
    {
        var vehicles = await _context.Vehicles
            .AsNoTracking()
            .Where(v => v.CustomerId == customerId)
            .OrderBy(v => v.Make)
            .ThenBy(v => v.Model)
            .ThenBy(v => v.PlateNumber)
            .Select(v => new
            {
                id = v.VehicleId,
                text = v.Make + " " + v.Model + " · " + v.PlateNumber,
                imageUrl = v.ImageUrl,
                make = v.Make,
                model = v.Model,
                plateNumber = v.PlateNumber,
                isArchived = v.IsArchived
            })
            .ToListAsync();

        return Json(vehicles);
    }

    [HttpGet]
    public async Task<IActionResult> CustomerProfile(int customerId)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new
            {
                c.CustomerId,
                name = (c.FirstName + " " + c.LastName).Trim(),
                c.Phone,
                c.Email,
                imageUrl = c.ImageUrl
            })
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound();
        }

        return Json(customer);
    }

    // GET: CustomerFollowUps/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null || !await CustomerFollowUpExistsAsync(id.Value))
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { open = "details", id });
    }

    // GET: CustomerFollowUps/Create
    public IActionResult Create()
    {
        return RedirectToAction(nameof(Index), new { open = "create" });
    }

    // POST: CustomerFollowUps/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(
            "CustomerId," +
            "VehicleId," +
            "FollowUpDate," +
            "FollowUpType," +
            "Subject," +
            "Notes," +
            "Status," +
            "NextFollowUpDate")]
        CustomerFollowUp followUp)
    {
        if (string.IsNullOrWhiteSpace(followUp.FollowUpType))
        {
            followUp.FollowUpType = "Service Follow-up";
            ModelState.Remove(nameof(followUp.FollowUpType));
        }

        if (string.IsNullOrWhiteSpace(followUp.Status))
        {
            followUp.Status = "Pending";
            ModelState.Remove(nameof(followUp.Status));
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

        TempData["ErrorMessage"] =
            ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? "Please check the follow-up details and try again.";

        return RedirectToAction(
            nameof(Index),
            new { open = "create" });
    }

    // GET: CustomerFollowUps/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null || !await CustomerFollowUpExistsAsync(id.Value))
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { open = "edit", id });
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
            followUp.Status,
            followUp.FollowUpType);

        return View(followUp);
    }

    // GET: CustomerFollowUps/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null || !await CustomerFollowUpExistsAsync(id.Value))
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { open = "delete", id });
    }

    // POST: CustomerFollowUps/Delete/5
    // Kept as the existing route so the current archive confirmation drawer continues to work.
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var followUp = await _context.CustomerFollowUps.FindAsync(id);
        if (followUp == null)
        {
            return NotFound();
        }

        followUp.Status = "Archived";
        followUp.UpdatedAt = GetDatabaseDateTime();

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Customer follow-up archived successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: CustomerFollowUps/Restore/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var followUp = await _context.CustomerFollowUps.FindAsync(id);
        if (followUp == null)
        {
            return NotFound();
        }

        if (string.Equals(followUp.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            followUp.Status = "Pending";
            followUp.UpdatedAt = GetDatabaseDateTime();
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Customer follow-up restored successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Load dropdown data for Create and Edit
    private async Task LoadFormDataAsync(
        int? selectedCustomerId = null,
        int? selectedVehicleId = null,
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

        IQueryable<Vehicle> vehiclesQuery =
            _context.Vehicles
                .AsNoTracking()
                .Include(v => v.Customer);

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
                    v.PlateNumber +
                    (v.IsArchived ? " · Archived" : ""),
                v.IsArchived
            })
            .ToList();

        ViewBag.VehicleId =
            new SelectList(
                vehicleOptions,
                "VehicleId",
                "DisplayName",
                selectedVehicleId);

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
                    v.CustomerId == customerId);
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