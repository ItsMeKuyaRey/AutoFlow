using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers
{
    public class ServiceRecordsController : Controller
    {
        private readonly AutoFlowDbContext _context;

        public ServiceRecordsController(AutoFlowDbContext context)
        {
            _context = context;
        }

        // GET: ServiceRecords
        public async Task<IActionResult> Index()
        {
            var serviceRecords = await _context.ServiceRecords
                .Include(s => s.Vehicle)
                .ToListAsync();

            return View(serviceRecords);
        }

        // GET: ServiceRecords/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var serviceRecord = await _context.ServiceRecords
                .Include(s => s.Vehicle)
                .FirstOrDefaultAsync(m => m.ServiceRecordId == id);

            if (serviceRecord == null)
            {
                return NotFound();
            }

            return View(serviceRecord);
        }

        // GET: ServiceRecords/Create
        public IActionResult Create()
        {
            LoadVehicles();

            return View();
        }

        // POST: ServiceRecords/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("VehicleId,ServiceDate,Mileage,Complaint,Diagnosis,Status,Notes")]
            ServiceRecord serviceRecord)
        {
            if (ModelState.IsValid)
            {
                serviceRecord.CreatedAt = DateTime.UtcNow;
                serviceRecord.UpdatedAt = DateTime.UtcNow;

                _context.ServiceRecords.Add(serviceRecord);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            LoadVehicles(serviceRecord.VehicleId);

            return View(serviceRecord);
        }

        // GET: ServiceRecords/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var serviceRecord = await _context.ServiceRecords
                .FindAsync(id);

            if (serviceRecord == null)
            {
                return NotFound();
            }

            LoadVehicles(serviceRecord.VehicleId);

            return View(serviceRecord);
        }

        // POST: ServiceRecords/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("ServiceRecordId,VehicleId,ServiceDate,Mileage,Complaint,Diagnosis,Status,Notes")]
            ServiceRecord serviceRecord)
        {
            if (id != serviceRecord.ServiceRecordId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingServiceRecord =
                        await _context.ServiceRecords.FindAsync(id);

                    if (existingServiceRecord == null)
                    {
                        return NotFound();
                    }

                    existingServiceRecord.VehicleId = serviceRecord.VehicleId;
                    existingServiceRecord.ServiceDate = serviceRecord.ServiceDate;
                    existingServiceRecord.Mileage = serviceRecord.Mileage;
                    existingServiceRecord.Complaint = serviceRecord.Complaint;
                    existingServiceRecord.Diagnosis = serviceRecord.Diagnosis;
                    existingServiceRecord.Status = serviceRecord.Status;
                    existingServiceRecord.Notes = serviceRecord.Notes;

                    existingServiceRecord.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServiceRecordExists(serviceRecord.ServiceRecordId))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            LoadVehicles(serviceRecord.VehicleId);

            return View(serviceRecord);
        }

        // GET: ServiceRecords/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var serviceRecord = await _context.ServiceRecords
                .Include(s => s.Vehicle)
                .FirstOrDefaultAsync(m => m.ServiceRecordId == id);

            if (serviceRecord == null)
            {
                return NotFound();
            }

            return View(serviceRecord);
        }

        // POST: ServiceRecords/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var serviceRecord =
                await _context.ServiceRecords.FindAsync(id);

            if (serviceRecord != null)
            {
                _context.ServiceRecords.Remove(serviceRecord);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // Helper: Load active vehicles for dropdown
        private void LoadVehicles(int? selectedVehicleId = null)
        {
            ViewData["VehicleId"] = new SelectList(
                _context.Vehicles
                    .Where(v => !v.IsArchived)
                    .Select(v => new
                    {
                        v.VehicleId,
                        DisplayName = v.PlateNumber
                            + " — "
                            + v.Make
                            + " "
                            + v.Model
                    })
                    .ToList(),
                "VehicleId",
                "DisplayName",
                selectedVehicleId
            );
        }

        // Check if service record exists
        private bool ServiceRecordExists(int id)
        {
            return _context.ServiceRecords
                .Any(e => e.ServiceRecordId == id);
        }
    }
}

