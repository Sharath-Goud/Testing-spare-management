using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TestingSpareManagement.Models;

// Excel & PDF export
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;

namespace TestingSpareManagement.Controllers
{
    public class BudgetController : Controller
    {
        private readonly IConfiguration _configuration;
        private const decimal INR_TO_RMB = 0.0858m;

        public BudgetController(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        // =====================================================
        // GET: Budget/Index
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index(int? year)
        {
            int activeYear = year ?? DateTime.Now.Year;

            var model = new BudgetExpenditureViewModel
            {
                ActiveYear = activeYear,
                MonthlyLedger = await GetBudgetLedger(activeYear)
            };

            BuildChartData(model);
            BuildSummaryCards(model);

            return View(model);
        }


        // =====================================================
        // GET: Budget/ExportExcel
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ExportExcel(int? year)
        {
            int activeYear = year ?? DateTime.Now.Year;
            var ledger = await GetBudgetLedger(activeYear);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add($"Budget {activeYear}");

            // Title
            ws.Cell(1, 1).Value = "TESTING SPARE MANAGEMENT SYSTEM";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#3B4CCA");
            ws.Range(1, 1, 1, 7).Merge();

            ws.Cell(2, 1).Value = $"Budget & Expenditure Ledger — Year {activeYear}";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Cell(2, 1).Style.Font.FontSize = 11;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range(2, 1, 2, 7).Merge();

            ws.Cell(3, 1).Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            ws.Cell(3, 1).Style.Font.FontSize = 9;
            ws.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range(3, 1, 3, 7).Merge();

            // Headers
            string[] headers = {
                "MONTH", "ALLOCATED (INR)", "EXPENSES (INR)",
                "EXPENSES (RMB)", "REMAINING (INR)", "SAVED (INR)", "STATUS"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(5, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#8B5CF6");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data
            int row = 6;
            decimal totalAlloc = 0, totalExp = 0, totalSaved = 0;

            foreach (var item in ledger.OrderBy(x => x.MonthNumber))
            {
                var balance = item.AllocatedBudgetINR - item.ActualExpensesINR;
                var saved = balance > 0 ? balance : 0;

                totalAlloc += item.AllocatedBudgetINR;
                totalExp += item.ActualExpensesINR;
                totalSaved += saved;

                ws.Cell(row, 1).Value = $"{item.MonthName} ({activeYear})";
                ws.Cell(row, 2).Value = item.AllocatedBudgetINR;
                ws.Cell(row, 3).Value = item.ActualExpensesINR;
                ws.Cell(row, 4).Value = item.ActualExpensesINR * INR_TO_RMB;
                ws.Cell(row, 5).Value = balance;
                ws.Cell(row, 6).Value = saved;

                ws.Cell(row, 2).Style.NumberFormat.Format = "₹#,##0.00";
                ws.Cell(row, 3).Style.NumberFormat.Format = "₹#,##0.00";
                ws.Cell(row, 4).Style.NumberFormat.Format = "¥#,##0.00";
                ws.Cell(row, 5).Style.NumberFormat.Format = "₹#,##0.00";
                ws.Cell(row, 6).Style.NumberFormat.Format = "₹#,##0.00";

                var statusCell = ws.Cell(row, 7);
                if (balance < 0)
                {
                    statusCell.Value = "DANGER";
                    statusCell.Style.Font.FontColor = XLColor.FromHtml("#991B1B");
                    statusCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                }
                else
                {
                    statusCell.Value = "SAFE";
                    statusCell.Style.Font.FontColor = XLColor.FromHtml("#15803D");
                    statusCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                }
                statusCell.Style.Font.Bold = true;

                if (row % 2 == 0)
                    ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");

                row++;
            }

            // Totals
            row++;
            ws.Cell(row, 1).Value = "TOTAL:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = totalAlloc;
            ws.Cell(row, 3).Value = totalExp;
            ws.Cell(row, 5).Value = totalAlloc - totalExp;
            ws.Cell(row, 6).Value = totalSaved;

            ws.Cell(row, 2).Style.NumberFormat.Format = "₹#,##0.00";
            ws.Cell(row, 3).Style.NumberFormat.Format = "₹#,##0.00";
            ws.Cell(row, 5).Style.NumberFormat.Format = "₹#,##0.00";
            ws.Cell(row, 6).Style.NumberFormat.Format = "₹#,##0.00";

            ws.Range(row, 1, row, 7).Style.Font.Bold = true;
            ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"Budget_Ledger_{activeYear}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }


        // =====================================================
        // GET: Budget/ExportPdf
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ExportPdf(int? year)
        {
            int activeYear = year ?? DateTime.Now.Year;
            var ledger = await GetBudgetLedger(activeYear);

            QuestPDF.Settings.License = LicenseType.Community;

            decimal totalAlloc = ledger.Sum(x => x.AllocatedBudgetINR);
            decimal totalExp = ledger.Sum(x => x.ActualExpensesINR);
            decimal totalSaved = ledger.Sum(x => Math.Max(0, x.AllocatedBudgetINR - x.ActualExpensesINR));

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                    // Header
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
                                c.Item().Text($"Budget & Expenditure Ledger — {activeYear}")
                                    .FontSize(10).Bold().FontColor(Colors.White);
                            });
                            row.ConstantItem(180).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                                    .FontSize(8).FontColor(Colors.White);
                                c.Item().Text($"Total Months: {ledger.Count}")
                                    .FontSize(8).FontColor(Colors.White);
                            });
                        });
                        col.Item().PaddingTop(8);
                    });

                    // Content
                    page.Content().Column(col =>
                    {
                        // Summary cards
                        col.Item().PaddingBottom(10).Row(row =>
                        {
                            void SummaryCard(string label, string value, string color) =>
                                row.RelativeItem().Padding(4).Background(color).Padding(8).Column(c =>
                                {
                                    c.Item().Text(label).FontSize(7).FontColor(Colors.White);
                                    c.Item().Text(value).FontSize(11).Bold().FontColor(Colors.White);
                                });

                            SummaryCard("TOTAL ALLOCATED", $"₹{totalAlloc:N0}", "#8B5CF6");
                            SummaryCard("TOTAL EXPENSES", $"₹{totalExp:N0}", "#DC2626");
                            SummaryCard("TOTAL SAVED", $"₹{totalSaved:N0}", "#2563EB");
                        });

                        // Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1.5f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(0.8f);
                            });

                            table.Header(header =>
                            {
                                void HeaderCell(string text) =>
                                    header.Cell().Background("#8B5CF6").Padding(4)
                                        .Text(text).Bold().FontColor(Colors.White).FontSize(8);

                                HeaderCell("MONTH");
                                HeaderCell("ALLOCATED (₹)");
                                HeaderCell("EXPENSES (₹)");
                                HeaderCell("EXPENSES (¥)");
                                HeaderCell("REMAINING (₹)");
                                HeaderCell("SAVED (₹)");
                                HeaderCell("STATUS");
                            });

                            int idx = 0;
                            foreach (var item in ledger.OrderBy(x => x.MonthNumber))
                            {
                                idx++;
                                var balance = item.AllocatedBudgetINR - item.ActualExpensesINR;
                                var saved = balance > 0 ? balance : 0;
                                var bg = idx % 2 == 0 ? "#FAFAFA" : "#FFFFFF";

                                void DataCell(string text) =>
                                    table.Cell().Background(bg).Padding(3).Text(text).FontSize(8);

                                DataCell($"{item.MonthName} {activeYear}");
                                DataCell($"₹{item.AllocatedBudgetINR:N0}");
                                DataCell($"₹{item.ActualExpensesINR:N0}");
                                DataCell($"¥{(item.ActualExpensesINR * INR_TO_RMB):N2}");
                                DataCell($"₹{balance:N0}");
                                DataCell($"₹{saved:N0}");

                                var statusText = balance < 0 ? "DANGER" : "SAFE";
                                var statusBg = balance < 0 ? "#FEE2E2" : "#DCFCE7";
                                var statusColor = balance < 0 ? "#991B1B" : "#15803D";

                                table.Cell().Background(statusBg).Padding(3)
                                    .Text(statusText).FontSize(8).Bold().FontColor(statusColor);
                            }
                        });
                    });

                    // Footer
                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"Foxlink Budget Ledger — {activeYear}")
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

            string fileName = $"Budget_Ledger_{activeYear}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }


        // =====================================================
        // POST: Budget/UpdateExpense
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateExpense(int year, int monthNumber, decimal actualExpensesINR)
        {
            if (year <= 0 || monthNumber < 1 || monthNumber > 12)
            {
                TempData["ErrorMessage"] = "Invalid month or year.";
                return RedirectToAction(nameof(Index), new { year });
            }

            if (actualExpensesINR < 0)
            {
                TempData["ErrorMessage"] = "Expense cannot be negative.";
                return RedirectToAction(nameof(Index), new { year });
            }

            try
            {
                string conn = GetConnectionString();
                using SqlConnection connection = new SqlConnection(conn);
                using SqlCommand command = new SqlCommand("dbo.UpdateBudgetExpense", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@Year", SqlDbType.Int).Value = year;
                command.Parameters.Add("@MonthNumber", SqlDbType.Int).Value = monthNumber;

                var inrParam = command.Parameters.Add("@ActualExpensesINR", SqlDbType.Decimal);
                inrParam.Precision = 18; inrParam.Scale = 2;
                inrParam.Value = actualExpensesINR;

                var rateParam = command.Parameters.Add("@ExchangeRate", SqlDbType.Decimal);
                rateParam.Precision = 10; rateParam.Scale = 4;
                rateParam.Value = 11.66m;

                command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                    HttpContext.Session.GetString("EmployeeName") ?? "System";

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = $"Expense updated to ₹{actualExpensesINR:N0} successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to update expense: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { year });
        }


        // =====================================================
        // POST: Budget/UpdateYearlyBudget
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateYearlyBudget(int year, decimal monthlyBudgetINR)
        {
            if (year <= 0)
            {
                TempData["ErrorMessage"] = "Invalid year.";
                return RedirectToAction(nameof(Index));
            }

            if (monthlyBudgetINR < 0)
            {
                TempData["ErrorMessage"] = "Budget cannot be negative.";
                return RedirectToAction(nameof(Index), new { year });
            }

            try
            {
                string conn = GetConnectionString();
                using SqlConnection connection = new SqlConnection(conn);
                using SqlCommand command = new SqlCommand("dbo.UpdateYearlyBudgetAllocation", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@Year", SqlDbType.Int).Value = year;

                var budgetParam = command.Parameters.Add("@MonthlyBudgetINR", SqlDbType.Decimal);
                budgetParam.Precision = 18; budgetParam.Scale = 2;
                budgetParam.Value = monthlyBudgetINR;

                command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                    HttpContext.Session.GetString("EmployeeName") ?? "System";

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = $"Yearly budget updated to ₹{monthlyBudgetINR:N0} per month for {year}.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to update yearly budget: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { year });
        }


        // =====================================================
        // PRIVATE: GET BUDGET LEDGER
        // =====================================================
        private async Task<List<MonthlyLedgerItem>> GetBudgetLedger(int year)
        {
            var list = new List<MonthlyLedgerItem>();

            try
            {
                string conn = GetConnectionString();
                using SqlConnection connection = new SqlConnection(conn);
                using SqlCommand command = new SqlCommand("dbo.GetBudgetLedgerByYear", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@Year", SqlDbType.Int).Value = year;

                var defaultBudget = command.Parameters.Add("@DefaultBudgetINR", SqlDbType.Decimal);
                defaultBudget.Precision = 18; defaultBudget.Scale = 2;
                defaultBudget.Value = 0m;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(MapLedgerItem(reader));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading budget ledger: {ex.Message}");
                list = BuildFallbackLedger(year);
            }

            return list;
        }


        // =====================================================
        // PRIVATE: FALLBACK LEDGER
        // =====================================================
        private List<MonthlyLedgerItem> BuildFallbackLedger(int year)
        {
            var months = new[]
            {
                "January", "February", "March", "April", "May", "June",
                "July", "August", "September", "October", "November", "December"
            };

            var list = new List<MonthlyLedgerItem>();

            for (int i = 0; i < months.Length; i++)
            {
                var budget = 200000m;
                var expense = 0m;

                list.Add(new MonthlyLedgerItem
                {
                    Id = i + 1,
                    Year = year,
                    MonthNumber = i + 1,
                    MonthName = months[i],
                    AllocatedBudgetINR = budget,
                    ActualExpensesINR = expense,
                    ActualExpensesRMB = expense * INR_TO_RMB,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });
            }

            return list;
        }


        // =====================================================
        // PRIVATE: BUILD CHART DATA
        // =====================================================
        private void BuildChartData(BudgetExpenditureViewModel model)
        {
            if (model.MonthlyLedger == null || !model.MonthlyLedger.Any()) return;

            var ordered = model.MonthlyLedger.OrderBy(x => x.MonthNumber).ToList();

            model.ChartLabels = ordered.Select(x => x.MonthName).ToList();
            model.ChartBudgetLine = ordered.Select(x => x.AllocatedBudgetINR).ToList();
            model.ChartActualExpenses = ordered.Select(x => x.ActualExpensesINR).ToList();
        }


        // =====================================================
        // PRIVATE: BUILD SUMMARY CARDS
        // =====================================================
        private void BuildSummaryCards(BudgetExpenditureViewModel model)
        {
            if (model.MonthlyLedger == null || !model.MonthlyLedger.Any()) return;

            decimal totalAlloc = 0;
            decimal totalExp = 0;
            decimal totalSaved = 0;
            bool anyBreached = false;

            foreach (var item in model.MonthlyLedger)
            {
                totalAlloc += item.AllocatedBudgetINR;
                totalExp += item.ActualExpensesINR;

                var balance = item.AllocatedBudgetINR - item.ActualExpensesINR;
                if (balance > 0) totalSaved += balance;
                else if (balance < 0) anyBreached = true;
            }

            model.TotalAllocatedBudget = totalAlloc;
            model.TotalAllocatedBudgetRMB = totalAlloc * INR_TO_RMB;

            model.CumulativeExpensesYTD = totalExp;
            model.CumulativeExpensesYTDRMB = totalExp * INR_TO_RMB;

            model.TotalCostSaved = totalSaved;
            model.TotalCostSavedRMB = totalSaved * INR_TO_RMB;

            model.BudgetAllocationHealth = anyBreached ? "BREACH DETECTED" : "STABLE CAPACITY";
        }


        // =====================================================
        // PRIVATE: MAP SQL READER
        // =====================================================
        private MonthlyLedgerItem MapLedgerItem(SqlDataReader reader)
        {
            return new MonthlyLedgerItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                Year = Convert.ToInt32(reader["Year"]),
                MonthNumber = Convert.ToInt32(reader["MonthNumber"]),
                MonthName = reader["MonthName"]?.ToString() ?? "",
                AllocatedBudgetINR = Convert.ToDecimal(reader["AllocatedBudgetINR"]),
                ActualExpensesINR = Convert.ToDecimal(reader["ActualExpensesINR"]),
                ActualExpensesRMB = Convert.ToDecimal(reader["ActualExpensesRMB"]),
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
            string? cs = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("DefaultConnection was not found.");
            return cs;
        }
    }
}