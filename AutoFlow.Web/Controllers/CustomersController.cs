using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace AutoFlow.Web.Controllers
{
    public class CustomersController : Controller
    {
        private readonly AutoFlowDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public CustomersController(
            AutoFlowDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                .AsNoTracking()
                .OrderByDescending(c => c.IsArchived)
                .ThenByDescending(c => c.CustomerId)
                .ToListAsync();

            return View(customers);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("FirstName,LastName,Phone,Email,Address")]
            Customer customer,
            IFormFile? profileImage)
        {
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            if (profileImage is { Length: > 0 })
            {
                var imagePath = await SaveImageAsync(profileImage, "customers");

                if (imagePath == null)
                {
                    ModelState.AddModelError(
                        "ImageUrl",
                        "Please choose a valid image file smaller than 5 MB.");

                    return View(customer);
                }

                customer.ImageUrl = imagePath;
            }

            customer.CreatedAt = DateTime.UtcNow;
            customer.UpdatedAt = DateTime.UtcNow;
            customer.IsArchived = false;

            _context.Customers.Add(customer);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("CustomerId,FirstName,LastName,Phone,Email,Address")]
            Customer customer,
            IFormFile? profileImage)
        {
            if (id != customer.CustomerId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            try
            {
                var existingCustomer =
                    await _context.Customers.FindAsync(id);

                if (existingCustomer == null)
                {
                    return NotFound();
                }

                existingCustomer.FirstName = customer.FirstName;
                existingCustomer.LastName = customer.LastName;
                existingCustomer.Phone = customer.Phone;
                existingCustomer.Email = customer.Email;
                existingCustomer.Address = customer.Address;

                if (profileImage is { Length: > 0 })
                {
                    var imagePath = await SaveImageAsync(profileImage, "customers");

                    if (imagePath == null)
                    {
                        ModelState.AddModelError(
                            "ImageUrl",
                            "Please choose a valid image file smaller than 5 MB.");

                        return View(customer);
                    }

                    DeleteStoredImage(existingCustomer.ImageUrl);
                    existingCustomer.ImageUrl = imagePath;
                }

                existingCustomer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(customer.CustomerId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.CustomerId == id && !c.IsArchived);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer =
                await _context.Customers.FindAsync(id);

            if (customer != null && !customer.IsArchived)
            {
                customer.IsArchived = true;
                customer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Archived()
        {
            var customers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.IsArchived)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            return View(customers);
        }

        public async Task<IActionResult> ArchivedDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.CustomerId == id && c.IsArchived);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var customer =
                await _context.Customers.FindAsync(id);

            if (customer != null && customer.IsArchived)
            {
                customer.IsArchived = false;
                customer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<string?> SaveImageAsync(
            IFormFile file,
            string folder)
        {
            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp",
                ".gif"
            };

            var extension = Path.GetExtension(file.FileName);

            if (file.Length > 5 * 1024 * 1024 ||
                !allowedExtensions.Contains(
                    extension,
                    StringComparer.OrdinalIgnoreCase) ||
                !file.ContentType.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var uploadsPath = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                folder);

            Directory.CreateDirectory(uploadsPath);

            var fileName =
                $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

            var physicalPath = Path.Combine(
                uploadsPath,
                fileName);

            await using var stream =
                new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);

            await file.CopyToAsync(stream);

            return $"/uploads/{folder}/{fileName}";
        }

        private void DeleteStoredImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith(
                    "/uploads/customers/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(imageUrl);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var physicalPath = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "customers",
                fileName);

            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers
                .Any(c => c.CustomerId == id);
        }
    }
}
