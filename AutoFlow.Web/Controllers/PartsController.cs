using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers;

public class PartsController : Controller
{
    private readonly AutoFlowDbContext _context;

    public PartsController(AutoFlowDbContext context)
    {
        _context = context;
    }

    // GET: Parts
    public async Task<IActionResult> Index()
    {
        var parts = await _context.Parts
            .Include(p => p.Supplier)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return View(parts);
    }

    // GET: Parts/Create
    public async Task<IActionResult> Create()
    {
        await LoadSupplierDropdown();
        return View();
    }

    // POST: Parts/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Name,PartNumber,UnitPrice,StockQuantity,SupplierId")]
        Part part)
    {
        if (ModelState.IsValid)
        {
            part.CreatedAt = DateTime.UtcNow;
            part.UpdatedAt = DateTime.UtcNow;
            _context.Parts.Add(part);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        await LoadSupplierDropdown(part.SupplierId);
        return View(part);
    }

    // GET: Parts/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var part = await _context.Parts.FindAsync(id);
        if (part == null) return NotFound();

        await LoadSupplierDropdown(part.SupplierId);
        return View(part);
    }

    // POST: Parts/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("PartId,Name,PartNumber,UnitPrice,StockQuantity,SupplierId")]
        Part part)
    {
        if (id != part.PartId) return NotFound();

        if (ModelState.IsValid)
        {
            var existingPart = await _context.Parts.FindAsync(id);
            if (existingPart == null) return NotFound();

            existingPart.Name = part.Name;
            existingPart.PartNumber = part.PartNumber;
            existingPart.UnitPrice = part.UnitPrice;
            existingPart.StockQuantity = part.StockQuantity;
            existingPart.SupplierId = part.SupplierId;
            existingPart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        await LoadSupplierDropdown(part.SupplierId);
        return View(part);
    }

    // GET: Parts/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var part = await _context.Parts
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.PartId == id);

        if (part == null) return NotFound();

        return View(part);
    }

    // POST: Parts/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var part = await _context.Parts.FindAsync(id);
        if (part != null)
        {
            _context.Parts.Remove(part);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadSupplierDropdown(int? selectedSupplierId = null)
    {
        var suppliers = await _context.Suppliers
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.Name)
            .Select(s => new { s.SupplierId, s.Name })
            .ToListAsync();

        ViewData["SupplierId"] = new SelectList(suppliers, "SupplierId", "Name", selectedSupplierId);
    }
}
