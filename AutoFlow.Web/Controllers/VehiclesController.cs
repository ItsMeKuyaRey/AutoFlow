using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers
{
    public class VehiclesController : Controller
    {
        private readonly AutoFlowDbContext _context;

        public VehiclesController(AutoFlowDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vehicles = await _context.Vehicles
                .Include(v => v.Customer)
                .Where(v => !v.IsArchived)
                .OrderBy(v => v.Make)
                .ThenBy(v => v.Model)
                .ToListAsync();

            ViewData["CustomerId"] = new SelectList(
                _context.Customers.Where(c => !c.IsArchived),
                "CustomerId",
                "FirstName"
            );

            return View(vehicles);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
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

        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(
                _context.Customers.Where(c => !c.IsArchived),
                "CustomerId",
                "FirstName"
            );

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("CustomerId,PlateNumber,VIN,Make,Model,Year,Color,Mileage,ImageUrl")]
            Vehicle vehicle)
        {
            if (ModelState.IsValid)
            {
                vehicle.CreatedAt = DateTime.UtcNow;
                vehicle.UpdatedAt = DateTime.UtcNow;
                vehicle.IsArchived = false;

                _context.Add(vehicle);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewData["CustomerId"] = new SelectList(
                _context.Customers.Where(c => !c.IsArchived),
                "CustomerId",
                "FirstName",
                vehicle.CustomerId
            );

            return View(vehicle);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    !v.IsArchived);

            if (vehicle == null)
            {
                return NotFound();
            }

            ViewData["CustomerId"] = new SelectList(
                _context.Customers.Where(c => !c.IsArchived),
                "CustomerId",
                "FirstName",
                vehicle.CustomerId
            );

            return View(vehicle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("VehicleId,CustomerId,PlateNumber,VIN,Make,Model,Year,Color,Mileage,ImageUrl")]
            Vehicle vehicle)
        {
            if (id != vehicle.VehicleId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
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

                    existingVehicle.CustomerId = vehicle.CustomerId;
                    existingVehicle.PlateNumber = vehicle.PlateNumber;
                    existingVehicle.VIN = vehicle.VIN;
                    existingVehicle.Make = vehicle.Make;
                    existingVehicle.Model = vehicle.Model;
                    existingVehicle.Year = vehicle.Year;
                    existingVehicle.Color = vehicle.Color;
                    existingVehicle.Mileage = vehicle.Mileage;
                    existingVehicle.ImageUrl = vehicle.ImageUrl;
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

            ViewData["CustomerId"] = new SelectList(
                _context.Customers.Where(c => !c.IsArchived),
                "CustomerId",
                "FirstName",
                vehicle.CustomerId
            );

            return View(vehicle);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
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
                .Include(v => v.Customer)
                .Where(v => v.IsArchived)
                .OrderByDescending(v => v.UpdatedAt)
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

        private bool VehicleExists(int id)
        {
            return _context.Vehicles.Any(v => v.VehicleId == id);
        }
    }
}