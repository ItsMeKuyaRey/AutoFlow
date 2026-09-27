using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers;

public class PartsController : Controller
{
    private readonly AutoFlowDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public PartsController(AutoFlowDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: Parts
    public async Task<IActionResult> Index()
    {
        var parts = await _context.Parts
            .Include(p => p.Supplier)
            .OrderBy(p => p.IsArchived)
            .ThenBy(p => p.Name)
            .ToListAsync();

        ViewData["SupplierOptions"] = await _context.Suppliers
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .Distinct()
            .ToListAsync();

        await LoadSupplierDropdown();

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
        Part part,
        IFormFile? profileImage)
    {
        if (ModelState.IsValid)
        {
            part.CreatedAt = DateTime.UtcNow;
            part.UpdatedAt = DateTime.UtcNow;
            part.IsArchived = false;

            if (profileImage != null && profileImage.Length > 0)
            {
                var uploadResult = await SavePartImageAsync(profileImage);
                if (!uploadResult.Success)
                {
                    TempData["PartNotificationError"] = uploadResult.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }

                part.ImageUrl = uploadResult.Path;
            }

            _context.Parts.Add(part);
            await _context.SaveChangesAsync();

            TempData["PartNotification"] = "Part added successfully.";
            return RedirectToAction(nameof(Index));
        }

        await LoadSupplierDropdown(part.SupplierId);
        return View(part);
    }

    // GET: Parts/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var part = await _context.Parts.FindAsync(id);

        if (part == null)
            return NotFound();

        await LoadSupplierDropdown(part.SupplierId);
        return View(part);
    }

    // POST: Parts/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("PartId,Name,PartNumber,UnitPrice,StockQuantity,SupplierId")]
        Part part,
        IFormFile? profileImage)
    {
        if (id != part.PartId)
            return NotFound();

        if (ModelState.IsValid)
        {
            var existingPart = await _context.Parts.FindAsync(id);

            if (existingPart == null)
                return NotFound();

            existingPart.Name = part.Name;
            existingPart.PartNumber = part.PartNumber;
            existingPart.UnitPrice = part.UnitPrice;
            existingPart.StockQuantity = part.StockQuantity;
            existingPart.SupplierId = part.SupplierId;

            if (profileImage != null && profileImage.Length > 0)
            {
                var uploadResult = await SavePartImageAsync(profileImage);
                if (!uploadResult.Success)
                {
                    TempData["PartNotificationError"] = uploadResult.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }

                DeletePartImage(existingPart.ImageUrl);
                existingPart.ImageUrl = uploadResult.Path;
            }

            existingPart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["PartNotification"] = "Part updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        await LoadSupplierDropdown(part.SupplierId);
        return View(part);
    }

    // POST: Parts/AdjustStock
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(
        int partId,
        string adjustmentType,
        int quantity)
    {
        if (quantity <= 0)
        {
            TempData["PartNotificationError"] = "Adjustment quantity must be greater than zero.";
            return RedirectToAction(nameof(Index));
        }

        var part = await _context.Parts.FindAsync(partId);

        if (part == null)
            return NotFound();

        if (adjustmentType == "add")
        {
            part.StockQuantity += quantity;
        }
        else if (adjustmentType == "remove")
        {
            if (quantity > part.StockQuantity)
            {
                TempData["PartNotificationError"] =
                    $"Cannot remove {quantity} units. {part.StockQuantity} units are currently available.";

                return RedirectToAction(nameof(Index));
            }

            part.StockQuantity -= quantity;
        }
        else
        {
            TempData["PartNotificationError"] = "Invalid stock adjustment type.";
            return RedirectToAction(nameof(Index));
        }

        part.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["PartNotification"] =
            $"{part.Name} stock adjusted successfully. Current stock: {part.StockQuantity}.";

        return RedirectToAction(nameof(Index));
    }

    // POST: Parts/Archive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var part = await _context.Parts.FindAsync(id);

        if (part == null)
            return NotFound();

        if (!part.IsArchived)
        {
            part.IsArchived = true;
            part.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["PartNotification"] = $"{part.Name} was archived successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Parts/Restore/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var part = await _context.Parts.FindAsync(id);

        if (part == null)
            return NotFound();

        if (part.IsArchived)
        {
            part.IsArchived = false;
            part.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["PartNotification"] = $"{part.Name} was restored successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<(bool Success, string? Path, string? ErrorMessage)> SavePartImageAsync(IFormFile file)
    {
        const long maxBytes = 5 * 1024 * 1024;
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };
        var allowedContentTypes = new[] { "image/png", "image/jpeg", "image/webp", "image/gif" };

        if (file.Length > maxBytes)
            return (false, null, "Part image must be 5 MB or smaller.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension) || !allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            return (false, null, "Please choose a PNG, JPG, WEBP, or GIF image.");

        var uploadDirectory = Path.Combine(_environment.WebRootPath, "uploads", "parts");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadDirectory, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return (true, $"/uploads/parts/{fileName}", null);
    }

    private void DeletePartImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return;

        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }

    private async Task LoadSupplierDropdown(int? selectedSupplierId = null)
    {
        var suppliers = await _context.Suppliers
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.SupplierId,
                s.Name
            })
            .ToListAsync();

        ViewData["SupplierId"] =
            new SelectList(
                suppliers,
                "SupplierId",
                "Name",
                selectedSupplierId);
    }
}