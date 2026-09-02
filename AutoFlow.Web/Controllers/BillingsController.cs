using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers;

public class BillingsController : Controller
{
    private readonly AutoFlowDbContext _context;

    public BillingsController(AutoFlowDbContext context)
    {
        _context = context;
    }

    // GET: Billings
    public async Task<IActionResult> Index()
    {
        var billings = await _context.Billings
            .Include(b => b.JobOrder)
                .ThenInclude(j => j.Vehicle!)
                    .ThenInclude(v => v.Customer)
            .OrderByDescending(b => b.IssuedAt)
            .ToListAsync();
        return View(billings);
    }

    // GET: Billings/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var billing = await _context.Billings
            .Include(b => b.JobOrder)
                .ThenInclude(j => j.Vehicle)
                    .ThenInclude(v => v.Customer)
            .Include(b => b.JobOrder)
                .ThenInclude(j => j.JobOrderParts)
                    .ThenInclude(jp => jp.Part)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BillingId == id);

        if (billing == null) return NotFound();

        return View(billing);
    }

    // GET: Billings/RecordPayment/5
    public async Task<IActionResult> RecordPayment(int? id)
    {
        if (id == null) return NotFound();

        var billing = await _context.Billings
            .Include(b => b.JobOrder)
                .ThenInclude(j => j.Vehicle)
            .FirstOrDefaultAsync(b => b.BillingId == id);

        if (billing == null) return NotFound();

        ViewBag.BillingId = billing.BillingId;
        ViewBag.InvoiceNumber = billing.InvoiceNumber;
        ViewBag.TotalAmount = billing.TotalAmount;
        ViewBag.AmountPaid = billing.AmountPaid;
        ViewBag.Remaining = billing.TotalAmount - billing.AmountPaid;

        return View();
    }

    // POST: Billings/RecordPayment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int billingId, decimal amountPaid, string paymentMethod, string? notes)
    {
        var billing = await _context.Billings.FindAsync(billingId);
        if (billing == null) return NotFound();

        if (amountPaid <= 0)
        {
            ModelState.AddModelError("", "Amount must be greater than zero.");
        }

        var remaining = billing.TotalAmount - billing.AmountPaid;
        if (amountPaid > remaining)
        {
            ModelState.AddModelError("", $"Amount cannot exceed remaining balance of {remaining:C}.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.BillingId = billing.BillingId;
            ViewBag.InvoiceNumber = billing.InvoiceNumber;
            ViewBag.TotalAmount = billing.TotalAmount;
            ViewBag.AmountPaid = billing.AmountPaid;
            ViewBag.Remaining = remaining;
            return View();
        }

        var payment = new Payment
        {
            BillingId = billing.BillingId,
            AmountPaid = amountPaid,
            PaymentMethod = paymentMethod ?? "Cash",
            PaymentDate = DateTime.UtcNow,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        billing.AmountPaid += amountPaid;
        billing.UpdatedAt = DateTime.UtcNow;

        if (billing.AmountPaid >= billing.TotalAmount)
        {
            billing.Status = "Paid";
        }
        else if (billing.AmountPaid > 0)
        {
            billing.Status = "Partial";
        }

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = billing.BillingId });
    }
}
