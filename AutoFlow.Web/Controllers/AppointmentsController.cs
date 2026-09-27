using AutoFlow.Web.Data;
using AutoFlow.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Web.Controllers;

public class AppointmentsController : Controller
{
    private readonly AutoFlowDbContext _context;

    public AppointmentsController(AutoFlowDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(DateTime? weekStart)
    {
        var appointments = await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Vehicle)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();

        ViewBag.WeekStart = weekStart;
        return View(appointments);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(
                a => a.AppointmentId == id);

        if (appointment == null)
            return NotFound();

        if (IsAjaxRequest())
            return PartialView(appointment);

        return View(appointment);
    }

    public async Task<IActionResult> Create()
    {
        await LoadSelectionsAsync();

        if (IsAjaxRequest())
            return PartialView();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("CustomerId,VehicleId,AppointmentDate,Status,Reason,Notes")]
        Appointment appointment)
    {
        await ValidateCustomerVehicleAsync(
            appointment.CustomerId,
            appointment.VehicleId);

        if (ModelState.IsValid)
        {
            if (string.IsNullOrWhiteSpace(appointment.Status))
                appointment.Status = "Scheduled";

            appointment.CreatedAt = DateTime.UtcNow;
            appointment.UpdatedAt = DateTime.UtcNow;

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            if (IsAjaxRequest())
            {
                var savedAppointment =
                    await _context.Appointments
                        .AsNoTracking()
                        .Include(a => a.Customer)
                        .Include(a => a.Vehicle)
                        .FirstAsync(
                            a =>
                                a.AppointmentId ==
                                appointment.AppointmentId);

                var customerName =
                    savedAppointment.Customer == null
                        ? "Customer"
                        : $"{savedAppointment.Customer.FirstName} {savedAppointment.Customer.LastName}"
                            .Trim();

                var vehicleName =
                    savedAppointment.Vehicle == null
                        ? ""
                        : $"{savedAppointment.Vehicle.Make} {savedAppointment.Vehicle.Model}"
                            .Trim();

                if (
                    savedAppointment.Vehicle != null &&
                    !string.IsNullOrWhiteSpace(
                        savedAppointment.Vehicle.PlateNumber))
                {
                    vehicleName +=
                        $" ({savedAppointment.Vehicle.PlateNumber})";
                }

                return Json(
                    new
                    {
                        success = true,
                        appointment = new
                        {
                            id =
                                savedAppointment.AppointmentId,

                            date =
                                savedAppointment.AppointmentDate
                                    .ToString("yyyy-MM-dd"),

                            time =
                                savedAppointment.AppointmentDate
                                    .ToString("HH:mm"),

                            timeDisplay =
                                savedAppointment.AppointmentDate
                                    .ToString("h:mm tt"),

                            status =
                                savedAppointment.Status ??
                                "Scheduled",

                            customer = customerName,
                            vehicle = vehicleName,

                            reason =
                                savedAppointment.Reason ??
                                ""
                        }
                    });
            }

            var weekStart =
                StartOfWeek(
                    appointment.AppointmentDate);

            return RedirectToAction(
                nameof(Index),
                new
                {
                    weekStart =
                        weekStart.ToString("yyyy-MM-dd")
                });
        }

        await LoadSelectionsAsync(
            appointment.CustomerId,
            appointment.VehicleId);

        if (IsAjaxRequest())
            return PartialView(appointment);

        return View(appointment);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var appointment =
            await _context.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a =>
                        a.AppointmentId ==
                        id);

        if (appointment == null)
            return NotFound();

        await LoadSelectionsAsync(
            appointment.CustomerId,
            appointment.VehicleId);

        if (IsAjaxRequest())
            return PartialView(appointment);

        return View(appointment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("AppointmentId,CustomerId,VehicleId,AppointmentDate,Status,Reason,Notes")]
        Appointment appointment)
    {
        if (id != appointment.AppointmentId)
            return NotFound();

        await ValidateCustomerVehicleAsync(
            appointment.CustomerId,
            appointment.VehicleId);

        if (ModelState.IsValid)
        {
            var existingAppointment =
                await _context.Appointments
                    .FindAsync(id);

            if (existingAppointment == null)
                return NotFound();

            existingAppointment.CustomerId =
                appointment.CustomerId;

            existingAppointment.VehicleId =
                appointment.VehicleId;

            existingAppointment.AppointmentDate =
                appointment.AppointmentDate;

            existingAppointment.Status =
                string.IsNullOrWhiteSpace(
                    appointment.Status)
                    ? "Scheduled"
                    : appointment.Status;

            existingAppointment.Reason =
                appointment.Reason;

            existingAppointment.Notes =
                appointment.Notes;

            existingAppointment.UpdatedAt =
                DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AppointmentExists(
                    appointment.AppointmentId))
                {
                    return NotFound();
                }

                throw;
            }

            var weekStart =
                StartOfWeek(
                    existingAppointment.AppointmentDate);

            return RedirectToAction(
                nameof(Index),
                new
                {
                    weekStart =
                        weekStart.ToString("yyyy-MM-dd")
                });
        }

        await LoadSelectionsAsync(
            appointment.CustomerId,
            appointment.VehicleId);

        if (IsAjaxRequest())
            return PartialView(appointment);

        return View(appointment);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var appointment =
            await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                .FirstOrDefaultAsync(
                    a =>
                        a.AppointmentId ==
                        id);

        if (appointment == null)
            return NotFound();

        if (IsAjaxRequest())
            return PartialView(appointment);

        return View(appointment);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int id)
    {
        var appointment =
            await _context.Appointments
                .FindAsync(id);

        if (appointment != null)
        {
            _context.Appointments.Remove(
                appointment);

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(
            nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetVehiclesByCustomer(
        int customerId)
    {
        if (customerId <= 0)
            return Json(
                Array.Empty<object>());

        var customerExists =
            await _context.Customers
                .AsNoTracking()
                .AnyAsync(
                    c =>
                        c.CustomerId ==
                        customerId &&
                        !c.IsArchived);

        if (!customerExists)
            return Json(
                Array.Empty<object>());

        var vehicles =
            await _context.Vehicles
                .AsNoTracking()
                .Where(
                    v =>
                        v.CustomerId ==
                        customerId &&
                        !v.IsArchived)
                .OrderBy(v => v.PlateNumber)
                .Select(
                    v =>
                        new
                        {
                            vehicleId =
                                v.VehicleId,

                            customerId =
                                v.CustomerId,

                            imageUrl =
                                v.ImageUrl,

                            color =
                                v.Color,

                            year =
                                v.Year,

                            displayName =
                                v.PlateNumber +
                                " — " +
                                v.Make +
                                " " +
                                v.Model +
                                (
                                    v.Year != null
                                        ? " (" +
                                          v.Year +
                                          ")"
                                        : ""
                                )
                        })
                .ToListAsync();

        return Json(vehicles);
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomerProfile(int customerId)
    {
        if (customerId <= 0)
            return Json(new { imageUrl = (string?)null });

        var customer = await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new
            {
                c.CustomerId,
                name = (c.FirstName + " " + c.LastName).Trim(),
                imageUrl = c.ImageUrl
            })
            .FirstOrDefaultAsync();

        if (customer == null)
            return NotFound();

        return Json(customer);
    }

    private async Task LoadSelectionsAsync(
        int? selectedCustomerId = null,
        int? selectedVehicleId = null)
    {
        var customers =
            await _context.Customers
                .AsNoTracking()
                .Where(c => !c.IsArchived)
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .Select(
                    c =>
                        new
                        {
                            c.CustomerId,
                            DisplayName =
                                c.FirstName +
                                " " +
                                c.LastName
                        })
                .ToListAsync();

        var vehicles =
            await _context.Vehicles
                .AsNoTracking()
                .Where(
                    v =>
                        !v.IsArchived &&
                        v.Customer != null &&
                        !v.Customer.IsArchived)
                .OrderBy(v => v.PlateNumber)
                .Select(
                    v =>
                        new
                        {
                            v.VehicleId,
                            v.CustomerId,

                            DisplayName =
                                v.PlateNumber +
                                " — " +
                                v.Make +
                                " " +
                                v.Model +
                                (
                                    v.Year != null
                                        ? " (" +
                                          v.Year +
                                          ")"
                                        : ""
                                )
                        })
                .ToListAsync();

        ViewData["AppointmentVehicles"] =
            vehicles;

        ViewData["CustomerId"] =
            new SelectList(
                customers,
                "CustomerId",
                "DisplayName",
                selectedCustomerId);

        ViewData["VehicleId"] =
            new SelectList(
                vehicles.Where(
                    v =>
                        !selectedCustomerId.HasValue ||
                        v.CustomerId ==
                        selectedCustomerId.Value),
                "VehicleId",
                "DisplayName",
                selectedVehicleId);
    }

    private async Task ValidateCustomerVehicleAsync(
        int customerId,
        int vehicleId)
    {
        if (customerId <= 0)
        {
            ModelState.AddModelError(
                "CustomerId",
                "Customer is required.");

            return;
        }

        if (vehicleId <= 0)
        {
            ModelState.AddModelError(
                "VehicleId",
                "Vehicle is required.");

            return;
        }

        var validVehicle =
            await _context.Vehicles
                .AsNoTracking()
                .AnyAsync(
                    v =>
                        v.VehicleId ==
                        vehicleId &&
                        v.CustomerId ==
                        customerId &&
                        !v.IsArchived);

        if (!validVehicle)
        {
            ModelState.AddModelError(
                "VehicleId",
                "Select a vehicle associated with the selected customer.");
        }
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime StartOfWeek(
        DateTime date)
    {
        var diff =
            (
                7 +
                (
                    date.DayOfWeek -
                    DayOfWeek.Sunday
                )
            ) % 7;

        return date.Date.AddDays(-diff);
    }

    private bool AppointmentExists(int id)
    {
        return _context.Appointments
            .Any(
                a =>
                    a.AppointmentId ==
                    id);
    }
}