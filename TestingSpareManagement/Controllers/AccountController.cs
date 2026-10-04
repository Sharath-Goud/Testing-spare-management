using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestingSpareManagement.Data;
using TestingSpareManagement.Models;
using TestingSpareManagement.ViewModels;

namespace TestingSpareManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // REGISTER - GET
        // =========================================================

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // =========================================================
        // REGISTER - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string employeeId = model.EmployeeId.Trim();

            string employeeName = model.EmployeeName.Trim();

            // Check duplicate Employee ID
            bool employeeExists = await _context.Users
                .AnyAsync(x => x.EmployeeId == employeeId);

            if (employeeExists)
            {
                ModelState.AddModelError(
                    nameof(model.EmployeeId),
                    "This Employee ID is already registered."
                );

                return View(model);
            }

            // Create user
            var user = new User
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                Password = model.Password,
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Registration successful. Please login.";

            return RedirectToAction(nameof(Login));
        }

        // =========================================================
        // LOGIN - GET
        // =========================================================

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // =========================================================
        // LOGIN - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string employeeId = model.EmployeeId.Trim();

            string password = model.Password;

            var user = await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.EmployeeId == employeeId &&
                    x.Password == password &&
                    x.IsActive
                );

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid Employee ID or Password."
                );

                return View(model);
            }

            // Store login information in Session
            HttpContext.Session.SetInt32(
                "UserId",
                user.Id
            );

            HttpContext.Session.SetString(
                "EmployeeId",
                user.EmployeeId
            );

            HttpContext.Session.SetString(
                "EmployeeName",
                user.EmployeeName
            );

            return RedirectToAction(
                "Index",
                "Dashboard"
            );
        }

        // =========================================================
        // LOGOUT
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction(nameof(Login));
        }
    }
}