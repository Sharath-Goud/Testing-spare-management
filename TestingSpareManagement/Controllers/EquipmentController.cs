using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using TestingSpareManagement.Models;
using CommandType = System.Data.CommandType;
using ParameterDirection = System.Data.ParameterDirection;
using SqlDbType = System.Data.SqlDbType;

namespace TestingSpareManagement.Controllers
{
    public class EquipmentController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EquipmentController> _logger;

        public EquipmentController(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<EquipmentController> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }


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
            model.TestingStations = await GetTestingStations();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(EquipmentConsumableViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var (records, _) = await GetEquipments(1, 25);
                model.Equipments = records;
                model.TestingStations = await GetTestingStations();
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
                    model.TestingStations = await GetTestingStations();
                    return View("Index", model);
                }
            }

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("dbo.InsertEquipment", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = model.ProjectCode;
            command.Parameters.Add("@TestItemName", SqlDbType.NVarChar, 250).Value = model.TestItemName;
            command.Parameters.Add("@PartNumber", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(model.PartNumber)
                    ? DBNull.Value
                    : model.PartNumber.Trim();

            command.Parameters.Add("@BrandManufacturer", SqlDbType.NVarChar, 200).Value =
                string.IsNullOrWhiteSpace(model.BrandManufacturer)
                    ? DBNull.Value
                    : model.BrandManufacturer.Trim();
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

            // Send email only when stock reaches or falls below the threshold.
            if (model.AvailableQuantity <= model.AlertThresholdMin)
            {
                _logger.LogInformation(
                    "Low-stock condition detected for {ItemName}. Quantity={Quantity}, Threshold={Threshold}",
                    model.TestItemName,
                    model.AvailableQuantity,
                    model.AlertThresholdMin);

                bool emailSent = await SendLowStockEmailAsync(
                    model.ProjectCode,
                    model.TestItemName,
                    model.TargetStation,
                    model.StorageLocation,
                    model.AvailableQuantity,
                    model.AlertThresholdMin);

                if (!emailSent)
                {
                    _logger.LogError(
                        "LOW-STOCK EMAIL FAILED for {ItemName}. Check the SMTP exception in application logs.",
                        model.TestItemName);

                    TempData["ErrorMessage"] =
                        "Equipment saved, but the low-stock email failed. Check application logs.";
                }
                else
                {
                    _logger.LogInformation(
                        "Low-stock email accepted by the configured SMTP server for {ItemName}.",
                        model.TestItemName);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Email not sent because stock is above the threshold. Item={ItemName}, Quantity={Quantity}, Threshold={Threshold}",
                    model.TestItemName,
                    model.AvailableQuantity,
                    model.AlertThresholdMin);
            }

            if (TempData["ErrorMessage"] == null)
            {
                TempData["SuccessMessage"] = "Equipment consumable saved successfully!";
            }
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(EquipmentConsumableViewModel model, IFormFile? Photo)
        {
            string? photoPath = null;

            try
            {
                // Validate the submitted model
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] =
                        "Please check the entered values. Some fields are invalid.";

                    return RedirectToAction(nameof(Index));
                }

                // Save a new photo only when the user uploads one
                if (Photo != null && Photo.Length > 0)
                {
                    photoPath = await SavePhotoAsync(Photo);

                    if (photoPath == null)
                    {
                        TempData["ErrorMessage"] = "Unable to upload the photo.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                using (SqlCommand command = new SqlCommand("dbo.UpdateEquipment", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add("@Id", SqlDbType.Int).Value = model.Id;

                    command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50)
                        .Value = model.ProjectCode ?? string.Empty;

                    command.Parameters.Add("@TestItemName", SqlDbType.NVarChar, 250)
                        .Value = model.TestItemName ?? string.Empty;

                    command.Parameters.Add("@PartNumber", SqlDbType.NVarChar, 100)
                        .Value = string.IsNullOrWhiteSpace(model.PartNumber)
                            ? DBNull.Value
                            : model.PartNumber.Trim();

                    command.Parameters.Add("@BrandManufacturer", SqlDbType.NVarChar, 200)
                        .Value = string.IsNullOrWhiteSpace(model.BrandManufacturer)
                            ? DBNull.Value
                            : model.BrandManufacturer.Trim();

                    command.Parameters.Add("@AssignedEquipment", SqlDbType.NVarChar, 50)
                        .Value = string.IsNullOrWhiteSpace(model.TargetStation)
                            ? DBNull.Value
                            : model.TargetStation;

                    command.Parameters.Add("@StorageLocation", SqlDbType.NVarChar, 100)
                        .Value = string.IsNullOrWhiteSpace(model.StorageLocation)
                            ? DBNull.Value
                            : model.StorageLocation;

                    command.Parameters.Add("@AvailableQuantity", SqlDbType.Int)
                        .Value = model.AvailableQuantity;

                    command.Parameters.Add("@AlertThreshold", SqlDbType.Int)
                        .Value = model.AlertThresholdMin;

                    SqlParameter priceParameter =
                        command.Parameters.Add("@UnitPriceINR", SqlDbType.Decimal);

                    priceParameter.Precision = 18;
                    priceParameter.Scale = 2;
                    priceParameter.Value = model.UnitPriceINR;

                    command.Parameters.Add("@SpecCycles", SqlDbType.Int)
                        .Value = model.SpecCyclesLimit.HasValue
                            ? model.SpecCyclesLimit.Value
                            : DBNull.Value;

                    command.Parameters.Add("@PhotoPath", SqlDbType.NVarChar, 500)
                        .Value = photoPath == null
                            ? DBNull.Value
                            : photoPath;

                    command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100)
                        .Value = HttpContext.Session.GetString("EmployeeName") ?? "System";

                    await connection.OpenAsync();

                    await command.ExecuteNonQueryAsync();
                }

                if (model.AvailableQuantity <= model.AlertThresholdMin)
                {
                    _logger.LogInformation(
                        "Low-stock condition detected during update. Item={ItemName}, Quantity={Quantity}, Threshold={Threshold}",
                        model.TestItemName,
                        model.AvailableQuantity,
                        model.AlertThresholdMin);

                    bool emailSent = await SendLowStockEmailAsync(
                        model.ProjectCode,
                        model.TestItemName,
                        model.TargetStation,
                        model.StorageLocation,
                        model.AvailableQuantity,
                        model.AlertThresholdMin);

                    if (!emailSent)
                    {
                        _logger.LogError(
                            "LOW-STOCK EMAIL FAILED after updating {ItemName}.",
                            model.TestItemName);

                        TempData["ErrorMessage"] =
                            "Equipment updated, but the low-stock email failed. Check application logs.";
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Low-stock email accepted by SMTP for updated item {ItemName}.",
                            model.TestItemName);
                    }
                }

                if (TempData["ErrorMessage"] == null)
                {
                    TempData["SuccessMessage"] = "Equipment updated successfully!";
                }
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = $"Database error: {ex.Message}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating equipment: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }


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
                partNumber = item.PartNumber,
                brandManufacturer = item.BrandManufacturer,
                assignedEquipment = item.AssignedEquipment,
                storageLocation = item.StorageLocation,
                availableQuantity = item.AvailableQuantity,
                alertThreshold = item.AlertThreshold,
                unitPriceINR = item.UnitPriceINR,
                specCycles = item.SpecCycles,
                photoPath = item.PhotoPath
            });
        }


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


        private EquipmentItem MapEquipment(SqlDataReader reader)
        {
            return new EquipmentItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                TestItemName = reader["TestItemName"]?.ToString() ?? "",
                PartNumber = reader["PartNumber"] == DBNull.Value
                    ? null
                    : reader["PartNumber"].ToString(),

                                BrandManufacturer = reader["BrandManufacturer"] == DBNull.Value
                    ? null
                    : reader["BrandManufacturer"].ToString(),
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


        private string GetConnectionString()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection was not found.");
            }

            return connectionString;
        }


        [HttpGet]
        public async Task<IActionResult> PurchaseHistory(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid equipment ID.");

            try
            {
                // Get the selected equipment's actual project and item name.
                var equipment = await GetEquipmentById(id);

                if (equipment == null)
                    return NotFound("Equipment item not found.");

                var history = new List<object>();
                string connectionString = GetConnectionString();

                // Retrieve matching procurement history by year.
                // Uses the existing PR/PO report stored procedure.
                for (int year = 2000; year <= DateTime.Today.Year; year++)
                {
                    using SqlConnection connection =
                        new SqlConnection(connectionString);

                    using SqlCommand command =
                        new SqlCommand("dbo.GetPrPoReport", connection);

                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add("@Year", SqlDbType.Int).Value = year;
                    command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = 1;
                    command.Parameters.Add("@PageSize", SqlDbType.Int).Value = 10000;

                    SqlParameter totalParam =
                        command.Parameters.Add("@TotalRecords", SqlDbType.Int);

                    totalParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();

                    using SqlDataReader reader =
                        await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        string projectCode =
                            reader["ProjectCode"]?.ToString()?.Trim() ?? "";

                        string assetTarget =
                            reader["AssetTarget"]?.ToString()?.Trim() ?? "";

                        // Both project and test item must match.
                        if (!string.Equals(
                                projectCode,
                                equipment.ProjectCode?.Trim(),
                                StringComparison.OrdinalIgnoreCase)
                            ||
                            !string.Equals(
                                assetTarget,
                                equipment.TestItemName?.Trim(),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        DateTime requisitionDate =
                            Convert.ToDateTime(reader["RequisitionDate"]);

                        decimal totalCost =
                            Convert.ToDecimal(reader["TotalCost"]);

                        history.Add(new
                        {
                            date = requisitionDate.ToString("yyyy-MM-dd"),
                            prNo = reader["PrNumber"]?.ToString() ?? "",
                            quantity = Convert.ToInt32(reader["Quantity"]),
                            totalCost = "₹" + totalCost.ToString("N2"),
                            status = reader["PoStatus"]?.ToString() ?? ""
                        });
                    }
                }

                return Json(history);
            }
            catch (Exception ex)
            {
                // The actual exception will be available in the application log.
                Console.Error.WriteLine(
                    $"PurchaseHistory failed for equipment ID {id}: {ex}");

                return StatusCode(
                    500,
                    new { message = "Unable to load purchase history." });
            }
        }

        private async Task<List<string>> GetTestingStations()
        {
            var stations = new List<string>();

            using SqlConnection connection =
                new SqlConnection(GetConnectionString());

            const string query = @"
        SELECT StationCode
        FROM dbo.TestingStation
        WHERE IsActive = 1
        ORDER BY StationCode;";

            using SqlCommand command = new SqlCommand(query, connection);

            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string station = reader["StationCode"]?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(station))
                    stations.Add(station);
            }

            return stations;
        }

        private async Task<bool> SendLowStockEmailAsync(
            string projectCode,
            string itemName,
            string station,
            string storageLocation,
            int availableQuantity,
            int alertThreshold)
        {

            _logger.LogInformation(
                "SendLowStockEmailAsync CALLED. Item={ItemName}, Available={AvailableQuantity}, Threshold={AlertThreshold}",
                itemName,
                availableQuantity,
                alertThreshold);

            // Send only when stock reaches or falls below the threshold.
            if (availableQuantity > alertThreshold)
            {
                _logger.LogInformation(
                    "Low-stock email not required for {ItemName}. " +
                    "Available quantity: {AvailableQuantity}; Threshold: {Threshold}",
                    itemName, availableQuantity, alertThreshold);

                return true;
            }

            try
            {
                string host = _configuration["SmtpSettings:Host"] ?? "";
                int port = _configuration.GetValue<int>("SmtpSettings:Port");
                bool enableSsl = _configuration.GetValue<bool>(
                    "SmtpSettings:EnableSsl");

                string fromEmail =
                    _configuration["SmtpSettings:SenderEmail"] ?? "";

                string senderName =
                    _configuration["SmtpSettings:SenderName"]
                    ?? "Testing Spare Management System";

                string username =
                    _configuration["SmtpSettings:Username"] ?? "";

                string password =
                    _configuration["SmtpSettings:Password"] ?? "";

                string[] recipients =
                    _configuration
                        .GetSection("SmtpSettings:Recipients")
                        .Get<string[]>()
                    ?? Array.Empty<string>();

                if (string.IsNullOrWhiteSpace(host) ||
                    string.IsNullOrWhiteSpace(fromEmail) ||
                    port <= 0)
                {
                    _logger.LogError(
                        "Low-stock email failed: SMTP configuration is incomplete.");

                    return false;
                }

                using var mail = new MailMessage();

                mail.From = new MailAddress(fromEmail, senderName);

                foreach (string recipient in recipients)
                {
                    if (!string.IsNullOrWhiteSpace(recipient))
                    {
                        mail.To.Add(recipient.Trim());
                    }
                }

                if (mail.To.Count == 0)
                {
                    _logger.LogError(
                    "Low-stock email failed: No valid recipients configured.");

                    return false;

                }

                mail.Subject = $"LOW STOCK ALERT - {itemName}";
                mail.IsBodyHtml = true;

                mail.Body = $@"
                    <html>
                    <body style='font-family:Arial,sans-serif;color:#333;'>
                        <div style='max-width:650px;margin:auto;
                                    border:1px solid #ddd;border-radius:8px;padding:20px;'>

                            <h2 style='color:#dc2626;'>LOW STOCK ALERT</h2>

                            <p>Available stock has reached or fallen below
                               the configured alert threshold.</p>

                            <table style='width:100%;border-collapse:collapse;'>
                                <tr>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        Project Code
                                    </td>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        {WebUtility.HtmlEncode(projectCode)}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        Spare Item
                                    </td>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        {WebUtility.HtmlEncode(itemName)}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        Testing Station
                                    </td>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        {WebUtility.HtmlEncode(station)}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        Storage Location
                                    </td>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        {WebUtility.HtmlEncode(storageLocation)}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        Available Quantity
                                    </td>
                                    <td style='padding:8px;border:1px solid #ddd;
                                               color:#dc2626;font-weight:bold;'>
                                        {availableQuantity}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        Alert Threshold
                                    </td>
                                    <td style='padding:8px;border:1px solid #ddd;'>
                                        {alertThreshold}
                                    </td>
                                </tr>
                            </table>

                            <p>Please review the stock and arrange replenishment.</p>

                            <p style='font-size:12px;color:#777;'>
                                Automated notification from TestingSpareManagement.
                            </p>
                        </div>
                    </body>
                    </html>";

                using var smtp = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 20000
                };

                // Use these credentials when your SMTP server requires authentication.
                if (!string.IsNullOrWhiteSpace(username))
                {
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(username, password);
                }

                await smtp.SendMailAsync(mail);

                _logger.LogInformation(
                    "Low-stock email accepted by SMTP for {ItemName}. " +
                    "Available quantity: {AvailableQuantity}; Threshold: {Threshold}; " +
                    "Recipients: {Recipients}",
                    itemName,
                    availableQuantity,
                    alertThreshold,
                    string.Join(", ", recipients));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Low-stock email failed for item {ItemName}.",
                    itemName);

                return false;
            }
        }
    }
}