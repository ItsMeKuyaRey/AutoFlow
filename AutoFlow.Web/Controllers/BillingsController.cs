using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;
using AutoFlow.Web.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace AutoFlow.Web.Controllers;

public class BillingsController : Controller
{
    private const decimal TaxRate = 0.12m;
    private readonly AutoFlowDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly XenditPaymentService _xendit;

    public BillingsController(AutoFlowDbContext context, IWebHostEnvironment environment, XenditPaymentService xendit)
    {
        _context = context;
        _environment = environment;
        _xendit = xendit;
    }

    // Billing workspace. Details and payment forms are rendered as drawers on this page.
    public async Task<IActionResult> Index(
        string? q,
        string? status,
        DateTime? issuedDate,
        string? open,
        int? id,
        int? jobOrderId,
        string? xendit,
        int? paymentId)
    {
        var query = _context.Billings
            .AsNoTracking()
            .Where(b => !b.IsArchived)
            .Include(b => b.JobOrder)
                .ThenInclude(j => j!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(b =>
                b.InvoiceNumber.Contains(q) ||
                b.JobOrder!.JobOrderId.ToString().Contains(q) ||
                (b.JobOrder.Vehicle!.PlateNumber ?? string.Empty).Contains(q) ||
                (b.JobOrder.Vehicle.Customer!.FirstName + " " + b.JobOrder.Vehicle.Customer.LastName).Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => b.Status == status);
        }

        if (issuedDate.HasValue)
        {
            var date = issuedDate.Value.Date;
            var nextDate = date.AddDays(1);
            query = query.Where(b => b.IssuedAt >= date && b.IssuedAt < nextDate);
        }

        var billingList = await query
            .OrderByDescending(b => b.IssuedAt)
            .ThenByDescending(b => b.BillingId)
            .ToListAsync();

        // Completed jobs without an invoice are the only jobs eligible for manual invoice creation.
        var invoiceJobIds = await _context.Billings
            .AsNoTracking()
            .Select(b => b.JobOrderId)
            .ToListAsync();

        var invoiceJobIdSet = invoiceJobIds.ToHashSet();

        var availableJobs = await _context.JobOrders
            .AsNoTracking()
            .Where(j => !j.IsArchived && j.Status == "Completed")
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .Where(j => !invoiceJobIdSet.Contains(j.JobOrderId))
            .OrderByDescending(j => j.JobOrderDate)
            .ThenByDescending(j => j.JobOrderId)
            .ToListAsync();

        ViewBag.CompletedJobCount = await _context.JobOrders
            .AsNoTracking()
            .CountAsync(j => !j.IsArchived && j.Status == "Completed");

        ViewBag.CompletedInvoicedJobCount = await _context.JobOrders
            .AsNoTracking()
            .Where(j => !j.IsArchived && j.Status == "Completed")
            .Join(_context.Billings.AsNoTracking(), j => j.JobOrderId, b => b.JobOrderId, (j, b) => j.JobOrderId)
            .CountAsync();

        ViewBag.ArchivedBillingCount = await _context.Billings
            .AsNoTracking()
            .CountAsync(b => b.IsArchived);

        if (string.Equals(open, "archived", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.ArchivedBillings = await _context.Billings
                .AsNoTracking()
                .Where(b => b.IsArchived)
                .Include(b => b.JobOrder)
                    .ThenInclude(j => j!.Vehicle)
                        .ThenInclude(v => v!.Customer)
                .OrderByDescending(b => b.UpdatedAt)
                .ThenByDescending(b => b.BillingId)
                .ToListAsync();
        }

        Billing? selectedBilling = null;
        if ((string.Equals(open, "details", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(open, "payment", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(open, "archive", StringComparison.OrdinalIgnoreCase)) && id.HasValue)
        {
            selectedBilling = await GetBillingAsync(id.Value);
        }

        ViewBag.Search = q ?? string.Empty;
        ViewBag.SelectedJobOrderId = jobOrderId;
        ViewBag.SelectedStatus = status ?? "All";
        ViewBag.SelectedDate = issuedDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        ViewBag.Open = open ?? string.Empty;
        ViewBag.SelectedBilling = selectedBilling;
        ViewBag.AvailableJobs = availableJobs;
        ViewBag.TaxRate = TaxRate;
        ViewBag.PaymentSuccess = TempData["PaymentSuccess"] as string;
        ViewBag.PaymentError = TempData["PaymentError"] as string;
        ViewBag.PaymentAmount = TempData["PaymentAmount"] as string;
        ViewBag.PaymentMethod = TempData["PaymentMethod"] as string ?? "Cash";
        ViewBag.PaymentNotes = TempData["PaymentNotes"] as string ?? string.Empty;
        ViewBag.PaymentId = paymentId;
        ViewBag.BillingInfo = TempData["BillingInfo"] as string;
        ViewBag.XenditPaymentRequestId = xendit ?? string.Empty;
        ViewBag.XenditStatus = string.Empty;
        ViewBag.XenditQrString = string.Empty;
        ViewBag.XenditCheckoutUrl = string.Empty;
        ViewBag.XenditError = string.Empty;

        if (!string.IsNullOrWhiteSpace(xendit) && selectedBilling != null && _xendit.IsConfigured)
        {
            var xenditPayment = await _xendit.GetPaymentRequestAsync(xendit);
            ViewBag.XenditStatus = xenditPayment.Status;
            ViewBag.XenditQrString = xenditPayment.QrString ?? string.Empty;
            ViewBag.XenditCheckoutUrl = xenditPayment.CheckoutUrl ?? string.Empty;
            ViewBag.XenditError = xenditPayment.Error ?? string.Empty;

            if (xenditPayment.Success && string.Equals(xenditPayment.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                await ApplyXenditPaymentAsync(selectedBilling.BillingId, xenditPayment);
                selectedBilling = await GetBillingAsync(selectedBilling.BillingId);
                ViewBag.SelectedBilling = selectedBilling;
            }
        }

        return View(billingList);
    }

    // Keep the documented route working while moving the UI into the billing workspace drawer.
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null || !await _context.Billings.AnyAsync(b => b.BillingId == id))
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { open = "details", id });
    }

    // Keep the documented route working while moving the payment UI into the billing workspace drawer.
    [HttpGet]
    public async Task<IActionResult> RecordPayment(int? id)
    {
        if (id == null || !await _context.Billings.AnyAsync(b => b.BillingId == id))
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { open = "payment", id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(
        int billingId,
        decimal amountPaid,
        string? paymentMethod,
        string? notes)
    {
        var billing = await _context.Billings.FindAsync(billingId);
        if (billing == null)
        {
            return NotFound();
        }

        var remaining = Math.Max(0, billing.TotalAmount - billing.AmountPaid);
        var error = string.Empty;

        if (billing.Status == "Paid" || remaining <= 0)
        {
            error = "This invoice is already fully paid.";
        }
        else if (amountPaid <= 0)
        {
            error = "Amount must be greater than zero.";
        }
        else if (amountPaid > remaining)
        {
            error = $"Amount cannot exceed the remaining balance of ₱{remaining:N2}.";
        }

        if (!string.IsNullOrEmpty(error))
        {
            TempData["PaymentError"] = error;
            TempData["PaymentAmount"] = amountPaid.ToString("0.00");
            TempData["PaymentMethod"] = paymentMethod ?? "Cash";
            TempData["PaymentNotes"] = notes ?? string.Empty;

            return RedirectToAction(nameof(Index), new { open = "payment", id = billingId });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        var payment = new Payment
        {
            BillingId = billing.BillingId,
            AmountPaid = amountPaid,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Cash" : paymentMethod.Trim(),
            PaymentDate = DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        billing.AmountPaid += amountPaid;
        billing.UpdatedAt = DateTime.UtcNow;
        billing.Status = billing.AmountPaid >= billing.TotalAmount ? "Paid" : "Partial";

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["PaymentSuccess"] = $"Payment of ₱{amountPaid:N2} recorded successfully.";
        return RedirectToAction(nameof(Index), new { open = "payment", id = billing.BillingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPayment(
        int billingId,
        int paymentId,
        decimal amountPaid,
        string? paymentMethod,
        string? notes)
    {
        var billing = await _context.Billings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BillingId == billingId);

        if (billing == null)
        {
            return NotFound();
        }

        var payment = billing.Payments.FirstOrDefault(p => p.PaymentId == paymentId);
        if (payment == null)
        {
            return NotFound();
        }

        var otherPaymentsTotal = billing.Payments
            .Where(p => p.PaymentId != paymentId)
            .Sum(p => p.AmountPaid);
        var maximumAllowed = Math.Max(0, billing.TotalAmount - otherPaymentsTotal);

        if (amountPaid <= 0)
        {
            TempData["PaymentError"] = "Payment amount must be greater than zero.";
        }
        else if (amountPaid > maximumAllowed)
        {
            TempData["PaymentError"] = $"Amount cannot exceed the invoice balance available for this payment: ₱{maximumAllowed:N2}.";
        }
        else if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            TempData["PaymentError"] = "Please select a payment method.";
        }
        else
        {
            payment.AmountPaid = amountPaid;
            payment.PaymentMethod = paymentMethod.Trim();
            payment.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            billing.AmountPaid = billing.Payments.Sum(p => p.AmountPaid);
            billing.UpdatedAt = DateTime.UtcNow;
            billing.Status = billing.AmountPaid >= billing.TotalAmount
                ? "Paid"
                : billing.AmountPaid > 0
                    ? "Partial"
                    : "Unpaid";

            await _context.SaveChangesAsync();

            TempData["PaymentSuccess"] = $"Payment #{payment.PaymentId} was updated successfully.";
            return RedirectToAction(nameof(Index), new { open = "payment", id = billing.BillingId });
        }

        TempData["PaymentAmount"] = amountPaid.ToString("0.00");
        TempData["PaymentMethod"] = paymentMethod ?? payment.PaymentMethod;
        TempData["PaymentNotes"] = notes ?? payment.Notes ?? string.Empty;
        return RedirectToAction(nameof(Index), new { open = "payment", id = billing.BillingId, paymentId = payment.PaymentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartXenditQr(
        int billingId,
        decimal amountPaid,
        string? notes)
    {
        var billing = await _context.Billings.FindAsync(billingId);
        if (billing == null)
        {
            return NotFound();
        }

        var remaining = Math.Max(0, billing.TotalAmount - billing.AmountPaid);
        if (billing.Status == "Paid" || remaining <= 0)
        {
            TempData["PaymentError"] = "This invoice is already fully paid.";
            return RedirectToAction(nameof(Index), new { open = "payment", id = billingId });
        }

        if (amountPaid <= 0 || amountPaid > remaining)
        {
            TempData["PaymentError"] = $"Enter an amount between ₱0.01 and ₱{remaining:N2}.";
            TempData["PaymentAmount"] = amountPaid.ToString("0.00");
            TempData["PaymentMethod"] = "GCash";
            TempData["PaymentNotes"] = notes ?? string.Empty;
            return RedirectToAction(nameof(Index), new { open = "payment", id = billingId });
        }

        if (!_xendit.IsConfigured)
        {
            TempData["PaymentError"] = "Online payment is not configured yet. Set XENDIT_SECRET_KEY in the environment, then restart AutoFlow.";
            TempData["PaymentAmount"] = amountPaid.ToString("0.00");
            TempData["PaymentMethod"] = "GCash";
            TempData["PaymentNotes"] = notes ?? string.Empty;
            return RedirectToAction(nameof(Index), new { open = "payment", id = billingId });
        }

        var referenceId = $"autoflow-billing-{billingId}-{Guid.NewGuid():N}";
        var description = $"AutoFlow {billing.InvoiceNumber} payment";
        var result = await _xendit.CreateQrPaymentAsync(referenceId, amountPaid, description);

        if (!result.Success || string.IsNullOrWhiteSpace(result.PaymentRequestId))
        {
            TempData["PaymentError"] = result.Error ?? "Xendit could not create the QR payment.";
            TempData["PaymentAmount"] = amountPaid.ToString("0.00");
            TempData["PaymentMethod"] = "GCash";
            TempData["PaymentNotes"] = notes ?? string.Empty;
            return RedirectToAction(nameof(Index), new { open = "payment", id = billingId });
        }

        TempData["PaymentAmount"] = amountPaid.ToString("0.00");
        TempData["PaymentMethod"] = "GCash";
        TempData["PaymentNotes"] = notes ?? string.Empty;

        return RedirectToAction(nameof(Index), new
        {
            open = "payment",
            id = billingId,
            xendit = result.PaymentRequestId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimulateXenditPayment(int billingId, string paymentRequestId)
    {
        var billing = await _context.Billings.FindAsync(billingId);
        if (billing == null)
        {
            return NotFound();
        }

        var remaining = Math.Max(0, billing.TotalAmount - billing.AmountPaid);
        var result = await _xendit.GetPaymentRequestAsync(paymentRequestId);
        if (!result.Success || result.Amount <= 0 || result.Amount > remaining)
        {
            return BadRequest(new { success = false, message = result.Error ?? "The Xendit payment request is invalid." });
        }

        var simulated = await _xendit.SimulatePaymentAsync(paymentRequestId, result.Amount);
        return Json(new { success = simulated, message = simulated ? "Xendit test payment simulation started." : "Xendit test simulation was rejected." });
    }

    [HttpGet]
    public async Task<IActionResult> CheckXenditStatus(int billingId, string paymentRequestId)
    {
        if (string.IsNullOrWhiteSpace(paymentRequestId))
        {
            return BadRequest(new { success = false, message = "Payment request ID is required." });
        }

        var billing = await _context.Billings.FindAsync(billingId);
        if (billing == null)
        {
            return NotFound();
        }

        var result = await _xendit.GetPaymentRequestAsync(paymentRequestId);
        if (!result.Success)
        {
            return Json(new { success = false, status = "ERROR", message = result.Error });
        }

        var paymentRecorded = false;
        if (string.Equals(result.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
        {
            paymentRecorded = await ApplyXenditPaymentAsync(billingId, result);
        }

        return Json(new
        {
            success = true,
            status = result.Status,
            paymentRecorded,
            message = result.Status.Equals("SUCCEEDED", StringComparison.OrdinalIgnoreCase)
                ? "Payment confirmed."
                : "Payment is still waiting for customer confirmation."
        });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> XenditWebhook()
    {
        var expectedToken = _xendit.WebhookToken;
        if (!string.IsNullOrWhiteSpace(expectedToken))
        {
            var callbackToken = Request.Headers["x-callback-token"].ToString();
            if (!string.Equals(callbackToken, expectedToken, StringComparison.Ordinal))
            {
                return Unauthorized();
            }
        }

        using var document = await JsonDocument.ParseAsync(Request.Body);
        var root = document.RootElement;
        var eventName = root.TryGetProperty("event", out var eventProperty)
            ? eventProperty.GetString()
            : null;

        if (!string.Equals(eventName, "payment.capture", StringComparison.OrdinalIgnoreCase))
        {
            return Ok();
        }

        if (!root.TryGetProperty("data", out var data))
        {
            return BadRequest();
        }

        var paymentRequestId = GetJsonString(data, "payment_request_id");
        var referenceId = GetJsonString(data, "reference_id");
        var status = GetJsonString(data, "status");
        var amount = GetJsonDecimal(data, "request_amount");

        if (!string.Equals(status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(eventName, "payment.capture", StringComparison.OrdinalIgnoreCase))
        {
            return Ok();
        }

        if (!TryGetBillingId(referenceId, out var billingId) || string.IsNullOrWhiteSpace(paymentRequestId))
        {
            return Ok();
        }

        var result = new XenditPaymentResult(
            true,
            null,
            paymentRequestId,
            referenceId,
            "SUCCEEDED",
            amount,
            null,
            null);

        await ApplyXenditPaymentAsync(billingId, result);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Archive(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var exists = await _context.Billings
            .AsNoTracking()
            .AnyAsync(b => b.BillingId == id && !b.IsArchived);

        if (!exists)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { open = "archive", id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var billing = await _context.Billings
            .FirstOrDefaultAsync(b => b.BillingId == id && !b.IsArchived);

        if (billing == null)
        {
            return NotFound();
        }

        billing.IsArchived = true;
        billing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["BillingInfo"] = $"Invoice {billing.InvoiceNumber} archived successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Archived()
    {
        return RedirectToAction(nameof(Index), new { open = "archived" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var billing = await _context.Billings
            .FirstOrDefaultAsync(b => b.BillingId == id && b.IsArchived);

        if (billing == null)
        {
            return NotFound();
        }

        billing.IsArchived = false;
        billing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["BillingInfo"] = $"Invoice {billing.InvoiceNumber} restored successfully.";
        return RedirectToAction(nameof(Index), new { open = "archived" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvoice(int jobOrderId, IFormFile? vehicleImage)
    {
        var jobOrder = await _context.JobOrders
            .Include(j => j.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(j => j.JobOrderParts)
                .ThenInclude(jp => jp.Part)
            .FirstOrDefaultAsync(j =>
                j.JobOrderId == jobOrderId &&
                !j.IsArchived &&
                j.Status == "Completed");

        if (jobOrder == null)
        {
            TempData["BillingError"] = "The selected repair job is no longer available for invoicing.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.Billings.AnyAsync(b => b.JobOrderId == jobOrderId))
        {
            TempData["BillingError"] = $"Job Order #{jobOrderId} already has an invoice.";
            return RedirectToAction(nameof(Index), new { open = "details", id = await _context.Billings.Where(b => b.JobOrderId == jobOrderId).Select(b => b.BillingId).FirstAsync() });
        }

        if (vehicleImage is { Length: > 0 } && !IsValidVehicleImage(vehicleImage))
        {
            TempData["BillingError"] = "Please choose a valid PNG, JPG, WEBP, or GIF image smaller than 5 MB.";
            return RedirectToAction(nameof(Index), new { open = "create" });
        }

        var partsCost = jobOrder.JobOrderParts.Sum(jp => jp.Quantity * jp.UnitPriceAtTimeOfUse);
        var subtotal = jobOrder.LaborCost + partsCost;
        var taxAmount = Math.Round(subtotal * TaxRate, 2, MidpointRounding.AwayFromZero);
        var totalAmount = subtotal + taxAmount;

        string? newVehicleImagePath = null;
        var oldVehicleImagePath = jobOrder.Vehicle?.ImageUrl;

        if (vehicleImage is { Length: > 0 })
        {
            newVehicleImagePath = await SaveVehicleImageAsync(vehicleImage);
            if (newVehicleImagePath == null)
            {
                TempData["BillingError"] = "The vehicle image could not be saved. Please choose another image and try again.";
                return RedirectToAction(nameof(Index), new { open = "create" });
            }

            if (jobOrder.Vehicle != null)
            {
                jobOrder.Vehicle.ImageUrl = newVehicleImagePath;
                jobOrder.Vehicle.UpdatedAt = DateTime.UtcNow;
            }
        }

        var billing = new Billing
        {
            JobOrderId = jobOrder.JobOrderId,
            InvoiceNumber = await GenerateInvoiceNumberAsync(),
            LaborCost = jobOrder.LaborCost,
            PartsCost = partsCost,
            DiscountAmount = 0,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            AmountPaid = 0,
            Status = "Unpaid",
            IssuedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Billings.Add(billing);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The JobOrderId has a unique database index. If another request created
            // the invoice at the same time, return the existing invoice instead of
            // exposing a database error to the user.
            var existingBillingId = await _context.Billings
                .AsNoTracking()
                .Where(b => b.JobOrderId == jobOrderId)
                .Select(b => (int?)b.BillingId)
                .FirstOrDefaultAsync();

            if (existingBillingId.HasValue)
            {
                if (newVehicleImagePath != null)
                {
                    DeleteStoredVehicleImage(newVehicleImagePath);
                }

                TempData["BillingInfo"] = $"Job Order #{jobOrderId} already has an invoice.";
                return RedirectToAction(nameof(Index), new { open = "details", id = existingBillingId.Value });
            }

            if (newVehicleImagePath != null)
            {
                DeleteStoredVehicleImage(newVehicleImagePath);
            }

            throw;
        }

        if (newVehicleImagePath != null && !string.Equals(oldVehicleImagePath, newVehicleImagePath, StringComparison.OrdinalIgnoreCase))
        {
            DeleteStoredVehicleImage(oldVehicleImagePath);
        }

        return RedirectToAction(nameof(Index), new { open = "details", id = billing.BillingId });
    }

    [HttpGet]
    public async Task<FileResult> ExportCsv(
        string? q,
        string? status,
        DateTime? issuedDate)
    {
        var query = _context.Billings
            .AsNoTracking()
            .Where(b => !b.IsArchived)
            .Include(b => b.JobOrder)
                .ThenInclude(j => j!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(b =>
                b.InvoiceNumber.Contains(q) ||
                b.JobOrder!.JobOrderId.ToString().Contains(q) ||
                b.JobOrder.Vehicle!.PlateNumber.Contains(q) ||
                (b.JobOrder.Vehicle.Customer!.FirstName + " " + b.JobOrder.Vehicle.Customer.LastName).Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => b.Status == status);
        }

        if (issuedDate.HasValue)
        {
            var date = issuedDate.Value.Date;
            query = query.Where(b => b.IssuedAt >= date && b.IssuedAt < date.AddDays(1));
        }

        var rows = await query
            .OrderByDescending(b => b.IssuedAt)
            .Select(b => new
            {
                b.InvoiceNumber,
                JobOrder = b.JobOrderId,
                Customer = b.JobOrder!.Vehicle!.Customer!.FirstName + " " + b.JobOrder.Vehicle.Customer.LastName,
                Vehicle = b.JobOrder.Vehicle.PlateNumber,
                b.IssuedAt,
                b.TotalAmount,
                b.AmountPaid,
                Balance = b.TotalAmount - b.AmountPaid,
                b.Status
            })
            .ToListAsync();

        static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

        var lines = new List<string>
        {
            "Invoice Number,Job Order,Customer,Vehicle,Invoice Date,Total Amount,Amount Paid,Balance,Status"
        };

        lines.AddRange(rows.Select(r => string.Join(",", new[]
        {
            Csv(r.InvoiceNumber),
            r.JobOrder.ToString(),
            Csv(r.Customer),
            Csv(r.Vehicle),
            Csv(r.IssuedAt.ToString("yyyy-MM-dd HH:mm")),
            r.TotalAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            r.AmountPaid.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            r.Balance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            Csv(r.Status)
        })));

        return File(
            System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)),
            "text/csv",
            $"autoflow-invoices-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }


    private async Task<bool> ApplyXenditPaymentAsync(int billingId, XenditPaymentResult result)
    {
        if (!result.Success ||
            !string.Equals(result.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(result.PaymentRequestId))
        {
            return false;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var billing = await _context.Billings.FirstOrDefaultAsync(b => b.BillingId == billingId);
        if (billing == null)
        {
            return false;
        }

        var marker = $"Xendit Payment Request: {result.PaymentRequestId}";
        var alreadyRecorded = await _context.Payments
            .AsNoTracking()
            .AnyAsync(p => p.BillingId == billingId && p.Notes != null && p.Notes.Contains(marker));

        if (alreadyRecorded)
        {
            await transaction.CommitAsync();
            return false;
        }

        var remaining = Math.Max(0, billing.TotalAmount - billing.AmountPaid);
        var amount = Math.Min(result.Amount, remaining);
        if (amount <= 0)
        {
            await transaction.CommitAsync();
            return false;
        }

        var payment = new Payment
        {
            BillingId = billing.BillingId,
            AmountPaid = amount,
            PaymentMethod = "GCash / QRPh",
            PaymentDate = DateTime.UtcNow,
            Notes = marker,
            CreatedAt = DateTime.UtcNow
        };

        billing.AmountPaid += amount;
        billing.UpdatedAt = DateTime.UtcNow;
        billing.Status = billing.AmountPaid >= billing.TotalAmount ? "Paid" : "Partial";

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    private static bool TryGetBillingId(string? referenceId, out int billingId)
    {
        billingId = 0;
        const string prefix = "autoflow-billing-";
        if (string.IsNullOrWhiteSpace(referenceId) || !referenceId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = referenceId[prefix.Length..];
        var separator = remainder.IndexOf('-');
        if (separator <= 0)
        {
            return false;
        }

        return int.TryParse(remainder[..separator], out billingId);
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static decimal GetJsonDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.TryGetDecimal(out var value)
            ? value
            : 0m;
    }

    private bool IsValidVehicleImage(IFormFile file)
    {
        if (file.Length <= 0 || file.Length > 5 * 1024 * 1024)
        {
            return false;
        }

        var extension = Path.GetExtension(file.FileName);
        return new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }
            .Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string?> SaveVehicleImageAsync(IFormFile file)
    {
        if (!IsValidVehicleImage(file))
        {
            return null;
        }

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "vehicles");
        Directory.CreateDirectory(uploadsFolder);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.CreateNew);
        await file.CopyToAsync(stream);

        return $"/uploads/vehicles/{fileName}";
    }

    private void DeleteStoredVehicleImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !imageUrl.StartsWith("/uploads/vehicles/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(_environment.WebRootPath, relativePath);

        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch
        {
            // Do not fail invoice creation because an old image could not be removed.
        }
    }

    private Task<Billing?> GetBillingAsync(int id)
    {
        return _context.Billings
            .AsNoTracking()
            .Include(b => b.JobOrder)
                .ThenInclude(j => j!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .Include(b => b.JobOrder)
                .ThenInclude(j => j!.JobOrderParts)
                    .ThenInclude(jp => jp.Part)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BillingId == id);
    }

    private async Task<string> GenerateInvoiceNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";

        var lastInvoice = await _context.Billings
            .Where(b => b.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(b => b.InvoiceNumber)
            .Select(b => b.InvoiceNumber)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (!string.IsNullOrWhiteSpace(lastInvoice) &&
            int.TryParse(lastInvoice[prefix.Length..], out var parsed))
        {
            nextNumber = parsed + 1;
        }

        return $"{prefix}{nextNumber:D4}";
    }
}
