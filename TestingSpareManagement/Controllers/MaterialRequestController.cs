using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TestingSpareManagement.Models;

// Aliases to avoid SqlDbType conflict
using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;
using ParameterDirection = System.Data.ParameterDirection;

namespace TestingSpareManagement.Controllers
{
    public class MaterialRequestController : Controller
    {
        private readonly IConfiguration _configuration;

        public MaterialRequestController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // =====================================================
        // GET: MaterialRequest/Index
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            int pageNumber = 1,
            int pageSize = 25)
        {
            if (pageNumber < 1) pageNumber = 1;

            int[] allowedPageSizes = { 10, 25, 50, 100 };
            if (!allowedPageSizes.Contains(pageSize)) pageSize = 25;

            var model = new MaterialRequestViewModel
            {
                RequestDate = DateTime.Today,
                ProjectCode = "A246F",
                CurrentPage = pageNumber,
                PageSize = pageSize
            };

            // Load paginated records
            var (records, totalRecords) = await GetMaterialRequests(pageNumber, pageSize);
            model.Requests = records;
            model.TotalRecords = totalRecords;

            // If requested page doesn't exist, redirect to last valid page
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


        // =====================================================
        // POST: MaterialRequest/Submit
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(MaterialRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload data and return to view
                var (records, totalRecords) = await GetMaterialRequests(1, 25);
                model.Requests = records;
                model.TotalRecords = totalRecords;
                return View("Index", model);
            }

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.InsertMaterialRequest", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@RequestDate", SqlDbType.Date).Value = model.RequestDate;
            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = model.ProjectCode;

            string requestedBy = HttpContext.Session.GetString("EmployeeName") ?? "Unknown";
            command.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 100).Value = requestedBy;

            command.Parameters.Add("@SpareDescription", SqlDbType.NVarChar, 250).Value = model.SpareDescription;
            command.Parameters.Add("@PartNumber", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(model.PartNumber) ? DBNull.Value : model.PartNumber;
            command.Parameters.Add("@Brand", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(model.Brand) ? DBNull.Value : model.Brand;
            command.Parameters.Add("@TargetMachine", SqlDbType.NVarChar, 50).Value = model.TargetMachine;
            command.Parameters.Add("@RequestedQuantity", SqlDbType.Int).Value = model.RequestedQuantity;
            command.Parameters.Add("@PriorityLevel", SqlDbType.NVarChar, 50).Value = model.PriorityLevel;
            command.Parameters.Add("@Justification", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(model.Justification) ? DBNull.Value : model.Justification;
            command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = requestedBy;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Material request submitted successfully!";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // POST: MaterialRequest/Delete
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return BadRequest();

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.DeleteMaterialRequest", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Material request deleted successfully.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // PRIVATE: GET PAGINATED RECORDS
        // =====================================================
        private async Task<(List<MaterialRequestItem> Records, int TotalRecords)>
            GetMaterialRequests(int pageNumber, int pageSize)
        {
            var list = new List<MaterialRequestItem>();
            int totalRecords = 0;

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.GetMaterialRequestReport", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            SqlParameter totalParam = command.Parameters.Add("@TotalRecords", SqlDbType.Int);
            totalParam.Direction = ParameterDirection.Output;

            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(MapMaterialRequest(reader));
            }

            await reader.CloseAsync();

            if (totalParam.Value != DBNull.Value)
            {
                totalRecords = Convert.ToInt32(totalParam.Value);
            }

            return (list, totalRecords);
        }


        // =====================================================
        // PRIVATE: MAP SQL READER TO MODEL
        // =====================================================
        private MaterialRequestItem MapMaterialRequest(SqlDataReader reader)
        {
            return new MaterialRequestItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                ReqId = reader["ReqId"]?.ToString() ?? "",
                RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                RequestedBy = reader["RequestedBy"]?.ToString() ?? "",
                SpareDescription = reader["SpareDescription"]?.ToString() ?? "",
                PartNumber = reader["PartNumber"] == DBNull.Value ? "" : reader["PartNumber"].ToString() ?? "",
                Brand = reader["Brand"] == DBNull.Value ? "" : reader["Brand"].ToString() ?? "",
                TargetMachine = reader["TargetMachine"]?.ToString() ?? "",
                RequestedQuantity = Convert.ToInt32(reader["RequestedQuantity"]),
                PriorityLevel = reader["PriorityLevel"]?.ToString() ?? "",
                Justification = reader["Justification"] == DBNull.Value ? "" : reader["Justification"].ToString() ?? "",
                Status = reader["Status"]?.ToString() ?? "",
                AdminActions = reader["AdminActions"]?.ToString() ?? "",
                IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(reader["IsActive"]),
                CreatedBy = reader["CreatedBy"] == DBNull.Value ? null : reader["CreatedBy"].ToString(),
                CreatedDate = reader["CreatedDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["CreatedDate"]),
                ModifiedBy = reader["ModifiedBy"] == DBNull.Value ? null : reader["ModifiedBy"].ToString(),
                ModifiedDate = reader["ModifiedDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["ModifiedDate"])
            };
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