using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers
{
    public class AppointmentsController : Controller
    {
        private readonly AutoFlowDbContext _context;

        public AppointmentsController(AutoFlowDbContext context)
        {
            _context = context;
        }

        // GET: Appointments
        public async Task<IActionResult> Index()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Vehicle)
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync();

            return View(appointments);
        }

        // GET: Appointments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Vehicle)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // GET: Appointments/Create
        public IActionResult Create()
        {
            LoadVehicleDropdown();

            return View();
        }

        // POST: Appointments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("VehicleId,AppointmentDate,Status,CustomerConcern,Notes")]
            Appointment appointment)
        {
            if (ModelState.IsValid)
            {
                appointment.CreatedAt = DateTime.UtcNow;
                appointment.UpdatedAt = DateTime.UtcNow;

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            LoadVehicleDropdown(appointment.VehicleId);

            return View(appointment);
        }

        // GET: Appointments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments.FindAsync(id);

            if (appointment == null)
            {
                return NotFound();
            }

            LoadVehicleDropdown(appointment.VehicleId);

            return View(appointment);
        }

        // POST: Appointments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("AppointmentId,VehicleId,AppointmentDate,Status,CustomerConcern,Notes")]
            Appointment appointment)
        {
            if (id != appointment.AppointmentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingAppointment =
                        await _context.Appointments.FindAsync(id);

                    if (existingAppointment == null)
                    {
                        return NotFound();
                    }

                    existingAppointment.VehicleId = appointment.VehicleId;
                    existingAppointment.AppointmentDate = appointment.AppointmentDate;
                    existingAppointment.Status = appointment.Status;
                    existingAppointment.CustomerConcern = appointment.CustomerConcern;
                    existingAppointment.Notes = appointment.Notes;

                    existingAppointment.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppointmentExists(appointment.AppointmentId))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            LoadVehicleDropdown(appointment.VehicleId);

            return View(appointment);
        }

        // GET: Appointments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Vehicle)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // POST: Appointments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);

            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // Load vehicle dropdown
        private void LoadVehicleDropdown(int? selectedVehicleId = null)
        {
            ViewData["VehicleId"] = new SelectList(
                _context.Vehicles
                    .Where(v => !v.IsArchived)
                    .Select(v => new
                    {
                        v.VehicleId,
                        DisplayName =
                            v.PlateNumber + " — " +
                            v.Make + " " +
                            v.Model
                    })
                    .ToList(),
                "VehicleId",
                "DisplayName",
                selectedVehicleId
            );
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments
                .Any(a => a.AppointmentId == id);
        }
    }
}
