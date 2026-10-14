using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TestingSpareManagement.Models;

// Excel & PDF export packages
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// Aliases to avoid SqlDbType conflict
using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;
using ParameterDirection = System.Data.ParameterDirection;

namespace TestingSpareManagement.Controllers
{
    public class ConsumptionController : Controller
    {
        private readonly IConfiguration _configuration;
        private const decimal INR_TO_RMB = 0.0858m;

        public ConsumptionController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // =====================================================
        // GET: Consumption/DailyConsumption
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> DailyConsumption(
            int pageNumber = 1,
            int pageSize = 25,
            string? filterDate = null)
        {
            if (pageNumber < 1) pageNumber = 1;

            int[] allowedPageSizes = { 10, 25, 50, 100 };
            if (!allowedPageSizes.Contains(pageSize)) pageSize = 25;

            var model = new DailyConsumptionViewModel
            {
                SelectedDate = string.IsNullOrEmpty(filterDate)
                    ? DateTime.Today
                    : DateTime.Parse(filterDate),
                CurrentPage = pageNumber,
                PageSize = pageSize,
                FilterDate = filterDate
            };

            model.AvailableDates = await GetDistinctConsumptionDates();
            var (records, totalRecords) = await GetDailyConsumptions(pageNumber, pageSize, filterDate);
            model.Consumptions = records;
            model.TotalRecords = totalRecords;

            if (model.TotalRecords > 0 && pageNumber > model.TotalPages)
            {
                return RedirectToAction(nameof(DailyConsumption), new
                {
                    pageNumber = model.TotalPages,
                    pageSize = pageSize,
                    filterDate = filterDate
                });
            }

            // Load dropdown data
            model.ProjectCodes = await GetProjectCodes();
            model.EquipmentStations = await GetEquipmentStations();
            model.DriEngineers = await GetDriEngineers();
            model.LineTargets = await GetLineTargets();

            return View(model);
        }


        // =====================================================
        // POST: Consumption/LogUsage
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogUsage(DailyConsumptionViewModel model)
        {
            // ✅ Safety net — default shift if empty
            if (string.IsNullOrWhiteSpace(model.WorkingShift))
                model.WorkingShift = "Shift-A (Day Shift)";

            if (!ModelState.IsValid)
            {
                await ReloadViewModelData(model);
                return View("DailyConsumption", model);
            }

            string connectionString = GetConnectionString();
            using SqlConnection connection = new SqlConnection(connectionString);

            decimal? unitCostINR = await GetUnitCostFromMaster(model.SpareDetails, connectionString);

            using SqlCommand command = new SqlCommand("dbo.InsertDailyConsumption", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@ConsumptionDate", SqlDbType.Date).Value = model.SelectedDate;
            command.Parameters.Add("@WorkingShift", SqlDbType.NVarChar, 50).Value = model.WorkingShift;
            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = model.ProjectCode;
            command.Parameters.Add("@SpareDetails", SqlDbType.NVarChar, 100).Value = model.SpareDetails;
            command.Parameters.Add("@EquipmentStation", SqlDbType.NVarChar, 50).Value = model.EquipmentStation;
            command.Parameters.Add("@QuantityUsed", SqlDbType.Int).Value = model.QuantityUsed;
            command.Parameters.Add("@LineTarget", SqlDbType.NVarChar, 50).Value = model.LineTarget;
            command.Parameters.Add("@DriEngineer", SqlDbType.NVarChar, 100).Value = model.DriEngineer;

            command.Parameters.Add("@UnitCostINR", SqlDbType.Decimal).Value =
                unitCostINR.HasValue ? unitCostINR.Value : DBNull.Value;
            command.Parameters.Add("@UnitCostCNY", SqlDbType.Decimal).Value =
                unitCostINR.HasValue ? unitCostINR.Value * INR_TO_RMB : DBNull.Value;

            command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "✅ Usage entry logged successfully!";
            return RedirectToAction(nameof(DailyConsumption));
        }


        // =====================================================
        // POST: Consumption/Update
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, DailyConsumptionViewModel model)
        {
            // ✅ Safety net — default shift if empty
            if (string.IsNullOrWhiteSpace(model.WorkingShift))
                model.WorkingShift = "Shift-A (Day Shift)";

            if (id <= 0 || !ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "❌ Invalid data. Please check your inputs.";
                return RedirectToAction(nameof(DailyConsumption));
            }

            string connectionString = GetConnectionString();
            decimal? unitCostINR = await GetUnitCostFromMaster(model.SpareDetails, connectionString);

            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("dbo.UpdateDailyConsumption", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@ConsumptionDate", SqlDbType.Date).Value = model.SelectedDate;
            command.Parameters.Add("@WorkingShift", SqlDbType.NVarChar, 50).Value = model.WorkingShift;
            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = model.ProjectCode;
            command.Parameters.Add("@SpareDetails", SqlDbType.NVarChar, 100).Value = model.SpareDetails;
            command.Parameters.Add("@EquipmentStation", SqlDbType.NVarChar, 50).Value = model.EquipmentStation;
            command.Parameters.Add("@QuantityUsed", SqlDbType.Int).Value = model.QuantityUsed;
            command.Parameters.Add("@LineTarget", SqlDbType.NVarChar, 50).Value = model.LineTarget;
            command.Parameters.Add("@DriEngineer", SqlDbType.NVarChar, 100).Value = model.DriEngineer;
            command.Parameters.Add("@UnitCostINR", SqlDbType.Decimal).Value =
                unitCostINR.HasValue ? unitCostINR.Value : DBNull.Value;
            command.Parameters.Add("@UnitCostCNY", SqlDbType.Decimal).Value =
                unitCostINR.HasValue ? unitCostINR.Value * INR_TO_RMB : DBNull.Value;
            command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "✅ Usage entry updated successfully!";
            return RedirectToAction(nameof(DailyConsumption));
        }


        // =====================================================
        // POST: Consumption/Delete
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return BadRequest();

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);
            using SqlCommand command = new SqlCommand("dbo.DeleteDailyConsumption", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "✅ Usage entry deleted successfully.";
            return RedirectToAction(nameof(DailyConsumption));
        }


        // =====================================================
        // GET: Consumption/GetItemForEdit
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> GetItemForEdit(int id)
        {
            if (id <= 0) return BadRequest();

            var item = await GetConsumptionById(id);
            if (item == null) return NotFound();

            return Json(item);
        }


        // =====================================================
        // GET: Consumption/ExportExcel
        // ✅ Server-side Excel export using ClosedXML
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? filterDate = null)
        {
            // Fetch ALL records (not just current page)
            var (records, _) = await GetDailyConsumptions(1, 100000, filterDate);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Daily Consumption");

            // ===== Title =====
            ws.Cell(1, 1).Value = "TESTING SPARE MANAGEMENT SYSTEM";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#3B4CCA");
            ws.Range(1, 1, 1, 10).Merge();

            ws.Cell(2, 1).Value = "Daily Consumption Report — Industry 4.0 Smart Factory Automation Platform | Foxlink";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Cell(2, 1).Style.Font.FontSize = 10;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range(2, 1, 2, 10).Merge();

            ws.Cell(3, 1).Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}" +
                (string.IsNullOrEmpty(filterDate) ? "" : $" | Filtered Date: {filterDate}");
            ws.Cell(3, 1).Style.Font.FontSize = 9;
            ws.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range(3, 1, 3, 10).Merge();

            // ===== Header Row =====
            string[] headers = {
                "DATE", "SHIFT", "PROJECT", "ASSET KEY", "STATION",
                "CONSUMED QTY", "LINE", "DRI NAME", "EXPENSES (INR)", "EXPENSES (RMB)"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(5, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#8B5CF6");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ===== Data Rows =====
            int row = 6;
            foreach (var item in records)
            {
                ws.Cell(row, 1).Value = item.ConsumptionDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value = item.WorkingShift ?? "-";
                ws.Cell(row, 3).Value = item.ProjectCode ?? "-";
                ws.Cell(row, 4).Value = item.SpareDetails ?? "-";
                ws.Cell(row, 5).Value = item.EquipmentStation ?? "-";
                ws.Cell(row, 6).Value = item.QuantityUsed;
                ws.Cell(row, 7).Value = item.LineTarget ?? "-";
                ws.Cell(row, 8).Value = item.DriEngineer ?? "-";

                var inrCell = ws.Cell(row, 9);
                inrCell.Value = item.TotalCostINR ?? 0;
                inrCell.Style.NumberFormat.Format = "₹#,##0.00";
                inrCell.Style.Font.FontColor = XLColor.FromHtml("#DC2626");

                var rmbCell = ws.Cell(row, 10);
                rmbCell.Value = (item.TotalCostINR ?? 0) * INR_TO_RMB;
                rmbCell.Style.NumberFormat.Format = "¥#,##0.00";
                rmbCell.Style.Font.FontColor = XLColor.FromHtml("#D97706");

                // Zebra striping
                if (row % 2 == 0)
                {
                    ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");
                }

                ws.Range(row, 1, row, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                row++;
            }

            // ===== Summary Row =====
            row++;
            ws.Cell(row, 8).Value = "TOTAL:";
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 9).FormulaA1 = $"SUM(I6:I{row - 2})";
            ws.Cell(row, 9).Style.NumberFormat.Format = "₹#,##0.00";
            ws.Cell(row, 9).Style.Font.Bold = true;
            ws.Cell(row, 9).Style.Font.FontColor = XLColor.FromHtml("#DC2626");

            ws.Cell(row, 10).FormulaA1 = $"SUM(J6:J{row - 2})";
            ws.Cell(row, 10).Style.NumberFormat.Format = "¥#,##0.00";
            ws.Cell(row, 10).Style.Font.Bold = true;
            ws.Cell(row, 10).Style.Font.FontColor = XLColor.FromHtml("#D97706");

            // Auto-fit
            ws.Columns().AdjustToContents();

            // Return file
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"Daily_Consumption_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }


        // =====================================================
        // GET: Consumption/ExportPdf
        // ✅ Server-side PDF export using QuestPDF
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ExportPdf(string? filterDate = null)
        {
            // Fetch ALL records
            var (records, _) = await GetDailyConsumptions(1, 100000, filterDate);

            // QuestPDF License (Community is free for internal use)
            QuestPDF.Settings.License = LicenseType.Community;

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                    // ===== Header =====
                    page.Header().Column(col =>
                    {
                        col.Item().Background("#3B4CCA").Padding(10).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("TESTING SPARE MANAGEMENT SYSTEM")
                                    .FontSize(14).Bold().FontColor(Colors.White);
                                c.Item().Text("Industry 4.0 Smart Factory Automation Platform | Foxlink")
                                    .FontSize(8).Italic().FontColor(Colors.White);
                                c.Item().Text("Daily Consumption Report")
                                    .FontSize(10).Bold().FontColor(Colors.White);
                            });
                            row.ConstantItem(180).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                                    .FontSize(8).FontColor(Colors.White);
                                if (!string.IsNullOrEmpty(filterDate))
                                {
                                    c.Item().Text($"Filtered: {filterDate}")
                                        .FontSize(8).FontColor(Colors.White);
                                }
                                c.Item().Text($"Total Records: {records.Count}")
                                    .FontSize(8).FontColor(Colors.White);
                            });
                        });
                        col.Item().PaddingTop(8);
                    });

                    // ===== Content Table =====
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(60);   // Date
                            cols.RelativeColumn(1.5f); // Shift
                            cols.RelativeColumn(1);    // Project
                            cols.RelativeColumn(1.5f); // Asset
                            cols.RelativeColumn(1);    // Station
                            cols.RelativeColumn(0.8f); // Qty
                            cols.RelativeColumn(1);    // Line
                            cols.RelativeColumn(1.5f); // DRI
                            cols.RelativeColumn(1.2f); // INR
                            cols.RelativeColumn(1.2f); // RMB
                        });

                        // Header
                        table.Header(header =>
                        {
                            void HeaderCell(string text) =>
                                header.Cell().Background("#8B5CF6").Padding(4)
                                    .Text(text).Bold().FontColor(Colors.White).FontSize(8);

                            HeaderCell("DATE");
                            HeaderCell("SHIFT");
                            HeaderCell("PROJECT");
                            HeaderCell("ASSET KEY");
                            HeaderCell("STATION");
                            HeaderCell("QTY");
                            HeaderCell("LINE");
                            HeaderCell("DRI NAME");
                            HeaderCell("EXPENSES (₹)");
                            HeaderCell("EXPENSES (¥)");
                        });

                        // Data rows
                        int idx = 0;
                        foreach (var item in records)
                        {
                            idx++;
                            var bg = idx % 2 == 0 ? "#FAFAFA" : "#FFFFFF";

                            void DataCell(string text) =>
                                table.Cell().Background(bg).Padding(3).Text(text).FontSize(8);

                            DataCell(item.ConsumptionDate.ToString("yyyy-MM-dd"));
                            DataCell(item.WorkingShift ?? "-");
                            DataCell(item.ProjectCode ?? "-");
                            DataCell(item.SpareDetails ?? "-");
                            DataCell(item.EquipmentStation ?? "-");
                            DataCell(item.QuantityUsed.ToString());
                            DataCell(item.LineTarget ?? "-");
                            DataCell(item.DriEngineer ?? "-");
                            DataCell(item.TotalCostINR.HasValue
                                ? $"₹{item.TotalCostINR.Value:N2}" : "-");
                            DataCell(item.TotalCostINR.HasValue
                                ? $"¥{(item.TotalCostINR.Value * INR_TO_RMB):N2}" : "-");
                        }
                    });

                    // ===== Footer =====
                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text("Foxlink Testing Spare Management System")
                            .FontSize(7).FontColor(Colors.Grey.Medium);
                        row.ConstantItem(120).AlignRight().Text(text =>
                        {
                            text.Span("Page ").FontSize(7);
                            text.CurrentPageNumber().FontSize(7);
                            text.Span(" of ").FontSize(7);
                            text.TotalPages().FontSize(7);
                        });
                    });
                });
            }).GeneratePdf();

            string fileName = $"Daily_Consumption_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task<decimal?> GetUnitCostFromMaster(string spareDetails, string connectionString)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                using SqlCommand command = new SqlCommand(
                    @"SELECT TOP 1 UnitCostINR 
              FROM MasterConnectors 
              WHERE AssetKey LIKE @Key OR Description LIKE @Desc",
                    connection);
                command.Parameters.AddWithValue("@Key", spareDetails + "%");
                command.Parameters.AddWithValue("@Desc", "%" + spareDetails + "%");

                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                    return Convert.ToDecimal(result);

                // ✅ FALLBACK: Default unit cost when item not found in master
                return 1500.00m;  // ₹1500 default per piece
            }
            catch
            {
                return 1500.00m;  // Even on error, return default
            }
        }


        // =====================================================
        // PRIVATE: GET DISTINCT DATES FOR FILTER
        // =====================================================
        private async Task<List<string>> GetDistinctConsumptionDates()
        {
            var dates = new List<string>();
            string connectionString = GetConnectionString();

            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                using SqlCommand command = new SqlCommand(
                    "SELECT DISTINCT CONVERT(VARCHAR(10), ConsumptionDate, 120) AS DateStr " +
                    "FROM DailyConsumption WHERE IsActive = 1 ORDER BY DateStr DESC",
                    connection);
                await connection.OpenAsync();

                using SqlDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    dates.Add(reader["DateStr"].ToString() ?? "");
            }
            catch { /* Silent fail — return empty list */ }

            return dates;
        }


        // =====================================================
        // PRIVATE: GET DROPDOWN DATA
        // =====================================================
        private async Task<List<string>> GetProjectCodes()
        {
            var list = new List<string>();
            string connectionString = GetConnectionString();
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                using SqlCommand command = new SqlCommand(
                    "SELECT ProjectCode FROM ProjectMaster ORDER BY ProjectCode", connection);
                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(reader["ProjectCode"].ToString() ?? "");
            }
            catch { }
            return list;
        }

        private async Task<List<string>> GetEquipmentStations()
        {
            var list = new List<string>();
            string connectionString = GetConnectionString();
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                using SqlCommand command = new SqlCommand(
                    "SELECT StationCode FROM TestingStation ORDER BY StationCode", connection);
                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(reader["StationCode"].ToString() ?? "");
            }
            catch { }
            return list;
        }

        private async Task<List<UserDropdownItem>> GetDriEngineers()
        {
            var list = new List<UserDropdownItem>();
            string connectionString = GetConnectionString();
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                using SqlCommand command = new SqlCommand("dbo.GetSystemUsers", connection);
                command.CommandType = CommandType.StoredProcedure;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new UserDropdownItem
                    {
                        EmployeeId = reader["EmployeeId"]?.ToString() ?? "",
                        FullName = reader["FullName"]?.ToString() ?? "",
                        Role = reader["AssignedRole"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetDriEngineers error: " + ex.Message);
            }
            return list;
        }

        private Task<List<string>> GetLineTargets()
        {
            // ✅ Hardcoded — ProductionLines table doesn't exist in DB
            var list = new List<string> { "Line-01", "Line-02", "Line-03" };
            return Task.FromResult(list);
        }


        // =====================================================
        // PRIVATE: GET SINGLE CONSUMPTION BY ID
        // =====================================================
        private async Task<DailyConsumptionItem?> GetConsumptionById(int id)
        {
            string connectionString = GetConnectionString();
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                using SqlCommand command = new SqlCommand(
                    "SELECT * FROM DailyConsumption WHERE Id = @Id AND IsActive = 1", connection);
                command.Parameters.AddWithValue("@Id", id);
                await connection.OpenAsync();

                using SqlDataReader reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    return MapConsumption(reader);
            }
            catch { }
            return null;
        }


        // =====================================================
        // PRIVATE: RELOAD VIEWMODEL DATA
        // =====================================================
        private async Task ReloadViewModelData(DailyConsumptionViewModel model)
        {
            var (records, totalRecords) = await GetDailyConsumptions(1, 25, null);
            model.Consumptions = records;
            model.TotalRecords = totalRecords;
            model.AvailableDates = await GetDistinctConsumptionDates();
            model.ProjectCodes = await GetProjectCodes();
            model.EquipmentStations = await GetEquipmentStations();
            model.DriEngineers = await GetDriEngineers();
            model.LineTargets = await GetLineTargets();
        }


        // =====================================================
        // PRIVATE: GET PAGINATED RECORDS
        // =====================================================
        private async Task<(List<DailyConsumptionItem> Records, int TotalRecords)>
            GetDailyConsumptions(int pageNumber, int pageSize, string? filterDate = null)
        {
            var list = new List<DailyConsumptionItem>();
            int totalRecords = 0;

            string connectionString = GetConnectionString();
            using SqlConnection connection = new SqlConnection(connectionString);

            // SP accepts only @PageNumber, @PageSize, @TotalRecords
            using SqlCommand command = new SqlCommand("dbo.GetDailyConsumptionReport", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            SqlParameter totalParam = command.Parameters.Add("@TotalRecords", SqlDbType.Int);
            totalParam.Direction = ParameterDirection.Output;

            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(MapConsumption(reader));

            await reader.CloseAsync();

            if (totalParam.Value != DBNull.Value)
                totalRecords = Convert.ToInt32(totalParam.Value);

            // Client-side filter (SP doesn't support @FilterDate yet)
            if (!string.IsNullOrEmpty(filterDate) && DateTime.TryParse(filterDate, out DateTime filterDt))
            {
                list = list.Where(x => x.ConsumptionDate.Date == filterDt.Date).ToList();
                totalRecords = list.Count;
            }

            return (list, totalRecords);
        }


        // =====================================================
        // PRIVATE: MAP SQL READER TO MODEL
        // =====================================================
        private DailyConsumptionItem MapConsumption(SqlDataReader reader)
        {
            decimal? unitCostINR = reader["UnitCostINR"] == DBNull.Value
                ? null
                : Convert.ToDecimal(reader["UnitCostINR"]);

            decimal? totalCostINR = reader["TotalCostINR"] == DBNull.Value
                ? (unitCostINR.HasValue
                    ? unitCostINR.Value * Convert.ToInt32(reader["QuantityUsed"])
                    : null)
                : Convert.ToDecimal(reader["TotalCostINR"]);

            return new DailyConsumptionItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                ConsumptionDate = Convert.ToDateTime(reader["ConsumptionDate"]),
                WorkingShift = reader["WorkingShift"]?.ToString() ?? "",
                ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                SpareDetails = reader["SpareDetails"]?.ToString() ?? "",
                EquipmentStation = reader["EquipmentStation"]?.ToString() ?? "",
                QuantityUsed = Convert.ToInt32(reader["QuantityUsed"]),
                LineTarget = reader["LineTarget"]?.ToString() ?? "",
                DriEngineer = reader["DriEngineer"]?.ToString() ?? "",
                UnitCostINR = unitCostINR,
                UnitCostCNY = reader["UnitCostCNY"] == DBNull.Value
                    ? (unitCostINR.HasValue ? unitCostINR.Value * INR_TO_RMB : null)
                    : Convert.ToDecimal(reader["UnitCostCNY"]),
                TotalCostINR = totalCostINR,
                TotalCostCNY = reader["TotalCostCNY"] == DBNull.Value
                    ? (totalCostINR.HasValue ? totalCostINR.Value * INR_TO_RMB : null)
                    : Convert.ToDecimal(reader["TotalCostCNY"]),
                IsActive = reader["IsActive"] == DBNull.Value
                    ? true
                    : Convert.ToBoolean(reader["IsActive"]),
                CreatedBy = reader["CreatedBy"] == DBNull.Value
                    ? null : reader["CreatedBy"].ToString(),
                CreatedDate = reader["CreatedDate"] == DBNull.Value
                    ? DateTime.MinValue : Convert.ToDateTime(reader["CreatedDate"]),
                ModifiedBy = reader["ModifiedBy"] == DBNull.Value
                    ? null : reader["ModifiedBy"].ToString(),
                ModifiedDate = reader["ModifiedDate"] == DBNull.Value
                    ? null : Convert.ToDateTime(reader["ModifiedDate"])
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