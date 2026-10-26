using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Net;
using System.Net.Mail;
using TestingSpareManagement.Models;
using CommandType = System.Data.CommandType;
using SqlDbType = System.Data.SqlDbType;

namespace TestingSpareManagement.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        public DashboardController(
            IConfiguration configuration,
            IMemoryCache memoryCache)
        {
            _configuration = configuration;
            _memoryCache = memoryCache;
        }


        [HttpGet]
        public async Task<IActionResult> Index(int? year)
        {
            int activeYear = year ?? DateTime.Now.Year;

            var model = new DashboardViewModel
            {
                ActiveYear = activeYear
            };

            // Load KPIs from stored procedure
            var kpis = await GetDashboardKPIs(activeYear);
            model.TotalConnectors = kpis.TotalConnectors;
            model.LowStockAlerts = kpis.LowStockAlerts;
            model.PendingRequests = kpis.PendingRequests;
            model.YTDBurnINR = kpis.YTDBurnINR;
            model.YTDBurnRMB = kpis.YTDBurnRMB;
            model.AllocatedBudgetINR = kpis.AllocatedBudgetINR;
            model.AllocatedBudgetRMB = kpis.AllocatedBudgetRMB;

            // Current Month Budget
            var currentMonthData = await GetCurrentMonthBudget(activeYear);
            model.CurrentMonthAllocatedBudget = currentMonthData.Allocated;
            model.CurrentMonthActualExpenses = currentMonthData.Expenses;
            model.CurrentMonthBalanceRemaining = currentMonthData.Allocated - currentMonthData.Expenses;
            model.CurrentMonthName = currentMonthData.MonthName;

            // Yearly Trend
            var yearlyTrend = await GetYearlyBudgetTrend();
            foreach (var item in yearlyTrend)
            {
                model.YearlyLabels.Add(item.Year);
                model.YearlyAllocatedBudget.Add(item.AllocatedBudget);
                model.YearlyBudgetUtilized.Add(item.BudgetUtilized);
                model.YearlyCostSaved.Add(item.CostSaved);
            }

            // Active Projects
            model.ActiveProjects = await GetActiveProjectsStatus();

            // Equipment Consumables (for Stock-Out Alerts)
            model.EquipmentConsumables = await GetEquipmentConsumables();

            model.LowStockAlerts = model.EquipmentConsumables
                .Count(e => e.AvailableQty <= e.MinLimit);

            // Material Requests (for Pending Requisitions)
            model.MaterialRequests = await GetMaterialRequests();

            // Consumption Logs (for charts)
            model.ConsumptionLogs = await GetConsumptionLogs(activeYear);

            // PR/PO Logs
            model.PRPOTransactions = await GetPRPOTransactions(activeYear);

            var missingAlerts = await GetMissingConsumptionShiftAlerts();

            ViewBag.MissingConsumptionAlerts = missingAlerts;

            if (missingAlerts.Any())
            {
                try
                {
                    await SendMissingConsumptionAlertEmailAsync(missingAlerts);
                }
                catch (Exception ex)
                {
                    // Keep the dashboard working even if email delivery fails.
                    Console.Error.WriteLine(
                        $"Daily Consumption alert email failed: {ex}");

                    ViewBag.EmailWarning =
                        "The dashboard loaded, but the alert email could not be sent. " +
                        "Please contact IT to verify the SMTP server connection.";
                }
            }

            return View(model);
        }

        private async Task SendMissingConsumptionAlertEmailAsync(
    List<string> alerts)
        {
            if (alerts == null || alerts.Count == 0)
                return;

            // Prevent repeated emails for the same day's alert
            // while the application remains running.
            string cacheKey =
                $"DailyConsumptionAlertEmail_{DateTime.Today:yyyyMMdd}";

            if (_memoryCache.TryGetValue(cacheKey, out bool alreadySent)
                && alreadySent)
            {
                return;
            }


            var smtpSettings = _configuration.GetSection("SmtpSettings");

            string host = smtpSettings["Host"] ?? "";
            string senderEmail = smtpSettings["SenderEmail"] ?? "";
            string senderName =
                smtpSettings["SenderName"] ?? "Testing Spare Management System";

            if (!int.TryParse(smtpSettings["Port"], out int port) ||
                port < 1 || port > 65535)
            {
                throw new InvalidOperationException(
                    "SmtpSettings:Port is missing or invalid.");
            }

            bool enableSsl =
                bool.TryParse(smtpSettings["EnableSsl"], out bool ssl)
                && ssl;

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(senderEmail))
            {
                throw new InvalidOperationException(
                    "SMTP Host and SenderEmail must be configured.");
            }


            using var message = new MailMessage();

            message.From = new MailAddress(senderEmail, senderName);

            message.To.Add("Sharath_G@foxlink.com");
            message.To.Add("Saibalaji_M@foxlink.com");
            message.To.Add("Chandu_V@foxlink.com");
            message.To.Add("Rajasekhar_M@Foxlink.com");
            message.To.Add("Bharathkumar_M@foxlink.com");

            message.Subject =
                $"Action Required: Daily Consumption Missing - {DateTime.Today:dd MMM yyyy}";

            string alertDetails = string.Join(
                Environment.NewLine,
                alerts.Select(a => "• " + a));

            string dashboardUrl =
                $"{Request.Scheme}://{Request.Host}" +
                Url.Action("DailyConsumption", "Consumption");

            message.IsBodyHtml = true;

            message.Body = $@"
                <html>
                <body style='font-family:Arial,sans-serif;color:#333;line-height:1.6;'>
                    <div style='max-width:650px;margin:auto;border:1px solid #ddd;
                                border-radius:8px;overflow:hidden;'>
                        <div style='background:#b91c1c;color:white;padding:18px;'>
                            <h2 style='margin:0;'>Daily Consumption Alert</h2>
                        </div>

                        <div style='padding:22px;'>
                            <p>Dear Team,</p>

                            <p>
                                This is an automated notification from the
                                Testing Spare Management System.
                            </p>

                            <p>
                                The system has detected that today's Daily Consumption
                                entry is missing. Please review the details below and
                                complete the required entry.
                            </p>

                            <p><strong>Date:</strong> {DateTime.Today:dd MMM yyyy}</p>

                            <div style='background:#fff7ed;border-left:4px solid #ea580c;
                                        padding:14px;margin:18px 0;'>
                                <strong>Alert Details</strong>
                                <pre style='white-space:pre-wrap;font-family:Arial,sans-serif;'>
                                    {System.Net.WebUtility.HtmlEncode(alertDetails)}
                                </pre>
                            </div>

                            <p>
                                <a href='{System.Net.WebUtility.HtmlEncode(dashboardUrl)}'
                                   style='background:#b91c1c;color:white;padding:12px 18px;
                                          text-decoration:none;border-radius:5px;
                                          display:inline-block;'>
                                    Open Daily Consumption
                                </a>
                            </p>

                            <p>Please take the necessary action at the earliest.</p>

                            <p>
                                Regards,<br/>
                                <strong>Testing Spare Management System</strong>
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };

            await smtp.SendMailAsync(message);

            _memoryCache.Set(
                cacheKey,
                true,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = DateTime.Today.AddDays(1)
                });
        }

        //private async Task<List<string>> GetMissingConsumptionShiftAlerts()
        //{
        //    var alerts = new List<string>();

        //    DateTime today = DateTime.Today;

        //    string connectionString = GetConnectionString();

        //    using SqlConnection connection =
        //        new SqlConnection(connectionString);

        //    await connection.OpenAsync();

        //    using SqlCommand command = new SqlCommand(@"
        //        SELECT COUNT(1)
        //        FROM dbo.DailyConsumption
        //        WHERE ConsumptionDate >= @Today
        //          AND ConsumptionDate < @Tomorrow
        //          AND IsActive = 1;", connection);

        //    command.Parameters.Add("@Today", SqlDbType.Date).Value = today;
        //    command.Parameters.Add("@Tomorrow", SqlDbType.Date).Value = today.AddDays(1);

        //    int recordCount = Convert.ToInt32(
        //        await command.ExecuteScalarAsync());

        //    if (recordCount > 0)
        //    {
        //        return alerts;
        //    }

        //    alerts.Add(
        //        $"Daily consumption record is missing for today " +
        //        $"({today:dd MMM yyyy}). Please update Daily Consumption."
        //    );

        //    return alerts;
        //}

        private async Task<List<string>> GetMissingConsumptionShiftAlerts()
        {
            var alerts = new List<string>();

            DateTime now = DateTime.Now;
            DateTime today = now.Date;
            TimeSpan currentTime = now.TimeOfDay;

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Exact shift names used by the Daily Consumption form.
            const string dayShift = "Shift-A (Day Shift)";
            const string nightShift = "Shift-B (Night Shift)";

            if (currentTime >= new TimeSpan(18, 0, 0))
            {
                using SqlCommand dayCommand = new SqlCommand(@"
            SELECT COUNT(1)
            FROM dbo.DailyConsumption
            WHERE ConsumptionDate >= @ShiftDate
              AND ConsumptionDate < @NextDate
              AND WorkingShift = @WorkingShift
              AND IsActive = 1;", connection);

                dayCommand.Parameters.Add("@ShiftDate", SqlDbType.Date).Value = today;
                dayCommand.Parameters.Add("@NextDate", SqlDbType.Date).Value = today.AddDays(1);
                dayCommand.Parameters.Add("@WorkingShift", SqlDbType.NVarChar, 100).Value = dayShift;

                int dayCount = Convert.ToInt32(await dayCommand.ExecuteScalarAsync());

                if (dayCount == 0)
                {
                    alerts.Add(
                        $"DAY SHIFT DATA NOT ENTERED: Daily Consumption data for " +
                        $"{today:dd MMM yyyy} has not been entered for the Day Shift " +
                        "(8:00 AM–6:00 PM).");
                }
            }

            if (currentTime >= new TimeSpan(6, 0, 0) &&
                currentTime < new TimeSpan(18, 0, 0))
            {
                DateTime nightShiftDate = today.AddDays(-1);

                using SqlCommand nightCommand = new SqlCommand(@"
            SELECT COUNT(1)
            FROM dbo.DailyConsumption
            WHERE ConsumptionDate >= @ShiftDate
              AND ConsumptionDate < @NextDate
              AND WorkingShift = @WorkingShift
              AND IsActive = 1;", connection);

                nightCommand.Parameters.Add("@ShiftDate", SqlDbType.Date).Value = nightShiftDate;
                nightCommand.Parameters.Add("@NextDate", SqlDbType.Date).Value = today;
                nightCommand.Parameters.Add("@WorkingShift", SqlDbType.NVarChar, 100).Value = nightShift;

                int nightCount = Convert.ToInt32(await nightCommand.ExecuteScalarAsync());

                if (nightCount == 0)
                {
                    alerts.Add(
                        $"NIGHT SHIFT DATA NOT ENTERED: Daily Consumption data for " +
                        $"the night shift starting {nightShiftDate:dd MMM yyyy} " +
                        $"and ending {today:dd MMM yyyy} has not been entered.");
                }
            }

            return alerts;
        }

        private async Task<DashboardKPIModel> GetDashboardKPIs(int year)
        {
            var model = new DashboardKPIModel();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetDashboardKPIs", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Year", SqlDbType.Int).Value = year;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                model.TotalConnectors = reader["TotalConnectors"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalConnectors"]);
                model.LowStockAlerts = reader["LowStockAlerts"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LowStockAlerts"]);
                model.PendingRequests = reader["PendingRequests"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PendingRequests"]);
                model.YTDBurnINR = reader["YTDBurnINR"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["YTDBurnINR"]);
                model.YTDBurnRMB = reader["YTDBurnRMB"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["YTDBurnRMB"]);
                model.AllocatedBudgetINR = reader["AllocatedBudgetINR"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["AllocatedBudgetINR"]);
                model.AllocatedBudgetRMB = reader["AllocatedBudgetRMB"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["AllocatedBudgetRMB"]);
            }

            return model;
        }

        private async Task<(decimal Allocated, decimal Expenses, string MonthName)> GetCurrentMonthBudget(int year)
        {
            decimal allocated = 0;
            decimal expenses = 0;
            string monthName = DateTime.Now.ToString("MMMM");

            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);

            string query = @"
                SELECT 
                    ISNULL([AllocatedBudgetINR], 0) AS Allocated,
                    ISNULL([ActualExpensesINR], 0) AS Expenses,
                    [MonthName]
                FROM [dbo].[BudgetExpenditure]
                WHERE [Year] = @Year 
                  AND [MonthNumber] = @MonthNumber
                  AND [IsActive] = 1";

            using SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.Add("@Year", SqlDbType.Int).Value = year;
            command.Parameters.Add("@MonthNumber", SqlDbType.Int).Value = DateTime.Now.Month;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                allocated = reader["Allocated"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Allocated"]);
                expenses = reader["Expenses"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Expenses"]);
                monthName = reader["MonthName"]?.ToString() ?? DateTime.Now.ToString("MMMM");
            }

            return (allocated, expenses, monthName);
        }

        private async Task<List<YearlyBudgetTrendModel>> GetYearlyBudgetTrend()
        {
            var list = new List<YearlyBudgetTrendModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetYearlyBudgetTrend", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new YearlyBudgetTrendModel
                {
                    Year = Convert.ToInt32(reader["Year"]),
                    AllocatedBudget = Convert.ToDecimal(reader["AllocatedBudget"]),
                    BudgetUtilized = Convert.ToDecimal(reader["BudgetUtilized"]),
                    CostSaved = Convert.ToDecimal(reader["CostSaved"])
                });
            }

            return list;
        }

        private async Task<List<ActiveProjectModel>> GetActiveProjectsStatus()
        {
            var list = new List<ActiveProjectModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);
            using SqlCommand command = new SqlCommand("dbo.GetActiveProjectsStatus", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ActiveProjectModel
                {
                    ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                    Description = reader["Description"]?.ToString() ?? "",
                    Status = reader["Status"]?.ToString() ?? "ONLINE"
                });
            }

            return list;
        }

        //private async Task<List<EquipmentConsumableModel>> GetEquipmentConsumables()
        //{
        //    var list = new List<EquipmentConsumableModel>();
        //    string conn = GetConnectionString();

        //    using SqlConnection connection = new SqlConnection(conn);

        //    string query = @"
        //        SELECT 
        //            ec.[Id],
        //            ec.[ProjectCode],
        //            ec.[TestItem],
        //            ec.[AssignedEquipment],
        //            ec.[StorageLocation],
        //            ec.[AvailableQty],
        //            ec.[MinLimit],
        //            ec.[UnitPriceINR],
        //            ec.[SpecCycles],
        //            ec.[ImageUrl]
        //        FROM [dbo].[EquipmentConsumables] ec
        //        WHERE ec.[IsActive] = 1
        //        ORDER BY ec.[ProjectCode], ec.[TestItem]";

        //    using SqlCommand command = new SqlCommand(query, connection);

        //    await connection.OpenAsync();
        //    using SqlDataReader reader = await command.ExecuteReaderAsync();

        //    while (await reader.ReadAsync())
        //    {
        //        list.Add(new EquipmentConsumableModel
        //        {
        //            Id = Convert.ToInt32(reader["Id"]),
        //            ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
        //            TestItem = reader["TestItem"]?.ToString() ?? "",
        //            AssignedEquipment = reader["AssignedEquipment"]?.ToString() ?? "",
        //            StorageLocation = reader["StorageLocation"]?.ToString() ?? "",
        //            AvailableQty = reader["AvailableQty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["AvailableQty"]),
        //            MinLimit = reader["MinLimit"] == DBNull.Value ? 0 : Convert.ToInt32(reader["MinLimit"]),
        //            UnitPriceINR = reader["UnitPriceINR"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["UnitPriceINR"]),
        //            SpecCycles = reader["SpecCycles"]?.ToString() ?? "",
        //            ImageUrl = reader["ImageUrl"]?.ToString() ?? ""
        //        });
        //    }

        //    return list;
        //}


        private async Task<List<EquipmentConsumableModel>> GetEquipmentConsumables()
        {
            var list = new List<EquipmentConsumableModel>();

            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);

            using SqlCommand command =
                new SqlCommand("dbo.GetEquipmentReport", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = 1;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = 10000;

            SqlParameter totalParam =
                command.Parameters.Add("@TotalRecords", SqlDbType.Int);

            totalParam.Direction = ParameterDirection.Output;

            await connection.OpenAsync();

            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new EquipmentConsumableModel
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                        TestItem = reader["TestItemName"]?.ToString() ?? "",
                        AssignedEquipment =
                            reader["AssignedEquipment"]?.ToString() ?? "",
                        AvailableQty =
                            reader["AvailableQuantity"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["AvailableQuantity"]),
                        MinLimit =
                            reader["AlertThreshold"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["AlertThreshold"]),
                        UnitPriceINR =
                            reader["UnitPriceINR"] == DBNull.Value
                                ? 0
                                : Convert.ToDecimal(reader["UnitPriceINR"]),
                        SpecCycles =
                            reader["SpecCycles"] == DBNull.Value
                                ? ""
                                : reader["SpecCycles"].ToString() ?? ""
                    });
                }
            }

            return list;
        }


        private async Task<List<MaterialRequestModel>> GetMaterialRequests()
        {
            var list = new List<MaterialRequestModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);

            string query = @"
                SELECT 
                    mr.[Id],
                    mr.[RequestId],
                    mr.[RequestDate],
                    mr.[ProjectCode],
                    mr.[RequestedBy],
                    mr.[SpareDescription],
                    mr.[PartNo],
                    mr.[Brand],
                    mr.[TargetMachine],
                    mr.[Quantity],
                    mr.[Priority],
                    mr.[Status]
                FROM [dbo].[MaterialRequests] mr
                WHERE mr.[Status] = 'Pending Admin Approval'
                ORDER BY mr.[RequestDate] DESC";

            using SqlCommand command = new SqlCommand(query, connection);

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new MaterialRequestModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    RequestId = reader["RequestId"]?.ToString() ?? "",
                    RequestDate = reader["RequestDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["RequestDate"]),
                    ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                    RequestedBy = reader["RequestedBy"]?.ToString() ?? "",
                    SpareDescription = reader["SpareDescription"]?.ToString() ?? "",
                    PartNo = reader["PartNo"]?.ToString() ?? "",
                    Brand = reader["Brand"]?.ToString() ?? "",
                    TargetMachine = reader["TargetMachine"]?.ToString() ?? "",
                    Quantity = reader["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Quantity"]),
                    Priority = reader["Priority"]?.ToString() ?? "Normal Reorder",
                    Status = reader["Status"]?.ToString() ?? "Pending"
                });
            }

            return list;
        }

        private async Task<List<ConsumptionLogModel>> GetConsumptionLogs(int year)
        {
            var list = new List<ConsumptionLogModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);

            string query = @"
                SELECT 
                    cl.[Id],
                    cl.[ConsumptionDate],
                    cl.[Shift],
                    cl.[ProjectCode],
                    cl.[AssetKey],
                    cl.[Station],
                    cl.[TestEquipment],
                    cl.[ConsumedQty],
                    cl.[LineTarget],
                    cl.[DRIEngineer],
                    cl.[ExpenseINR]
                FROM [dbo].[DailyConsumptionLogs] cl
                WHERE YEAR(cl.[ConsumptionDate]) = @Year
                ORDER BY cl.[ConsumptionDate] DESC";

            using SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.Add("@Year", SqlDbType.Int).Value = year;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ConsumptionLogModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    ConsumptionDate = reader["ConsumptionDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["ConsumptionDate"]),
                    Shift = reader["Shift"]?.ToString() ?? "Shift-A",
                    ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                    AssetKey = reader["AssetKey"]?.ToString() ?? "",
                    Station = reader["Station"]?.ToString() ?? "",
                    TestEquipment = reader["TestEquipment"]?.ToString() ?? "",
                    ConsumedQty = reader["ConsumedQty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ConsumedQty"]),
                    LineTarget = reader["LineTarget"]?.ToString() ?? "Line-01",
                    DRIEngineer = reader["DRIEngineer"]?.ToString() ?? "",
                    ExpenseINR = reader["ExpenseINR"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["ExpenseINR"])
                });
            }

            return list;
        }

        private async Task<List<PRPOTransactionModel>> GetPRPOTransactions(int year)
        {
            var list = new List<PRPOTransactionModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);

            string query = @"
                SELECT 
                    pt.[Id],
                    pt.[RequisitionDate],
                    pt.[PRNumber],
                    pt.[POStatus],
                    pt.[ProjectCode],
                    pt.[Origin],
                    pt.[Buyer],
                    pt.[AssetTarget],
                    pt.[Quantity],
                    pt.[UnitPrice],
                    pt.[ShipmentStatus],
                    pt.[ShipmentNo],
                    pt.[DeliveryDate],
                    pt.[Remarks]
                FROM [dbo].[PRPOTransactions] pt
                WHERE YEAR(pt.[RequisitionDate]) = @Year
                ORDER BY pt.[RequisitionDate] DESC";

            using SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.Add("@Year", SqlDbType.Int).Value = year;

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new PRPOTransactionModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    RequisitionDate = reader["RequisitionDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["RequisitionDate"]),
                    PRNumber = reader["PRNumber"]?.ToString() ?? "",
                    POStatus = reader["POStatus"]?.ToString() ?? "",
                    ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                    Origin = reader["Origin"]?.ToString() ?? "",
                    Buyer = reader["Buyer"]?.ToString() ?? "",
                    AssetTarget = reader["AssetTarget"]?.ToString() ?? "",
                    Quantity = reader["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Quantity"]),
                    UnitPrice = reader["UnitPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["UnitPrice"]),
                    ShipmentStatus = reader["ShipmentStatus"]?.ToString() ?? "",
                    ShipmentNo = reader["ShipmentNo"]?.ToString() ?? "",
                    DeliveryDate = reader["DeliveryDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["DeliveryDate"]),
                    Remarks = reader["Remarks"]?.ToString() ?? ""
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