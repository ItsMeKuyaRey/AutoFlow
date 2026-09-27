using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers;

public class SuppliersController : Controller
{
    private readonly AutoFlowDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public SuppliersController(AutoFlowDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: Suppliers
    public async Task<IActionResult> Index()
    {
        var suppliers = await _context.Suppliers
            .OrderBy(s => s.Status == "Archived")
            .ThenBy(s => s.Name)
            .ToListAsync();

        return View(suppliers);
    }

    private bool IsDrawerRequest => string.Equals(Request.Query["drawer"], "1", StringComparison.OrdinalIgnoreCase);

    // GET: Suppliers/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var supplier = await _context.Suppliers
            .Include(s => s.Parts)
            .FirstOrDefaultAsync(s => s.SupplierId == id);

        if (supplier == null) return NotFound();

        if (IsDrawerRequest)
        {
            ViewData["SupplierDrawerMode"] = "details";
            return PartialView("_SupplierDrawer", supplier);
        }

        return View(supplier);
    }

    // GET: Suppliers/Create
    public IActionResult Create()
    {
        var supplier = new Supplier { Status = "Active" };

        if (IsDrawerRequest)
        {
            ViewData["SupplierDrawerMode"] = "create";
            return PartialView("_SupplierDrawer", supplier);
        }

        return View(supplier);
    }

    // POST: Suppliers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Name,ContactPerson,Phone,Email,Address,City,Status,Notes")] Supplier supplier,
        IFormFile? profileImage)
    {
        if (ModelState.IsValid)
        {
            if (profileImage != null && profileImage.Length > 0)
            {
                var uploadResult = await SaveSupplierImageAsync(profileImage);
                if (!uploadResult.Success)
                {
                    ModelState.AddModelError("", uploadResult.ErrorMessage ?? "Unable to save supplier image.");
                }
                else
                {
                    supplier.ImageUrl = uploadResult.Path;
                }
            }

            if (ModelState.IsValid)
            {
                supplier.CreatedAt = DateTime.UtcNow;
                supplier.UpdatedAt = DateTime.UtcNow;
                _context.Add(supplier);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
        }

        if (IsDrawerRequest)
        {
            ViewData["SupplierDrawerMode"] = "create";
            return PartialView("_SupplierDrawer", supplier);
        }

        return View(supplier);
    }

    // GET: Suppliers/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null) return NotFound();

        if (IsDrawerRequest)
        {
            ViewData["SupplierDrawerMode"] = "edit";
            return PartialView("_SupplierDrawer", supplier);
        }

        return View(supplier);
    }

    // POST: Suppliers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("SupplierId,Name,ContactPerson,Phone,Email,Address,City,Status,Notes")] Supplier supplier,
        IFormFile? profileImage)
    {
        if (id != supplier.SupplierId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existing = await _context.Suppliers.FindAsync(id);
                if (existing == null) return NotFound();

                existing.Name = supplier.Name;
                existing.ContactPerson = supplier.ContactPerson;
                existing.Phone = supplier.Phone;
                existing.Email = supplier.Email;
                existing.Address = supplier.Address;
                existing.City = supplier.City;
                existing.Status = supplier.Status;
                existing.Notes = supplier.Notes;

                if (profileImage != null && profileImage.Length > 0)
                {
                    var uploadResult = await SaveSupplierImageAsync(profileImage);
                    if (!uploadResult.Success)
                    {
                        ModelState.AddModelError("", uploadResult.ErrorMessage ?? "Unable to save supplier image.");
                    }
                    else
                    {
                        DeleteSupplierImage(existing.ImageUrl);
                        existing.ImageUrl = uploadResult.Path;
                    }
                }

                if (ModelState.IsValid)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Suppliers.Any(e => e.SupplierId == supplier.SupplierId))
                    return NotFound();
                throw;
            }
        }


        if (IsDrawerRequest)
        {
            ViewData["SupplierDrawerMode"] = "edit";
            return PartialView("_SupplierDrawer", supplier);
        }

        return View(supplier);
    }

    // GET: Suppliers/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var supplier = await _context.Suppliers
            .Include(s => s.Parts)
            .FirstOrDefaultAsync(s => s.SupplierId == id);

        if (supplier == null) return NotFound();

        if (IsDrawerRequest)
        {
            ViewData["SupplierDrawerMode"] = "archive";
            return PartialView("_SupplierDrawer", supplier);
        }

        return View(supplier);
    }

    // POST: Suppliers/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier != null)
        {
            supplier.Status = "Archived";
            supplier.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Suppliers/Restore/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null) return NotFound();

        if (supplier.Status == "Archived")
        {
            supplier.Status = "Active";
            supplier.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<(bool Success, string? Path, string? ErrorMessage)> SaveSupplierImageAsync(IFormFile file)
    {
        const long maxBytes = 5 * 1024 * 1024;
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };
        var allowedContentTypes = new[] { "image/png", "image/jpeg", "image/webp", "image/gif" };

        if (file.Length > maxBytes)
            return (false, null, "Supplier image must be 5 MB or smaller.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = file.ContentType.ToLowerInvariant();

        if (!allowedExtensions.Contains(extension) || !allowedContentTypes.Contains(contentType))
            return (false, null, "Please choose a PNG, JPG, WEBP, or GIF image.");

        var uploadDirectory = Path.Combine(_environment.WebRootPath, "uploads", "suppliers");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadDirectory, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return (true, $"/uploads/suppliers/{fileName}", null);
    }

    private void DeleteSupplierImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;

        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }
}
