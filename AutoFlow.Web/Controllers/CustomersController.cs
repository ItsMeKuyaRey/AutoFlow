using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoFlow.Web.Data;
using AutoFlow.Web.Models;

namespace AutoFlow.Web.Controllers
{
    public class CustomersController : Controller
    {
        private readonly AutoFlowDbContext _context;

        public CustomersController(AutoFlowDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // GET: Customers
        // ============================================================

        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                .OrderByDescending(c => c.CustomerId)
                .ToListAsync();

            return View(customers);
        }


        // ============================================================
        // GET: Customers/Details/5
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }


        // ============================================================
        // GET: Customers/Create
        // ============================================================

        public IActionResult Create()
        {
            return View();
        }


        // ============================================================
        // POST: Customers/Create
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("FirstName,LastName,Phone,Email,Address")]
            Customer customer)
        {
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            customer.CreatedAt = DateTime.UtcNow;
            customer.UpdatedAt = DateTime.UtcNow;

            _context.Customers.Add(customer);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // GET: Customers/Edit/5
        // ============================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer =
                await _context.Customers.FindAsync(id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }


        // ============================================================
        // POST: Customers/Edit/5
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("CustomerId,FirstName,LastName,Phone,Email,Address")]
            Customer customer)
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

                existingCustomer.FirstName =
                    customer.FirstName;

                existingCustomer.LastName =
                    customer.LastName;

                existingCustomer.Phone =
                    customer.Phone;

                existingCustomer.Email =
                    customer.Email;

                existingCustomer.Address =
                    customer.Address;

                existingCustomer.UpdatedAt =
                    DateTime.UtcNow;

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


        // ============================================================
        // GET: Customers/Delete/5
        // ============================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer =
                await _context.Customers
                    .FirstOrDefaultAsync(
                        c => c.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }


        // ============================================================
        // POST: Customers/Delete/5
        // ============================================================

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(
            int id)
        {
            var customer =
                await _context.Customers.FindAsync(id);

            if (customer != null)
            {
                _context.Customers.Remove(customer);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // CUSTOMER EXISTENCE CHECK
        // ============================================================

        private bool CustomerExists(int id)
        {
            return _context.Customers
                .Any(c => c.CustomerId == id);
        }
    }
}