using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers;

public class JobOrdersController : Controller
{
    private const decimal BillingTaxRate = 0.12m;
    private readonly AutoFlowDbContext _context;

    public JobOrdersController(AutoFlowDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? open = null, int? id = null)
    {
        var jobOrders = await _context.JobOrders
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.Appointment)
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .OrderByDescending(j => j.JobOrderDate)
            .ToListAsync();

        await LoadDropdowns();

        ViewData["ArchivedJobOrderCount"] = await _context.JobOrders
            .AsNoTracking()
            .CountAsync(j => j.IsArchived);

        ViewData["BillingIdsByJobOrder"] = await _context.Billings
            .AsNoTracking()
            .ToDictionaryAsync(b => b.JobOrderId, b => b.BillingId);

        ViewData["BillingStatusesByJobOrder"] = await _context.Billings
            .AsNoTracking()
            .ToDictionaryAsync(b => b.JobOrderId, b => b.Status);

        ViewData["Open"] = open ?? string.Empty;
        ViewData["OpenId"] = id?.ToString() ?? string.Empty;

        return View(jobOrders);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var exists = await _context.JobOrders
            .AnyAsync(j => j.JobOrderId == id);

        if (!exists)
        {
            return NotFound();
        }

        return RedirectToAction(
            nameof(Index),
            new { open = "details", id });
    }

    [HttpGet]
    public async Task<IActionResult> AddPart(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jobOrder = await _context.JobOrders
            .AnyAsync(j => j.JobOrderId == id);

        if (!jobOrder)
        {
            return NotFound();
        }

        return RedirectToAction(
            nameof(Index),
            new { open = "addpart", id });
    }

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

        var jobOrder = await _context.JobOrders
            .FirstOrDefaultAsync(j => j.JobOrderId == id && !j.IsArchived);
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
            await LoadDropdowns();
            ViewData["Open"] = "addpart";
            ViewData["OpenId"] = id.ToString();

            return View("Index", await GetJobOrdersAsync());
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
            nameof(Index),
            new { open = "details", id = jobOrder.JobOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePart(
        int jobOrderPartId,
        int jobOrderId)
    {
        var jobOrderPart = await _context.JobOrderParts
            .Include(jp => jp.Part)
            .Include(jp => jp.JobOrder)
            .FirstOrDefaultAsync(
                jp => jp.JobOrderPartId == jobOrderPartId);

        if (jobOrderPart == null || jobOrderPart.JobOrder == null || jobOrderPart.JobOrder.IsArchived)
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

            var jobOrder = await _context.JobOrders
                .FindAsync(jobOrderId);

            if (jobOrder != null)
            {
                jobOrder.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToAction(
            nameof(Index),
            new { open = "details", id = jobOrderId });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDropdowns();

        return RedirectToAction(
            nameof(Index),
            new { open = "create" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("VehicleId,AppointmentId,JobOrderDate,Status,LaborCost,Description,Diagnosis,Notes")]
        JobOrder jobOrder,
        List<int>? partIds,
        List<int>? partQuantities)
    {
        Appointment? linkedAppointment = null;

        if (jobOrder.AppointmentId.HasValue)
        {
            linkedAppointment = await _context.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.AppointmentId == jobOrder.AppointmentId.Value);

            if (linkedAppointment == null)
            {
                ModelState.AddModelError(
                    nameof(jobOrder.AppointmentId),
                    "The selected appointment could not be found.");
            }
            else if (linkedAppointment.VehicleId != jobOrder.VehicleId)
            {
                ModelState.AddModelError(
                    nameof(jobOrder.AppointmentId),
                    "The selected appointment does not belong to the selected vehicle.");
            }
            else if (!string.Equals(linkedAppointment.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(jobOrder.AppointmentId),
                    "Only scheduled appointments can be linked to a new repair job.");
            }
            else if (await _context.JobOrders.AnyAsync(j =>
                j.AppointmentId == linkedAppointment.AppointmentId &&
                !j.IsArchived))
            {
                ModelState.AddModelError(
                    nameof(jobOrder.AppointmentId),
                    "This appointment is already linked to a repair job.");
            }
            else
            {
                // Appointment-linked jobs always inherit the appointment date/time.
                jobOrder.JobOrderDate =
                    DateTime.SpecifyKind(
                        linkedAppointment.AppointmentDate,
                        DateTimeKind.Utc);
            }
        }
        else
        {
            // No appointment means this is a walk-in/manual repair job.
            jobOrder.JobOrderDate =
                DateTime.SpecifyKind(
                    jobOrder.JobOrderDate,
                    DateTimeKind.Utc);
        }

        var requestedParts = new Dictionary<int, int>();

        if (partIds != null)
        {
            for (var i = 0; i < partIds.Count; i++)
            {
                var partId = partIds[i];
                if (partId <= 0)
                {
                    continue;
                }

                var quantity =
                    partQuantities != null && i < partQuantities.Count
                        ? partQuantities[i]
                        : 1;

                if (quantity < 1)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Part quantities must be at least 1.");
                    break;
                }

                requestedParts[partId] =
                    requestedParts.TryGetValue(partId, out var existingQuantity)
                        ? existingQuantity + quantity
                        : quantity;
            }
        }

        Dictionary<int, Part> selectedParts = new();

        if (requestedParts.Count > 0)
        {
            selectedParts = await _context.Parts
                .Where(p => requestedParts.Keys.Contains(p.PartId))
                .ToDictionaryAsync(p => p.PartId);

            foreach (var requestedPart in requestedParts)
            {
                if (!selectedParts.TryGetValue(requestedPart.Key, out var part) || part.IsArchived)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "One of the selected parts is no longer available in Parts Inventory.");
                    continue;
                }

                if (part.StockQuantity < requestedPart.Value)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"Not enough stock for {part.Name}. Only {part.StockQuantity} unit{(part.StockQuantity == 1 ? "" : "s")} available.");
                }
            }
        }

        if (ModelState.IsValid)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                jobOrder.CreatedAt = DateTime.UtcNow;
                jobOrder.UpdatedAt = DateTime.UtcNow;
                jobOrder.IsArchived = false;

                _context.JobOrders.Add(jobOrder);
                await _context.SaveChangesAsync();

                foreach (var requestedPart in requestedParts)
                {
                    var part = selectedParts[requestedPart.Key];

                    _context.JobOrderParts.Add(new JobOrderPart
                    {
                        JobOrderId = jobOrder.JobOrderId,
                        PartId = part.PartId,
                        Quantity = requestedPart.Value,
                        UnitPriceAtTimeOfUse = part.UnitPrice
                    });

                    part.StockQuantity -= requestedPart.Value;
                    part.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await LoadDropdowns(
            jobOrder.VehicleId,
            jobOrder.AppointmentId);

        ViewData["Open"] = "create";
        ViewData["OpenId"] = string.Empty;

        var currentJobs = await GetJobOrdersAsync();
        return View("Index", currentJobs);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jobOrder = await _context.JobOrders
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .FirstOrDefaultAsync(j => j.JobOrderId == id && !j.IsArchived);

        if (jobOrder == null)
        {
            return NotFound();
        }

        return RedirectToAction(
            nameof(Index),
            new { open = "edit", id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("JobOrderId,VehicleId,AppointmentId,JobOrderDate,Status,LaborCost,Description,Diagnosis,Notes")]
        JobOrder jobOrder,
        List<int>? partIds,
        List<int>? partQuantities,
        string? partsUsedJson)
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
                    await _context.JobOrders
                        .FirstOrDefaultAsync(
                            j => j.JobOrderId == id && !j.IsArchived);

                if (existingJobOrder == null)
                {
                    return NotFound();
                }

                // Completed jobs remain editable. Invoice creation is a separate
                // billing step, so completing a repair does not silently create an invoice.
                var existingBilling = await _context.Billings
                    .FirstOrDefaultAsync(b => b.JobOrderId == existingJobOrder.JobOrderId);

                // Keep the same appointment/date rule when editing a job: a linked
                // scheduled appointment owns the repair date/time; without an
                // appointment the user keeps a manual repair date.
                Appointment? linkedAppointment = null;

                if (jobOrder.AppointmentId.HasValue)
                {
                    linkedAppointment = await _context.Appointments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a =>
                            a.AppointmentId == jobOrder.AppointmentId.Value);

                    if (linkedAppointment == null)
                    {
                        ModelState.AddModelError(
                            nameof(jobOrder.AppointmentId),
                            "The selected appointment could not be found.");
                    }
                    else if (linkedAppointment.VehicleId != jobOrder.VehicleId)
                    {
                        ModelState.AddModelError(
                            nameof(jobOrder.AppointmentId),
                            "The selected appointment does not belong to the selected vehicle.");
                    }
                    else if (!string.Equals(linkedAppointment.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(
                            nameof(jobOrder.AppointmentId),
                            "Only scheduled appointments can be linked to a repair job.");
                    }
                    else if (await _context.JobOrders.AnyAsync(j =>
                        j.AppointmentId == linkedAppointment.AppointmentId &&
                        j.JobOrderId != existingJobOrder.JobOrderId &&
                        !j.IsArchived))
                    {
                        ModelState.AddModelError(
                            nameof(jobOrder.AppointmentId),
                            "This appointment is already linked to another repair job.");
                    }
                }

                var requestedParts = new Dictionary<int, int>();

                // The edit drawer sends a JSON snapshot of the visible Parts Used
                // rows as well as the repeated partIds/partQuantities fields.
                // Prefer the JSON snapshot so the exact list the user sees when
                // clicking Save Changes is persisted, even if a browser/proxy
                // normalizes repeated form fields unexpectedly.
                if (!string.IsNullOrWhiteSpace(partsUsedJson))
                {
                    try
                    {
                        var submittedParts =
                            JsonSerializer.Deserialize<List<EditPartPayload>>(
                                partsUsedJson,
                                new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                }) ?? new List<EditPartPayload>();

                        foreach (var submittedPart in submittedParts)
                        {
                            if (submittedPart.PartId <= 0)
                            {
                                continue;
                            }

                            if (submittedPart.Quantity < 1)
                            {
                                ModelState.AddModelError(
                                    string.Empty,
                                    "Part quantities must be at least 1.");
                                continue;
                            }

                            requestedParts[submittedPart.PartId] =
                                requestedParts.TryGetValue(submittedPart.PartId, out var existingQuantity)
                                    ? existingQuantity + submittedPart.Quantity
                                    : submittedPart.Quantity;
                        }
                    }
                    catch (JsonException)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "The Parts Used selection could not be read. Please add the parts again and save.");
                    }
                }
                else if (partIds != null)
                {
                    // Backwards-compatible fallback for older browsers/forms.
                    for (var i = 0; i < partIds.Count; i++)
                    {
                        var partId = partIds[i];
                        if (partId <= 0)
                        {
                            continue;
                        }

                        var quantity =
                            partQuantities != null && i < partQuantities.Count
                                ? partQuantities[i]
                                : 1;

                        if (quantity < 1)
                        {
                            ModelState.AddModelError(
                                string.Empty,
                                "Part quantities must be at least 1.");
                            break;
                        }

                        requestedParts[partId] =
                            requestedParts.TryGetValue(partId, out var existingQuantity)
                                ? existingQuantity + quantity
                                : quantity;
                    }
                }

                var currentJobParts = await _context.JobOrderParts
                    .Include(jp => jp.Part)
                    .Where(jp => jp.JobOrderId == existingJobOrder.JobOrderId)
                    .ToListAsync();

                var currentQuantities = currentJobParts
                    .GroupBy(jp => jp.PartId)
                    .ToDictionary(g => g.Key, g => g.Sum(jp => jp.Quantity));

                var requestedPartIds = requestedParts.Keys.ToList();
                var inventoryParts = requestedPartIds.Count == 0
                    ? new Dictionary<int, Part>()
                    : await _context.Parts
                        .Where(p => requestedPartIds.Contains(p.PartId))
                        .ToDictionaryAsync(p => p.PartId);

                foreach (var requestedPart in requestedParts)
                {
                    if (!inventoryParts.TryGetValue(requestedPart.Key, out var part))
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "One of the selected parts could not be found in Parts Inventory.");
                        continue;
                    }

                    var originalQuantity =
                        currentQuantities.TryGetValue(requestedPart.Key, out var currentQuantity)
                            ? currentQuantity
                            : 0;

                    // Existing job parts already consumed stock. Add their current
                    // quantity back when validating the new requested quantity so
                    // editing a job does not incorrectly reject the quantity it
                    // already owns.
                    var availableForThisJob =
                        part.StockQuantity + originalQuantity;

                    if (part.IsArchived && originalQuantity == 0)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            $"{part.Name} is archived and cannot be added to this repair job.");
                    }
                    else if (requestedPart.Value > availableForThisJob)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            $"Not enough stock for {part.Name}. Only {availableForThisJob} unit{(availableForThisJob == 1 ? "" : "s")} are available for this job.");
                    }
                }

                // A part that is already attached to the job may be kept even if
                // it was later archived in Parts Inventory. It is still part of
                // the historical repair record; only newly-added archived parts
                // are rejected above.
                foreach (var currentPart in currentJobParts)
                {
                    if (!requestedParts.ContainsKey(currentPart.PartId) &&
                        currentPart.Part?.IsArchived == true)
                    {
                        continue;
                    }
                }

                if (!ModelState.IsValid)
                {
                    await LoadDropdowns(
                        jobOrder.VehicleId,
                        jobOrder.AppointmentId);

                    ViewData["Open"] = "edit";
                    ViewData["OpenId"] = id.ToString();
                    TempData["JobOrderNotification"] =
                        string.Join(" ", ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .Where(m => !string.IsNullOrWhiteSpace(m)));

                    return View("Index", await GetJobOrdersAsync());
                }

                await using var editTransaction =
                    await _context.Database.BeginTransactionAsync();

                existingJobOrder.VehicleId =
                    jobOrder.VehicleId;

                existingJobOrder.AppointmentId =
                    jobOrder.AppointmentId;

                existingJobOrder.JobOrderDate = linkedAppointment != null
                    ? DateTime.SpecifyKind(
                        linkedAppointment.AppointmentDate,
                        DateTimeKind.Utc)
                    : DateTime.SpecifyKind(
                        jobOrder.JobOrderDate,
                        DateTimeKind.Utc);

                existingJobOrder.Status =
                    jobOrder.Status;

                existingJobOrder.LaborCost =
                    jobOrder.LaborCost;

                existingJobOrder.Description =
                    jobOrder.Description;

                existingJobOrder.Diagnosis =
                    jobOrder.Diagnosis;

                existingJobOrder.Notes =
                    jobOrder.Notes;

                existingJobOrder.UpdatedAt =
                    DateTime.UtcNow;

                // Reconcile Parts Used with Parts Inventory. Positive deltas consume
                // stock; negative deltas return stock. Existing unit prices are kept
                // as the historical price used by this repair.
                var allPartIds = currentJobParts
                    .Select(jp => jp.PartId)
                    .Union(requestedParts.Keys)
                    .Distinct()
                    .ToList();

                var allParts = allPartIds.Count == 0
                    ? new Dictionary<int, Part>()
                    : await _context.Parts
                        .Where(p => allPartIds.Contains(p.PartId))
                        .ToDictionaryAsync(p => p.PartId);

                foreach (var currentPart in currentJobParts)
                {
                    var desiredQuantity =
                        requestedParts.TryGetValue(currentPart.PartId, out var requestedQuantity)
                            ? requestedQuantity
                            : 0;

                    var delta = desiredQuantity - currentPart.Quantity;

                    if (allParts.TryGetValue(currentPart.PartId, out var inventoryPart) && delta != 0)
                    {
                        inventoryPart.StockQuantity -= delta;
                        inventoryPart.UpdatedAt = DateTime.UtcNow;
                    }

                    if (desiredQuantity <= 0)
                    {
                        _context.JobOrderParts.Remove(currentPart);
                    }
                    else
                    {
                        currentPart.Quantity = desiredQuantity;
                    }

                    requestedParts.Remove(currentPart.PartId);
                }

                foreach (var requestedPart in requestedParts)
                {
                    if (!allParts.TryGetValue(requestedPart.Key, out var inventoryPart))
                    {
                        continue;
                    }

                    _context.JobOrderParts.Add(new JobOrderPart
                    {
                        JobOrderId = existingJobOrder.JobOrderId,
                        PartId = inventoryPart.PartId,
                        Quantity = requestedPart.Value,
                        UnitPriceAtTimeOfUse = inventoryPart.UnitPrice
                    });

                    inventoryPart.StockQuantity -= requestedPart.Value;
                    inventoryPart.UpdatedAt = DateTime.UtcNow;
                }

                bool isCompleted =
                    existingJobOrder.Status == "Completed";

                if (isCompleted)
                {
                    if (existingBilling != null)
                    {
                        var partsCost =
                            await _context.JobOrderParts
                                .Where(
                                    jp =>
                                        jp.JobOrderId ==
                                        existingJobOrder.JobOrderId)
                                .SumAsync(
                                    jp =>
                                        jp.Quantity *
                                        jp.UnitPriceAtTimeOfUse);

                        var taxAmount = Math.Round(
                            (existingJobOrder.LaborCost + partsCost) * BillingTaxRate,
                            2,
                            MidpointRounding.AwayFromZero);

                        // Keep an existing invoice synchronized when a completed
                        // repair is edited after invoicing.
                        existingBilling.LaborCost = existingJobOrder.LaborCost;
                        existingBilling.PartsCost = partsCost;
                        existingBilling.TaxAmount = taxAmount;
                        existingBilling.TotalAmount =
                            existingJobOrder.LaborCost + partsCost + taxAmount;
                        existingBilling.UpdatedAt = DateTime.UtcNow;
                    }

                    var serviceRecordMarker =
                        $"Generated from Job Order #{existingJobOrder.JobOrderId}.";

                    var serviceRecordExists =
                        await _context.ServiceRecords
                            .AnyAsync(
                                sr =>
                                    sr.Notes != null &&
                                    sr.Notes.StartsWith(
                                        serviceRecordMarker));

                    if (!serviceRecordExists)
                    {
                        var vehicle =
                            await _context.Vehicles
                                .AsNoTracking()
                                .FirstOrDefaultAsync(
                                    v =>
                                        v.VehicleId ==
                                        existingJobOrder.VehicleId);

                        if (vehicle == null)
                        {
                            return NotFound();
                        }

                        var serviceNotes =
                            string.IsNullOrWhiteSpace(
                                existingJobOrder.Notes)
                                ? serviceRecordMarker
                                : $"{serviceRecordMarker} {existingJobOrder.Notes.Trim()}";

                        var serviceRecord =
                            new ServiceRecord
                            {
                                VehicleId =
                                    existingJobOrder.VehicleId,

                                ServiceDate =
                                    DateTime.SpecifyKind(
                                        existingJobOrder.JobOrderDate,
                                        DateTimeKind.Unspecified),

                                Mileage =
                                    vehicle.Mileage,

                                Complaint =
                                    existingJobOrder.Description,

                                Diagnosis =
                                    existingJobOrder.Diagnosis,

                                Status = "Completed",

                                Notes =
                                    serviceNotes,

                                CreatedAt =
                                    DateTime.UtcNow,

                                UpdatedAt =
                                    DateTime.UtcNow
                            };

                        _context.ServiceRecords.Add(
                            serviceRecord);
                    }
                }

                await _context.SaveChangesAsync();
                await editTransaction.CommitAsync();
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

    [HttpGet]
    public async Task<IActionResult> Archive(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var exists =
            await _context.JobOrders
                .AnyAsync(j => j.JobOrderId == id && !j.IsArchived);

        if (!exists)
        {
            return NotFound();
        }

        return RedirectToAction(
            nameof(Index),
            new { open = "archive", id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var jobOrder =
            await _context.JobOrders
                .FirstOrDefaultAsync(j => j.JobOrderId == id && !j.IsArchived);

        if (jobOrder == null)
        {
            return NotFound();
        }

        jobOrder.IsArchived = true;
        jobOrder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["JobOrderNotification"] = $"Job Order #{id} archived successfully.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Archived()
    {
        var jobOrders = await _context.JobOrders
            .Where(j => j.IsArchived)
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.Appointment)
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .OrderByDescending(j => j.UpdatedAt)
            .ThenByDescending(j => j.JobOrderId)
            .ToListAsync();

        return View(jobOrders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var jobOrder =
            await _context.JobOrders
                .FirstOrDefaultAsync(j => j.JobOrderId == id && j.IsArchived);

        if (jobOrder == null)
        {
            return NotFound();
        }

        jobOrder.IsArchived = false;
        jobOrder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["JobOrderNotification"] = $"Job Order #{id} restored successfully.";

        return RedirectToAction(nameof(Archived));
    }

    private async Task<List<JobOrder>> GetJobOrdersAsync()
    {
        return await _context.JobOrders
            .Where(j => !j.IsArchived)
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.Appointment)
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .OrderByDescending(j => j.JobOrderDate)
            .ToListAsync();
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
                v.ImageUrl,
                DisplayName =
                    v.PlateNumber +
                    " — " +
                    v.Make +
                    " " +
                    v.Model
            })
            .ToListAsync();

        ViewData["VehicleId"] =
            new SelectList(
                vehicles,
                "VehicleId",
                "DisplayName",
                selectedVehicleId);

        ViewData["VehicleImageMap"] = vehicles
            .ToDictionary(
                v => v.VehicleId,
                v => v.ImageUrl ?? string.Empty);

        var appointments =
            await _context.Appointments
                .AsNoTracking()
                .Where(a =>
                    a.Status == "Scheduled" &&
                    !_context.JobOrders.Any(j =>
                        j.AppointmentId == a.AppointmentId &&
                        !j.IsArchived))
                .Include(a => a.Vehicle)
                .OrderByDescending(
                    a => a.AppointmentDate)
                .Select(a => new
                {
                    a.AppointmentId,
                    a.VehicleId,
                    a.AppointmentDate,
                    a.Status,

                    DisplayName =
                        a.AppointmentDate
                            .ToString("yyyy-MM-dd HH:mm")
                        + " — "
                        + a.Vehicle!.PlateNumber
                        + " — "
                        + a.Vehicle.Make
                        + " "
                        + a.Vehicle.Model
                })
                .ToListAsync();

        ViewData["AppointmentId"] =
            new SelectList(
                appointments,
                "AppointmentId",
                "DisplayName",
                selectedAppointmentId);

        // The create drawer uses this metadata to automatically link a vehicle
        // to its scheduled appointment and inherit the appointment date/time.
        // Keep the full appointment list available so the UI can still show
        // "No appointment" for walk-in repairs.
        ViewData["AppointmentMetaJson"] =
            JsonSerializer.Serialize(
                appointments.Select(a => new
                {
                    id = a.AppointmentId,
                    vehicleId = a.VehicleId,
                    date = a.AppointmentDate.ToString("yyyy-MM-ddTHH:mm"),
                    status = a.Status ?? "Scheduled"
                }));

        var parts =
            await _context.Parts
                .Where(p => !p.IsArchived)
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.PartId,
                    p.Name,
                    p.UnitPrice,
                    p.StockQuantity,

                    DisplayName =
                        p.Name +
                        " (" +
                        p.StockQuantity +
                        " in stock — ₱" +
                        p.UnitPrice.ToString("N2") +
                        ")"
                })
                .ToListAsync();

        ViewData["PartId"] =
            new SelectList(
                parts,
                "PartId",
                "DisplayName");

        ViewData["PartMetaJson"] =
            JsonSerializer.Serialize(
                parts.Select(p => new
                {
                    id = p.PartId,
                    name = p.Name,
                    unitPrice = p.UnitPrice,
                    stock = p.StockQuantity
                }));
    }

    private sealed class EditPartPayload
    {
        public int PartId { get; set; }
        public int Quantity { get; set; }
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
                .Where(
                    b =>
                        b.InvoiceNumber
                            .StartsWith(prefix))
                .OrderByDescending(
                    b => b.InvoiceNumber)
                .Select(
                    b => b.InvoiceNumber)
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