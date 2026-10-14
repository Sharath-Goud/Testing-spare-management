using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TestingSpareManagement.Models;
using TestingSpareManagement.ViewModels;

// Aliases to avoid SqlDbType conflict
using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;
using ParameterDirection = System.Data.ParameterDirection;

namespace TestingSpareManagement.Controllers
{
    public class MasterConnectorController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public MasterConnectorController(
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }


        // =========================================================
        // INDEX
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            int pageNumber = 1,
            int pageSize = 25)
        {
            if (pageNumber < 1)
                pageNumber = 1;

            int[] allowedPageSizes = { 10, 25, 50, 100 };

            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 25;

            var model = new MasterConnectorViewModel();

            model.CurrentPage = pageNumber;
            model.PageSize = pageSize;

            var result = await GetMasterConnectors(pageNumber, pageSize);

            model.Connectors = result.Connectors;
            model.TotalRecords = result.TotalRecords;

            if (model.TotalRecords > 0 && pageNumber > model.TotalPages)
            {
                return RedirectToAction(nameof(Index), new
                {
                    pageNumber = model.TotalPages,
                    pageSize = pageSize
                });
            }

            return View(model);
        }


        // =========================================================
        // SAVE NEW CONNECTOR
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(MasterConnectorViewModel model)
        {
            ValidateConnector(model);

            if (!ModelState.IsValid)
            {
                await LoadConnectorReport(model);
                return View("Index", model);
            }

            string? photoPath = null;

            if (model.PhotoFile != null && model.PhotoFile.Length > 0)
            {
                photoPath = await SavePhotoAsync(model.PhotoFile);

                if (!ModelState.IsValid)
                {
                    await LoadConnectorReport(model);
                    return View("Index", model);
                }
            }

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.InsertMasterConnector", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@StationProfile", SqlDbType.NVarChar, 100).Value =
                model.Connector.StationProfile.Trim();

            command.Parameters.Add("@ConnectorCodeType", SqlDbType.NVarChar, 100).Value =
                model.Connector.ConnectorCodeType.Trim();

            command.Parameters.Add("@DescriptionPartNo", SqlDbType.NVarChar, 250).Value =
                model.Connector.DescriptionPartNo.Trim();

            command.Parameters.Add("@PhotoPath", SqlDbType.NVarChar, 500).Value =
                (object?)photoPath ?? DBNull.Value;

            command.Parameters.Add("@LifeCycleLimit", SqlDbType.NVarChar, 50).Value =
                (object?)model.Connector.LifeCycleLimit ?? DBNull.Value;

            command.Parameters.Add("@BaseStock", SqlDbType.Int).Value =
                model.Connector.BaseStock;

            command.Parameters.Add("@AlertThreshold", SqlDbType.Int).Value =
                model.Connector.AlertThreshold;

            var inr = command.Parameters.Add("@UnitCostINR", SqlDbType.Decimal);
            inr.Precision = 18;
            inr.Scale = 2;
            inr.Value = model.Connector.UnitCostINR;

            var cny = command.Parameters.Add("@UnitCostCNY", SqlDbType.Decimal);
            cny.Precision = 18;
            cny.Scale = 2;
            cny.Value = (object?)model.Connector.UnitCostCNY ?? DBNull.Value;

            command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Connector saved successfully.";
            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // EDIT
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Edit(
            int id,
            int pageNumber = 1,
            int pageSize = 25)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            var connector = await GetMasterConnectorById(id);

            if (connector == null)
            {
                TempData["ErrorMessage"] = "Connector not found.";
                return RedirectToAction(nameof(Index));
            }

            var model = new MasterConnectorViewModel();

            model.Connector = connector;
            model.CurrentPage = pageNumber;
            model.PageSize = pageSize;

            await LoadConnectorReport(model);

            return View("Index", model);
        }


        // =========================================================
        // UPDATE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(MasterConnectorViewModel model)
        {
            ValidateConnector(model);

            if (model.Connector.Id <= 0)
            {
                ModelState.AddModelError("", "Invalid connector ID.");
            }

            if (!ModelState.IsValid)
            {
                await LoadConnectorReport(model);
                return View("Index", model);
            }

            string? photoPath = null;

            if (model.PhotoFile != null && model.PhotoFile.Length > 0)
            {
                photoPath = await SavePhotoAsync(model.PhotoFile);

                if (!ModelState.IsValid)
                {
                    await LoadConnectorReport(model);
                    return View("Index", model);
                }
            }

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.UpdateMasterConnector", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = model.Connector.Id;

            command.Parameters.Add("@StationProfile", SqlDbType.NVarChar, 100).Value =
                model.Connector.StationProfile.Trim();

            command.Parameters.Add("@ConnectorCodeType", SqlDbType.NVarChar, 100).Value =
                model.Connector.ConnectorCodeType.Trim();

            command.Parameters.Add("@DescriptionPartNo", SqlDbType.NVarChar, 250).Value =
                model.Connector.DescriptionPartNo.Trim();

            command.Parameters.Add("@PhotoPath", SqlDbType.NVarChar, 500).Value =
                (object?)photoPath ?? DBNull.Value;

            command.Parameters.Add("@LifeCycleLimit", SqlDbType.NVarChar, 50).Value =
                (object?)model.Connector.LifeCycleLimit ?? DBNull.Value;

            command.Parameters.Add("@BaseStock", SqlDbType.Int).Value =
                model.Connector.BaseStock;

            command.Parameters.Add("@AlertThreshold", SqlDbType.Int).Value =
                model.Connector.AlertThreshold;

            var inr = command.Parameters.Add("@UnitCostINR", SqlDbType.Decimal);
            inr.Precision = 18;
            inr.Scale = 2;
            inr.Value = model.Connector.UnitCostINR;

            var cny = command.Parameters.Add("@UnitCostCNY", SqlDbType.Decimal);
            cny.Precision = 18;
            cny.Scale = 2;
            cny.Value = (object?)model.Connector.UnitCostCNY ?? DBNull.Value;

            command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Connector updated successfully.";
            return RedirectToAction(nameof(Index), new
            {
                pageNumber = model.CurrentPage,
                pageSize = model.PageSize
            });
        }


        // =========================================================
        // DELETE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.DeleteMasterConnector", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Connector deleted successfully.";
            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // GET PAGINATED MASTER CONNECTORS
        // =========================================================
        private async Task<(List<MasterConnector> Connectors, int TotalRecords)>
            GetMasterConnectors(int pageNumber, int pageSize)
        {
            var list = new List<MasterConnector>();
            int totalRecords = 0;

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.GetMasterConnectorReport", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            SqlParameter totalRecordsParameter = command.Parameters.Add("@TotalRecords", SqlDbType.Int);
            totalRecordsParameter.Direction = ParameterDirection.Output;

            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(MapConnector(reader));
            }

            await reader.CloseAsync();

            if (totalRecordsParameter.Value != DBNull.Value)
            {
                totalRecords = Convert.ToInt32(totalRecordsParameter.Value);
            }

            return (list, totalRecords);
        }


        // =========================================================
        // GET BY ID
        // =========================================================
        private async Task<MasterConnector?> GetMasterConnectorById(int id)
        {
            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.GetMasterConnectorById", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return MapConnector(reader);
            }

            return null;
        }


        // =========================================================
        // MAP SQL DATA
        // =========================================================
        private MasterConnector MapConnector(SqlDataReader reader)
        {
            return new MasterConnector
            {
                Id = Convert.ToInt32(reader["Id"]),

                StationProfile = reader["StationProfile"]?.ToString() ?? "",

                ConnectorCodeType = reader["ConnectorCodeType"]?.ToString() ?? "",

                DescriptionPartNo = reader["DescriptionPartNo"]?.ToString() ?? "",

                PhotoPath = reader["PhotoPath"] == DBNull.Value
                    ? null
                    : reader["PhotoPath"].ToString(),

                LifeCycleLimit = reader["LifeCycleLimit"] == DBNull.Value
                    ? null
                    : reader["LifeCycleLimit"].ToString(),

                BaseStock = Convert.ToInt32(reader["BaseStock"]),

                AlertThreshold = Convert.ToInt32(reader["AlertThreshold"]),

                UnitCostINR = Convert.ToDecimal(reader["UnitCostINR"]),

                UnitCostCNY = reader["UnitCostCNY"] == DBNull.Value
                    ? null
                    : Convert.ToDecimal(reader["UnitCostCNY"]),

                StockStatus = reader["StockStatus"] == DBNull.Value
                    ? null
                    : reader["StockStatus"].ToString(),

                IsActive = reader["IsActive"] == DBNull.Value
                    ? true
                    : Convert.ToBoolean(reader["IsActive"]),

                CreatedBy = reader["CreatedBy"] == DBNull.Value
                    ? null
                    : reader["CreatedBy"].ToString(),

                CreatedDate = reader["CreatedDate"] == DBNull.Value
                    ? DateTime.MinValue
                    : Convert.ToDateTime(reader["CreatedDate"])
            };
        }


        // =========================================================
        // VALIDATION
        // =========================================================
        private void ValidateConnector(MasterConnectorViewModel model)
        {
            if (model.Connector == null)
            {
                ModelState.AddModelError("", "Connector information is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(model.Connector.StationProfile))
                ModelState.AddModelError("Connector.StationProfile", "Testing Station Profile is required.");

            if (string.IsNullOrWhiteSpace(model.Connector.ConnectorCodeType))
                ModelState.AddModelError("Connector.ConnectorCodeType", "Connector Code Type is required.");

            if (string.IsNullOrWhiteSpace(model.Connector.DescriptionPartNo))
                ModelState.AddModelError("Connector.DescriptionPartNo", "Description / Part No is required.");

            if (model.Connector.BaseStock < 0)
                ModelState.AddModelError("Connector.BaseStock", "Base stock cannot be negative.");

            if (model.Connector.AlertThreshold < 0)
                ModelState.AddModelError("Connector.AlertThreshold", "Alert threshold cannot be negative.");

            if (model.Connector.UnitCostINR < 0)
                ModelState.AddModelError("Connector.UnitCostINR", "Unit cost cannot be negative.");

            if (model.Connector.UnitCostCNY.HasValue && model.Connector.UnitCostCNY.Value < 0)
                ModelState.AddModelError("Connector.UnitCostCNY", "CNY cost cannot be negative.");
        }


        // =========================================================
        // PHOTO
        // =========================================================
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

            string uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "connectors");

            Directory.CreateDirectory(uploadFolder);

            string fileName = $"{Guid.NewGuid():N}{extension}";

            string filePath = Path.Combine(uploadFolder, fileName);

            await using FileStream stream = new FileStream(filePath, FileMode.CreateNew);

            await photoFile.CopyToAsync(stream);

            return $"/uploads/connectors/{fileName}";
        }


        // =========================================================
        // CONNECTION STRING
        // =========================================================
        private string GetConnectionString()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection was not found.");
            }

            return connectionString;
        }


        // =========================================================
        // LOAD PAGINATED REPORT INTO VIEWMODEL
        // =========================================================
        private async Task LoadConnectorReport(MasterConnectorViewModel model)
        {
            int pageNumber = model.CurrentPage < 1 ? 1 : model.CurrentPage;

            int[] allowedPageSizes = { 10, 25, 50, 100 };

            int pageSize = allowedPageSizes.Contains(model.PageSize)
                ? model.PageSize
                : 25;

            model.CurrentPage = pageNumber;
            model.PageSize = pageSize;

            var result = await GetMasterConnectors(pageNumber, pageSize);

            model.Connectors = result.Connectors;
            model.TotalRecords = result.TotalRecords;

            if (model.TotalRecords > 0 && model.CurrentPage > model.TotalPages)
            {
                model.CurrentPage = model.TotalPages;

                result = await GetMasterConnectors(model.CurrentPage, model.PageSize);

                model.Connectors = result.Connectors;
                model.TotalRecords = result.TotalRecords;
            }
        }
    }
}