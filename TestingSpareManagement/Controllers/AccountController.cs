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

            var connection = _context.Database.GetDbConnection();

            bool closeConnection =
                connection.State != System.Data.ConnectionState.Open;

            try
            {
                if (closeConnection)
                {
                    await connection.OpenAsync();
                }

                await using var command = connection.CreateCommand();

                command.CommandText = @"
            SELECT TOP (1)
                Id,
                EmployeeId,
                FullName,
                PasswordHash,
                [Role]
            FROM dbo.SystemUsers
            WHERE EmployeeId = @EmployeeId
              AND IsActive = 1;";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@EmployeeId";
                parameter.Value = employeeId;
                parameter.DbType = System.Data.DbType.String;
                parameter.Size = 50;
                command.Parameters.Add(parameter);

                int userId;
                string dbEmployeeId;
                string employeeName;
                string storedPassword;
                string role;

                await using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "Invalid Employee ID or Password.");

                        return View(model);
                    }

                    userId = Convert.ToInt32(reader["Id"]);
                    dbEmployeeId = reader["EmployeeId"].ToString() ?? "";
                    employeeName = reader["FullName"].ToString() ?? "";
                    storedPassword = reader["PasswordHash"].ToString() ?? "";
                    role = reader["Role"] == DBNull.Value
                        ? ""
                        : reader["Role"].ToString()?.Trim() ?? "";
                }

                // Existing database values appear to be plain-text passwords.
                // This comparison matches that current storage format.
                if (!string.Equals(
                        password, storedPassword, StringComparison.Ordinal))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Invalid Employee ID or Password.");

                    return View(model);
                }

                if (!string.Equals(role, "Admin",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(role, "Employee",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Your account does not have a valid role. Please contact the administrator.");

                    return View(model);
                }

                // Save authenticated user details in Session.
                HttpContext.Session.SetInt32("UserId", userId);
                HttpContext.Session.SetString("EmployeeId", dbEmployeeId);
                HttpContext.Session.SetString("EmployeeName", employeeName);
                HttpContext.Session.SetString("Role", role);

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to process login right now. Please try again.");

                return View(model);
            }
            finally
            {
                if (closeConnection &&
                    connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
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