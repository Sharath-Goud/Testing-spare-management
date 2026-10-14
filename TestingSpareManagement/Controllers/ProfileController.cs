using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TestingSpareManagement.Models;

using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;

namespace TestingSpareManagement.Controllers
{
    public class ProfileController : Controller
    {
        private readonly IConfiguration _configuration;

        public ProfileController(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        // =====================================================
        // GET: Profile/Index
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new ProfileSettingsViewModel
            {
                UserProfile = await GetUserProfile(),
                SystemUsers = await GetSystemUsers(),
                TestingStations = await GetTestingStations(),
                Projects = await GetProjects(),
                Buyers = await GetBuyers(),
                YearlyBudgets = await GetYearlyBudgets()
            };

            return View(model);
        }


        // =====================================================
        // POST: Profile/SaveUser
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUser(SystemUserFormModel form)
        {
            if (string.IsNullOrWhiteSpace(form.EmployeeId) ||
                string.IsNullOrWhiteSpace(form.FullName) ||
                string.IsNullOrWhiteSpace(form.OfficialEmail) ||
                string.IsNullOrWhiteSpace(form.AssignedRole))
            {
                TempData["ErrorMessage"] = "All fields are required.";
                return RedirectToAction(nameof(Index));
            }

            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.InsertSystemUser", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@EmployeeId", SqlDbType.NVarChar, 50).Value = form.EmployeeId;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = form.FullName;
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255).Value =
                (object?)form.Password ?? DBNull.Value;
            command.Parameters.Add("@OfficialEmail", SqlDbType.NVarChar, 100).Value = form.OfficialEmail;
            command.Parameters.Add("@AssignedRole", SqlDbType.NVarChar, 100).Value = form.AssignedRole;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "User saved successfully.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/DeleteUser
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (id <= 0) return BadRequest();

            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.DeleteSystemUser", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/SaveStation
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStation(string stationCode)
        {
            if (string.IsNullOrWhiteSpace(stationCode))
            {
                TempData["ErrorMessage"] = "Station code is required.";
                return RedirectToAction(nameof(Index));
            }

            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.InsertTestingStation", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@StationCode", SqlDbType.NVarChar, 50).Value = stationCode.Trim();

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Station saved.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/UpdateStation
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStation(int id, string stationCode)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(stationCode))
            {
                TempData["ErrorMessage"] = "Invalid station data.";
                return RedirectToAction(nameof(Index));
            }

            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.UpdateTestingStation", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@StationCode", SqlDbType.NVarChar, 50).Value = stationCode.Trim();

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Station updated successfully.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/DeleteStation
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStation(int id)
        {
            if (id <= 0) return BadRequest();
            await ExecuteSimpleDelete("dbo.DeleteTestingStation", id);
            TempData["SuccessMessage"] = "Station deleted.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/SaveProject
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveProject(string projectCode)
        {
            if (string.IsNullOrWhiteSpace(projectCode))
            {
                TempData["ErrorMessage"] = "Project code is required.";
                return RedirectToAction(nameof(Index));
            }

            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.InsertProject", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = projectCode.Trim();

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Project saved.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/UpdateProject
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProject(int id, string projectCode)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(projectCode))
            {
                TempData["ErrorMessage"] = "Invalid project data.";
                return RedirectToAction(nameof(Index));
            }

            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.UpdateProject", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = projectCode.Trim();

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Project updated successfully.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/DeleteProject
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProject(int id)
        {
            if (id <= 0) return BadRequest();
            await ExecuteSimpleDelete("dbo.DeleteProject", id);
            TempData["SuccessMessage"] = "Project deleted.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/SaveBuyer
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBuyer(string buyerName)
        {
            if (string.IsNullOrWhiteSpace(buyerName))
            {
                TempData["ErrorMessage"] = "Buyer name is required.";
                return RedirectToAction(nameof(Index));
            }

            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.InsertBuyer", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@BuyerName", SqlDbType.NVarChar, 100).Value = buyerName.Trim();

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Buyer saved.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/UpdateBuyer
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBuyer(int id, string buyerName)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(buyerName))
            {
                TempData["ErrorMessage"] = "Invalid buyer data.";
                return RedirectToAction(nameof(Index));
            }

            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.UpdateBuyer", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@BuyerName", SqlDbType.NVarChar, 100).Value = buyerName.Trim();

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Buyer updated successfully.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/DeleteBuyer
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBuyer(int id)
        {
            if (id <= 0) return BadRequest();
            await ExecuteSimpleDelete("dbo.DeleteBuyer", id);
            TempData["SuccessMessage"] = "Buyer deleted.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/SaveYearlyBudget (Single)
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveYearlyBudget(int year, decimal allocatedBudget, decimal budgetUtilized, decimal costSaved)
        {
            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.UpsertYearlyBudget", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Year", SqlDbType.Int).Value = year;

            var alloc = command.Parameters.Add("@AllocatedBudget", SqlDbType.Decimal);
            alloc.Precision = 18; alloc.Scale = 2; alloc.Value = allocatedBudget;

            var util = command.Parameters.Add("@BudgetUtilized", SqlDbType.Decimal);
            util.Precision = 18; util.Scale = 2; util.Value = budgetUtilized;

            var saved = command.Parameters.Add("@CostSaved", SqlDbType.Decimal);
            saved.Precision = 18; saved.Scale = 2; saved.Value = costSaved;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = $"Yearly budget saved for {year}.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/SaveYearlyBudgetsBulk
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveYearlyBudgetsBulk(List<YearlyBudgetModel> budgets)
        {
            if (budgets == null || !budgets.Any())
            {
                TempData["ErrorMessage"] = "No budget rows to save.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                string conn = GetConnectionString();
                using SqlConnection connection = new SqlConnection(conn);
                await connection.OpenAsync();

                int savedCount = 0;

                foreach (var b in budgets)
                {
                    if (b.Year <= 0) continue;

                    using SqlCommand command = new SqlCommand("dbo.UpsertYearlyBudget", connection);
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add("@Year", SqlDbType.Int).Value = b.Year;

                    var alloc = command.Parameters.Add("@AllocatedBudget", SqlDbType.Decimal);
                    alloc.Precision = 18; alloc.Scale = 2; alloc.Value = b.AllocatedBudget;

                    var util = command.Parameters.Add("@BudgetUtilized", SqlDbType.Decimal);
                    util.Precision = 18; util.Scale = 2; util.Value = b.BudgetUtilized;

                    var saved = command.Parameters.Add("@CostSaved", SqlDbType.Decimal);
                    saved.Precision = 18; saved.Scale = 2; saved.Value = b.CostSaved;

                    await command.ExecuteNonQueryAsync();
                    savedCount++;
                }

                TempData["SuccessMessage"] = $"✅ {savedCount} yearly budget row(s) saved successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ Failed to save: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/UpdateProfile
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(int id, string designation, string adminEmail, string clearanceTier, string themeColor)
        {
            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.UpdateUserProfile", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@Designation", SqlDbType.NVarChar, 100).Value = designation;
            command.Parameters.Add("@AdminEmail", SqlDbType.NVarChar, 100).Value = adminEmail;
            command.Parameters.Add("@ClearanceTier", SqlDbType.NVarChar, 50).Value = clearanceTier;
            command.Parameters.Add("@ThemeColor", SqlDbType.NVarChar, 50).Value = themeColor;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Profile updated.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Profile/UpdateAvatar
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(int id, IFormFile avatarFile)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid user.";
                return RedirectToAction(nameof(Index));
            }

            if (avatarFile == null || avatarFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select an image.";
                return RedirectToAction(nameof(Index));
            }

            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
            string extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["ErrorMessage"] = "Only JPG, JPEG, PNG, WEBP allowed.";
                return RedirectToAction(nameof(Index));
            }

            if (avatarFile.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Image must be under 5 MB.";
                return RedirectToAction(nameof(Index));
            }

            string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            string fileName = $"avatar_{id}_{Guid.NewGuid():N}{extension}";
            string filePath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(stream);
            }

            string avatarPath = "/uploads/avatars/" + fileName;

            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.UpdateUserAvatar", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@AvatarPath", SqlDbType.NVarChar, 500).Value = avatarPath;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Avatar uploaded successfully!";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // HELPERS
        // =====================================================
        private async Task ExecuteSimpleDelete(string spName, int id)
        {
            string conn = GetConnectionString();
            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand(spName, connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        private async Task<UserProfileModel> GetUserProfile()
        {
            var model = new UserProfileModel();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetUserProfile", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                model.Id = Convert.ToInt32(reader["Id"]);
                model.LoginId = reader["LoginId"]?.ToString() ?? "";
                model.Designation = reader["Designation"]?.ToString() ?? "";
                model.AdminEmail = reader["AdminEmail"]?.ToString() ?? "";
                model.ClearanceTier = reader["ClearanceTier"]?.ToString() ?? "";
                model.ThemeColor = reader["ThemeColor"]?.ToString() ?? "Dark Charcoal";
                model.AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString();
            }

            return model;
        }

        private async Task<List<SystemUserModel>> GetSystemUsers()
        {
            var list = new List<SystemUserModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetSystemUsers", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SystemUserModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    EmployeeId = reader["EmployeeId"]?.ToString() ?? "",
                    FullName = reader["FullName"]?.ToString() ?? "",
                    OfficialEmail = reader["OfficialEmail"]?.ToString() ?? "",
                    AssignedRole = reader["AssignedRole"]?.ToString() ?? "",
                    IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(reader["IsActive"])
                });
            }
            return list;
        }

        private async Task<List<TestingStationModel>> GetTestingStations()
        {
            var list = new List<TestingStationModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetTestingStations", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TestingStationModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    StationCode = reader["StationCode"]?.ToString() ?? "",
                    IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(reader["IsActive"])
                });
            }
            return list;
        }

        private async Task<List<ProjectMasterModel>> GetProjects()
        {
            var list = new List<ProjectMasterModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetProjects", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ProjectMasterModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                    IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(reader["IsActive"])
                });
            }
            return list;
        }

        private async Task<List<BuyerRosterModel>> GetBuyers()
        {
            var list = new List<BuyerRosterModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetBuyers", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new BuyerRosterModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    BuyerName = reader["BuyerName"]?.ToString() ?? "",
                    IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(reader["IsActive"])
                });
            }
            return list;
        }

        private async Task<List<YearlyBudgetModel>> GetYearlyBudgets()
        {
            var list = new List<YearlyBudgetModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetYearlyBudgets", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new YearlyBudgetModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Year = Convert.ToInt32(reader["Year"]),
                    AllocatedBudget = Convert.ToDecimal(reader["AllocatedBudget"]),
                    BudgetUtilized = Convert.ToDecimal(reader["BudgetUtilized"]),
                    CostSaved = Convert.ToDecimal(reader["CostSaved"])
                });
            }
            return list;
        }

        private string GetConnectionString()
        {
            string? cs = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("DefaultConnection was not found.");
            return cs;
        }
    }
}