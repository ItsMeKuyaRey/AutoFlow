using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers;

public class JobOrdersController : Controller
{
    private readonly AutoFlowDbContext _context;

    public JobOrdersController(AutoFlowDbContext context)
    {
        _context = context;
    }
    // GET: JobOrders
    public async Task<IActionResult> Index()
    {
        var jobOrders = await _context.JobOrders
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.Appointment)
            .OrderByDescending(j => j.JobOrderDate)
            .ToListAsync();

        return View(jobOrders);
    }

    // GET: JobOrders/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jobOrder = await _context.JobOrders
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.Appointment)
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .FirstOrDefaultAsync(j => j.JobOrderId == id);

        if (jobOrder == null)
        {
            return NotFound();
        }

        return View(jobOrder);
    }

    // GET: JobOrders/AddPart/5
    [HttpGet]
    public async Task<IActionResult> AddPart(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jobOrder = await _context.JobOrders.FindAsync(id);

        if (jobOrder == null)
        {
            return NotFound();
        }

        var parts = await _context.Parts
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.PartId,
                DisplayName = p.Name + " (" + p.StockQuantity +
                              " in stock — $" + p.UnitPrice + ")"
            })
            .ToListAsync();

        ViewData["PartId"] = new SelectList(parts, "PartId", "DisplayName");
        ViewBag.JobOrderId = jobOrder.JobOrderId;

        return View();
    }

    // POST: JobOrders/AddPart/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPart(int id, int partId, int quantity)
    {
        if (quantity < 1)
        {
            ModelState.AddModelError(
                string.Empty,
                "Quantity must be at least 1.");
        }

        var jobOrder = await _context.JobOrders.FindAsync(id);
        var part = await _context.Parts.FindAsync(partId);

        if (jobOrder == null || part == null)
        {
            return NotFound();
        }

        if (part.StockQuantity < quantity)
        {
            ModelState.AddModelError(
                string.Empty,
                $"Not enough stock. Only {part.StockQuantity} available.");
        }

        if (!ModelState.IsValid)
        {
            var parts = await _context.Parts
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.PartId,
                    DisplayName = p.Name + " (" + p.StockQuantity +
                                  " in stock — $" + p.UnitPrice + ")"
                })
                .ToListAsync();

            ViewData["PartId"] =
                new SelectList(parts, "PartId", "DisplayName", partId);

            ViewBag.JobOrderId = jobOrder.JobOrderId;

            return View();
        }

        using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.JobOrderParts.Add(new JobOrderPart
            {
                JobOrderId = jobOrder.JobOrderId,
                PartId = part.PartId,
                Quantity = quantity,
                UnitPriceAtTimeOfUse = part.UnitPrice
            });

            part.StockQuantity -= quantity;
            part.UpdatedAt = DateTime.UtcNow;

            jobOrder.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToAction(
            nameof(Details),
            new { id = jobOrder.JobOrderId });
    }

    // POST: JobOrders/RemovePart/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePart(
        int jobOrderPartId,
        int jobOrderId)
    {
        var jobOrderPart = await _context.JobOrderParts
            .Include(jp => jp.Part)
            .FirstOrDefaultAsync(
                jp => jp.JobOrderPartId == jobOrderPartId);

        if (jobOrderPart == null)
        {
            return NotFound();
        }

        using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            if (jobOrderPart.Part != null)
            {
                jobOrderPart.Part.StockQuantity +=
                    jobOrderPart.Quantity;

                jobOrderPart.Part.UpdatedAt = DateTime.UtcNow;
            }

            _context.JobOrderParts.Remove(jobOrderPart);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToAction(
            nameof(Details),
            new { id = jobOrderId });
    }

    // GET: JobOrders/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDropdowns();

        return View();
    }

    // POST: JobOrders/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("VehicleId,AppointmentId,JobOrderDate,Status,LaborCost,Description,Diagnosis,Notes")]
        JobOrder jobOrder)
    {
        if (ModelState.IsValid)
        {
            jobOrder.JobOrderDate =
                DateTime.SpecifyKind(
                    jobOrder.JobOrderDate,
                    DateTimeKind.Utc);

            jobOrder.CreatedAt = DateTime.UtcNow;
            jobOrder.UpdatedAt = DateTime.UtcNow;

            _context.JobOrders.Add(jobOrder);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        await LoadDropdowns(
            jobOrder.VehicleId,
            jobOrder.AppointmentId);

        return View(jobOrder);
    }

    // GET: JobOrders/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jobOrder = await _context.JobOrders.FindAsync(id);

        if (jobOrder == null)
        {
            return NotFound();
        }

        await LoadDropdowns(
            jobOrder.VehicleId,
            jobOrder.AppointmentId);

        return View(jobOrder);
    }

    // POST: JobOrders/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("JobOrderId,VehicleId,AppointmentId,JobOrderDate,Status,LaborCost,Description,Diagnosis,Notes")]
        JobOrder jobOrder)
    {
        if (id != jobOrder.JobOrderId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                var existingJobOrder =
                    await _context.JobOrders.FindAsync(id);

                if (existingJobOrder == null)
                {
                    return NotFound();
                }

                var previousStatus = existingJobOrder.Status;

                existingJobOrder.VehicleId = jobOrder.VehicleId;
                existingJobOrder.AppointmentId = jobOrder.AppointmentId;

                existingJobOrder.JobOrderDate =
                    DateTime.SpecifyKind(
                        jobOrder.JobOrderDate,
                        DateTimeKind.Utc);

                existingJobOrder.Status = jobOrder.Status;
                existingJobOrder.LaborCost = jobOrder.LaborCost;
                existingJobOrder.Description = jobOrder.Description;
                existingJobOrder.Diagnosis = jobOrder.Diagnosis;
                existingJobOrder.Notes = jobOrder.Notes;
                existingJobOrder.UpdatedAt = DateTime.UtcNow;

                bool justCompleted =
                    previousStatus != "Completed" &&
                    existingJobOrder.Status == "Completed";

                if (justCompleted)
                {
                    var existingBilling =
                        await _context.Billings
                            .AsNoTracking()
                            .FirstOrDefaultAsync(
                                b => b.JobOrderId == existingJobOrder.JobOrderId);

                    if (existingBilling == null)
                    {
                        var partsCost =
                            await _context.JobOrderParts
                                .Where(jp =>
                                    jp.JobOrderId ==
                                    existingJobOrder.JobOrderId)
                                .SumAsync(jp =>
                                    jp.Quantity *
                                    jp.UnitPriceAtTimeOfUse);

                        var invoiceNumber =
                            await GenerateInvoiceNumberAsync();

                        var billing = new Billing
                        {
                            JobOrderId = existingJobOrder.JobOrderId,
                            InvoiceNumber = invoiceNumber,
                            LaborCost = existingJobOrder.LaborCost,
                            PartsCost = partsCost,
                            DiscountAmount = 0,
                            TaxAmount = 0,
                            TotalAmount =
                                existingJobOrder.LaborCost +
                                partsCost,
                            AmountPaid = 0,
                            Status = "Unpaid",
                            IssuedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Billings.Add(billing);
                    }

                    var serviceRecordMarker =
                        $"Generated from Job Order #{existingJobOrder.JobOrderId}.";

                    var serviceRecordExists =
                        await _context.ServiceRecords
                            .AnyAsync(sr =>
                                sr.Notes != null &&
                                sr.Notes.StartsWith(serviceRecordMarker));

                    if (!serviceRecordExists)
                    {
                        var vehicle =
                            await _context.Vehicles
                                .AsNoTracking()
                                .FirstOrDefaultAsync(
                                    v => v.VehicleId == existingJobOrder.VehicleId);

                        if (vehicle == null)
                        {
                            return NotFound();
                        }

                        var serviceNotes =
                            string.IsNullOrWhiteSpace(existingJobOrder.Notes)
                                ? serviceRecordMarker
                                : $"{serviceRecordMarker} {existingJobOrder.Notes.Trim()}";

                        var serviceRecord = new ServiceRecord
                        {
                            VehicleId = existingJobOrder.VehicleId,
                            ServiceDate = DateTime.SpecifyKind(
                                existingJobOrder.JobOrderDate,
                                DateTimeKind.Unspecified),
                            Mileage = vehicle.Mileage,
                            Complaint = existingJobOrder.Description,
                            Diagnosis = existingJobOrder.Diagnosis,
                            Status = "Completed",
                            Notes = serviceNotes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.ServiceRecords.Add(serviceRecord);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JobOrderExists(jobOrder.JobOrderId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        await LoadDropdowns(
            jobOrder.VehicleId,
            jobOrder.AppointmentId);

        return View(jobOrder);
    }

    // GET: JobOrders/Delete/5
    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jobOrder = await _context.JobOrders
            .Include(j => j.Vehicle)
            .Include(j => j.Appointment)
            .FirstOrDefaultAsync(
                j => j.JobOrderId == id);

        if (jobOrder == null)
        {
            return NotFound();
        }

        return View(jobOrder);
    }

    // POST: JobOrders/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var jobOrder = await _context.JobOrders.FindAsync(id);

        if (jobOrder != null)
        {
            _context.JobOrders.Remove(jobOrder);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadDropdowns(
        int? selectedVehicleId = null,
        int? selectedAppointmentId = null)
    {
        var vehicles = await _context.Vehicles
            .Where(v => !v.IsArchived)
            .OrderBy(v => v.PlateNumber)
            .Select(v => new
            {
                v.VehicleId,
                DisplayName =
                    v.PlateNumber + " — " +
                    v.Make + " " +
                    v.Model
            })
            .ToListAsync();

        ViewData["VehicleId"] = new SelectList(
            vehicles,
            "VehicleId",
            "DisplayName",
            selectedVehicleId);

        var appointments = await _context.Appointments
            .Include(a => a.Vehicle)
            .OrderByDescending(a => a.AppointmentDate)
            .Select(a => new
            {
                a.AppointmentId,
                DisplayName =
                    a.AppointmentDate.ToString("yyyy-MM-dd HH:mm")
                    + " — "
                    + a.Vehicle!.PlateNumber
                    + " — "
                    + a.Vehicle.Make
                    + " "
                    + a.Vehicle.Model
            })
            .ToListAsync();

        ViewData["AppointmentId"] = new SelectList(
            appointments,
            "AppointmentId",
            "DisplayName",
            selectedAppointmentId);
    }

    private bool JobOrderExists(int id)
    {
        return _context.JobOrders
            .Any(j => j.JobOrderId == id);
    }

    private async Task<string> GenerateInvoiceNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";

        var lastInvoice =
            await _context.Billings
                .Where(b =>
                    b.InvoiceNumber.StartsWith(prefix))
                .OrderByDescending(b => b.InvoiceNumber)
                .Select(b => b.InvoiceNumber)
                .FirstOrDefaultAsync();

        int nextNumber = 1;

        if (lastInvoice != null)
        {
            var numericPart =
                lastInvoice.Substring(prefix.Length);

            if (int.TryParse(
                numericPart,
                out var parsed))
            {
                nextNumber = parsed + 1;
            }
        }

        return $"{prefix}{nextNumber:D4}";
    }
}