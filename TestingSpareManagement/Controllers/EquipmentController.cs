using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TestingSpareManagement.Models;

using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;
using ParameterDirection = System.Data.ParameterDirection;

namespace TestingSpareManagement.Controllers
{
    public class EquipmentController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public EquipmentController(
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }


        // =====================================================
        // GET: Equipment/Index
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 25)
        {
            if (pageNumber < 1) pageNumber = 1;

            int[] allowedPageSizes = { 10, 25, 50, 100 };
            if (!allowedPageSizes.Contains(pageSize)) pageSize = 25;

            var model = new EquipmentConsumableViewModel
            {
                ProjectCode = "A246f"
            };

            var (records, totalRecords) = await GetEquipments(pageNumber, pageSize);
            model.Equipments = records;

            return View(model);
        }


        // =====================================================
        // POST: Equipment/Save
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(EquipmentConsumableViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var (records, _) = await GetEquipments(1, 25);
                model.Equipments = records;
                return View("Index", model);
            }

            string? photoPath = null;
            if (model.PhotoFile != null && model.PhotoFile.Length > 0)
            {
                photoPath = await SavePhotoAsync(model.PhotoFile);

                if (!ModelState.IsValid)
                {
                    var (records, _) = await GetEquipments(1, 25);
                    model.Equipments = records;
                    return View("Index", model);
                }
            }

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("dbo.InsertEquipment", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = model.ProjectCode;
            command.Parameters.Add("@TestItemName", SqlDbType.NVarChar, 250).Value = model.TestItemName;
            command.Parameters.Add("@AssignedEquipment", SqlDbType.NVarChar, 50).Value = model.TargetStation;
            command.Parameters.Add("@StorageLocation", SqlDbType.NVarChar, 100).Value = model.StorageLocation;
            command.Parameters.Add("@AvailableQuantity", SqlDbType.Int).Value = model.AvailableQuantity;
            command.Parameters.Add("@AlertThreshold", SqlDbType.Int).Value = model.AlertThresholdMin;

            var priceParam = command.Parameters.Add("@UnitPriceINR", SqlDbType.Decimal);
            priceParam.Precision = 18;
            priceParam.Scale = 2;
            priceParam.Value = model.UnitPriceINR;

            command.Parameters.Add("@SpecCycles", SqlDbType.Int).Value = model.SpecCyclesLimit ?? 0;
            command.Parameters.Add("@PhotoPath", SqlDbType.NVarChar, 500).Value = (object?)photoPath ?? DBNull.Value;
            command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Equipment consumable saved successfully!";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: Equipment/Update
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(EquipmentConsumableViewModel model)
        {
            if (model.Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid equipment ID.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.TestItemName) ||
                string.IsNullOrWhiteSpace(model.ProjectCode) ||
                string.IsNullOrWhiteSpace(model.StorageLocation))
            {
                TempData["ErrorMessage"] = "Required fields missing.";
                return RedirectToAction(nameof(Index));
            }

            // Photo upload (optional - only if new file uploaded)
            string? photoPath = null;
            if (model.PhotoFile != null && model.PhotoFile.Length > 0)
            {
                photoPath = await SavePhotoAsync(model.PhotoFile);
            }

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("dbo.UpdateEquipment", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = model.Id;
            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = model.ProjectCode;
            command.Parameters.Add("@TestItemName", SqlDbType.NVarChar, 250).Value = model.TestItemName;
            command.Parameters.Add("@AssignedEquipment", SqlDbType.NVarChar, 50).Value = model.TargetStation;
            command.Parameters.Add("@StorageLocation", SqlDbType.NVarChar, 100).Value = model.StorageLocation;
            command.Parameters.Add("@AvailableQuantity", SqlDbType.Int).Value = model.AvailableQuantity;
            command.Parameters.Add("@AlertThreshold", SqlDbType.Int).Value = model.AlertThresholdMin;

            var priceParam = command.Parameters.Add("@UnitPriceINR", SqlDbType.Decimal);
            priceParam.Precision = 18;
            priceParam.Scale = 2;
            priceParam.Value = model.UnitPriceINR;

            command.Parameters.Add("@SpecCycles", SqlDbType.Int).Value = model.SpecCyclesLimit ?? 0;
            command.Parameters.Add("@PhotoPath", SqlDbType.NVarChar, 500).Value =
                (object?)photoPath ?? DBNull.Value;

            command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Equipment updated successfully!";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // GET: Equipment/Edit/5  (returns JSON for modal)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0) return BadRequest();

            var item = await GetEquipmentById(id);
            if (item == null) return NotFound();

            return Json(new
            {
                id = item.Id,
                projectCode = item.ProjectCode,
                testItemName = item.TestItemName,
                assignedEquipment = item.AssignedEquipment,
                storageLocation = item.StorageLocation,
                availableQuantity = item.AvailableQuantity,
                alertThreshold = item.AlertThreshold,
                unitPriceINR = item.UnitPriceINR,
                specCycles = item.SpecCycles,
                photoPath = item.PhotoPath
            });
        }


        // =====================================================
        // POST: Equipment/Delete
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return BadRequest();

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("dbo.DeleteEquipment", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Equipment deleted successfully!";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // PRIVATE: GET BY ID
        // =====================================================
        private async Task<EquipmentItem?> GetEquipmentById(int id)
        {
            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("dbo.GetEquipmentById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return MapEquipment(reader);
            }

            return null;
        }


        // =====================================================
        // PRIVATE: GET PAGINATED
        // =====================================================
        private async Task<(List<EquipmentItem> Records, int TotalRecords)>
            GetEquipments(int pageNumber, int pageSize)
        {
            var list = new List<EquipmentItem>();
            int totalRecords = 0;

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("dbo.GetEquipmentReport", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            SqlParameter totalParam = command.Parameters.Add("@TotalRecords", SqlDbType.Int);
            totalParam.Direction = ParameterDirection.Output;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(MapEquipment(reader));
            }

            await reader.CloseAsync();

            if (totalParam.Value != DBNull.Value)
            {
                totalRecords = Convert.ToInt32(totalParam.Value);
            }

            return (list, totalRecords);
        }


        // =====================================================
        // PRIVATE: MAP SQL TO MODEL
        // =====================================================
        private EquipmentItem MapEquipment(SqlDataReader reader)
        {
            return new EquipmentItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                TestItemName = reader["TestItemName"]?.ToString() ?? "",
                AssignedEquipment = reader["AssignedEquipment"]?.ToString() ?? "",
                StorageLocation = reader["StorageLocation"]?.ToString() ?? "",
                AvailableQuantity = Convert.ToInt32(reader["AvailableQuantity"]),
                AlertThreshold = Convert.ToInt32(reader["AlertThreshold"]),
                UnitPriceINR = Convert.ToDecimal(reader["UnitPriceINR"]),
                UnitPriceRMB = reader["UnitPriceRMB"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["UnitPriceRMB"]),
                SpecCycles = Convert.ToInt32(reader["SpecCycles"]),
                PhotoPath = reader["PhotoPath"] == DBNull.Value ? null : reader["PhotoPath"].ToString(),
                IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(reader["IsActive"]),
                CreatedBy = reader["CreatedBy"] == DBNull.Value ? null : reader["CreatedBy"].ToString(),
                CreatedDate = reader["CreatedDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["CreatedDate"]),
                ModifiedBy = reader["ModifiedBy"] == DBNull.Value ? null : reader["ModifiedBy"].ToString(),
                ModifiedDate = reader["ModifiedDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["ModifiedDate"])
            };
        }


        // =====================================================
        // PRIVATE: PHOTO UPLOAD
        // =====================================================
        private async Task<string?> SavePhotoAsync(IFormFile photoFile)
        {
            const long maxSize = 5 * 1024 * 1024;

            if (photoFile.Length > maxSize)
            {
                ModelState.AddModelError("PhotoFile", "Image size cannot exceed 5 MB.");
                return null;
            }

            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
            string extension = Path.GetExtension(photoFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("PhotoFile", "Only JPG, JPEG, PNG and WEBP images are allowed.");
                return null;
            }

            string uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "equipment");
            Directory.CreateDirectory(uploadFolder);

            string fileName = $"{Guid.NewGuid():N}{extension}";
            string filePath = Path.Combine(uploadFolder, fileName);

            await using FileStream stream = new FileStream(filePath, FileMode.CreateNew);
            await photoFile.CopyToAsync(stream);

            return $"/uploads/equipment/{fileName}";
        }


        // =====================================================
        // PRIVATE: CONNECTION STRING
        // =====================================================
        private string GetConnectionString()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection was not found.");
            }

            return connectionString;
        }
    }
}