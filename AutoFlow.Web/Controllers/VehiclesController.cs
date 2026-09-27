using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Services;
using AutoFlow.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AutoFlow.Web.Controllers
{
    public class VehiclesController : Controller
    {
        private readonly AutoFlowDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly SupabaseStorageService _supabaseStorage;

        public VehiclesController(
            AutoFlowDbContext context,
            IWebHostEnvironment environment,
            SupabaseStorageService supabaseStorage)
        {
            _context = context;
            _environment = environment;
            _supabaseStorage = supabaseStorage;
        }

        public async Task<IActionResult> Index()
        {
            var vehicles = await _context.Vehicles
                .AsNoTracking()
                .Include(v => v.Customer)
                .OrderBy(v => v.IsArchived)
                .ThenBy(v => v.Make)
                .ThenBy(v => v.Model)
                .ThenBy(v => v.VehicleId)
                .ToListAsync();

            ViewData["CustomerId"] = BuildCustomerSelectList();

            return View(vehicles);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
                .AsNoTracking()
                .Include(v => v.Customer)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id);

            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        public IActionResult Create(int? customerId = null)
        {
            ViewData["CustomerId"] = BuildCustomerSelectList(customerId);
            ViewBag.CustomerProfilesJson = BuildCustomerProfilesJson(customerId);
            return View(new Vehicle { CustomerId = customerId ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("CustomerId,PlateNumber,VIN,Make,Model,Year,Color,Mileage,ImageUrl")]
            Vehicle vehicle,
            IFormFile? profileImage)
        {
            if (!ModelState.IsValid)
            {
                ViewData["CustomerId"] = BuildCustomerSelectList(vehicle.CustomerId);
                ViewBag.CustomerProfilesJson = BuildCustomerProfilesJson(vehicle.CustomerId);
                return View(vehicle);
            }

            var customerIsActive = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.CustomerId == vehicle.CustomerId && !c.IsArchived);

            if (!customerIsActive)
            {
                ModelState.AddModelError(nameof(Vehicle.CustomerId), "Select an active customer.");
                ViewData["CustomerId"] = BuildCustomerSelectList(vehicle.CustomerId);
                ViewBag.CustomerProfilesJson = BuildCustomerProfilesJson(vehicle.CustomerId);
                return View(vehicle);
            }

            if (profileImage is { Length: > 0 })
            {
                var imagePath = await SaveImageAsync(profileImage);
                if (imagePath == null)
                {
                    ModelState.AddModelError(nameof(Vehicle.ImageUrl), "Please choose a valid image file smaller than 5 MB.");
                    ViewData["CustomerId"] = BuildCustomerSelectList(vehicle.CustomerId);
                    ViewBag.CustomerProfilesJson = BuildCustomerProfilesJson(vehicle.CustomerId);
                    return View(vehicle);
                }
                vehicle.ImageUrl = imagePath;
            }

            vehicle.CreatedAt = DateTime.UtcNow;
            vehicle.UpdatedAt = DateTime.UtcNow;
            vehicle.IsArchived = false;

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    !v.IsArchived);

            if (vehicle == null)
            {
                return NotFound();
            }

            ViewData["CustomerId"] = BuildCustomerSelectList(vehicle.CustomerId);
            ViewBag.CustomerProfilesJson = BuildCustomerProfilesJson(vehicle.CustomerId);
            return View(vehicle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("VehicleId,CustomerId,PlateNumber,VIN,Make,Model,Year,Color,Mileage,ImageUrl")]
            Vehicle vehicle,
            IFormFile? profileImage)
        {
            if (id != vehicle.VehicleId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewData["CustomerId"] = BuildCustomerSelectList(vehicle.CustomerId);
                ViewBag.CustomerProfilesJson = BuildCustomerProfilesJson(vehicle.CustomerId);
                return View(vehicle);
            }

            try
            {
                var existingVehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v =>
                        v.VehicleId == id &&
                        !v.IsArchived);

                if (existingVehicle == null)
                {
                    return NotFound();
                }

                var targetCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerId == vehicle.CustomerId);

                var keepingExistingCustomer = existingVehicle.CustomerId == vehicle.CustomerId;
                if (targetCustomer == null || (targetCustomer.IsArchived && !keepingExistingCustomer))
                {
                    ModelState.AddModelError(nameof(Vehicle.CustomerId), "Select an active customer.");
                    ViewData["CustomerId"] = BuildCustomerSelectList(vehicle.CustomerId);
                    ViewBag.CustomerProfilesJson = BuildCustomerProfilesJson(vehicle.CustomerId);
                    return View(vehicle);
                }

                existingVehicle.CustomerId = vehicle.CustomerId;
                existingVehicle.PlateNumber = vehicle.PlateNumber;
                existingVehicle.VIN = vehicle.VIN;
                existingVehicle.Make = vehicle.Make;
                existingVehicle.Model = vehicle.Model;
                existingVehicle.Year = vehicle.Year;
                existingVehicle.Color = vehicle.Color;
                existingVehicle.Mileage = vehicle.Mileage;

                if (profileImage is { Length: > 0 })
                {
                    var imagePath = await SaveImageAsync(profileImage);
                    if (imagePath == null)
                    {
                        ModelState.AddModelError(nameof(Vehicle.ImageUrl), "Please choose a valid image file smaller than 5 MB.");
                        ViewData["CustomerId"] = BuildCustomerSelectList(vehicle.CustomerId);
                        ViewBag.CustomerProfilesJson = BuildCustomerProfilesJson(vehicle.CustomerId);
                        return View(vehicle);
                    }
                    await DeleteStoredImage(existingVehicle.ImageUrl);
                    existingVehicle.ImageUrl = imagePath;
                }

                existingVehicle.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VehicleExists(vehicle.VehicleId))
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

            var vehicle = await _context.Vehicles
                .AsNoTracking()
                .Include(v => v.Customer)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    !v.IsArchived);

            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    !v.IsArchived);

            if (vehicle == null)
            {
                return NotFound();
            }

            vehicle.IsArchived = true;
            vehicle.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Archived()
        {
            var vehicles = await _context.Vehicles
                .AsNoTracking()
                .Include(v => v.Customer)
                .Where(v => v.IsArchived)
                .OrderByDescending(v => v.UpdatedAt)
                .ThenByDescending(v => v.VehicleId)
                .ToListAsync();

            return View(vehicles);
        }

        public async Task<IActionResult> ArchivedDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
                .AsNoTracking()
                .Include(v => v.Customer)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    v.IsArchived);

            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    v.IsArchived);

            if (vehicle == null)
            {
                return NotFound();
            }

            vehicle.IsArchived = false;
            vehicle.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private SelectList BuildCustomerSelectList(int? selectedCustomerId = null)
        {
            var customers = _context.Customers
                .AsNoTracking()
                .Where(c => !c.IsArchived || (selectedCustomerId.HasValue && c.CustomerId == selectedCustomerId.Value))
                .OrderBy(c => c.IsArchived)
                .ThenBy(c => c.FirstName)
                .ThenBy(c => c.LastName)
                .AsEnumerable()
                .Select(c => new
                {
                    c.CustomerId,
                    Name = $"{c.FirstName} {c.LastName}".Trim() + (c.IsArchived ? " · Archived" : "")
                })
                .ToList();

            return new SelectList(
                customers,
                "CustomerId",
                "Name",
                selectedCustomerId);
        }

        private string BuildCustomerProfilesJson(int? selectedCustomerId = null)
        {
            var customers = _context.Customers
                .AsNoTracking()
                .Where(c => !c.IsArchived || (selectedCustomerId.HasValue && c.CustomerId == selectedCustomerId.Value))
                .OrderBy(c => c.FirstName)
                .ThenBy(c => c.LastName)
                .Select(c => new
                {
                    id = c.CustomerId,
                    name = ($"{c.FirstName} {c.LastName}").Trim(),
                    phone = c.Phone ?? string.Empty,
                    email = c.Email ?? string.Empty,
                    address = c.Address ?? string.Empty,
                    imageUrl = c.ImageUrl ?? string.Empty,
                    isArchived = c.IsArchived
                })
                .ToList();

            return JsonSerializer.Serialize(customers);
        }

        private async Task<string?> SaveImageAsync(IFormFile file)
        {
            return await _supabaseStorage.UploadAsync(
                file,
                "vehicles");
        }

        private async Task DeleteStoredImage(string? imageUrl)
        {
            await _supabaseStorage.DeleteAsync(imageUrl);
        }

        private bool VehicleExists(int id)
        {
            return _context.Vehicles.Any(v => v.VehicleId == id);
        }
    }
}