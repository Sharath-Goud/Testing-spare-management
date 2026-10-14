using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TestingSpareManagement.Models;
using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;

namespace TestingSpareManagement.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;

        public DashboardController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // =========================================================
        // INDEX - MAIN DASHBOARD
        // =========================================================
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

            // Material Requests (for Pending Requisitions)
            model.MaterialRequests = await GetMaterialRequests();

            // Consumption Logs (for charts)
            model.ConsumptionLogs = await GetConsumptionLogs(activeYear);

            // PR/PO Logs
            model.PRPOTransactions = await GetPRPOTransactions(activeYear);

            return View(model);
        }

        // =========================================================
        // GET DASHBOARD KPIs (Stored Procedure)
        // =========================================================
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

        // =========================================================
        // GET CURRENT MONTH BUDGET
        // =========================================================
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

        // =========================================================
        // GET YEARLY BUDGET TREND (Stored Procedure)
        // =========================================================
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

        // =========================================================
        // GET ACTIVE PROJECTS STATUS
        // =========================================================
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

        // =========================================================
        // GET EQUIPMENT CONSUMABLES (For Stock-Out Alerts)
        // =========================================================
        private async Task<List<EquipmentConsumableModel>> GetEquipmentConsumables()
        {
            var list = new List<EquipmentConsumableModel>();
            string conn = GetConnectionString();

            using SqlConnection connection = new SqlConnection(conn);

            string query = @"
                SELECT 
                    ec.[Id],
                    ec.[ProjectCode],
                    ec.[TestItem],
                    ec.[AssignedEquipment],
                    ec.[StorageLocation],
                    ec.[AvailableQty],
                    ec.[MinLimit],
                    ec.[UnitPriceINR],
                    ec.[SpecCycles],
                    ec.[ImageUrl]
                FROM [dbo].[EquipmentConsumables] ec
                WHERE ec.[IsActive] = 1
                ORDER BY ec.[ProjectCode], ec.[TestItem]";

            using SqlCommand command = new SqlCommand(query, connection);

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new EquipmentConsumableModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                    TestItem = reader["TestItem"]?.ToString() ?? "",
                    AssignedEquipment = reader["AssignedEquipment"]?.ToString() ?? "",
                    StorageLocation = reader["StorageLocation"]?.ToString() ?? "",
                    AvailableQty = reader["AvailableQty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["AvailableQty"]),
                    MinLimit = reader["MinLimit"] == DBNull.Value ? 0 : Convert.ToInt32(reader["MinLimit"]),
                    UnitPriceINR = reader["UnitPriceINR"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["UnitPriceINR"]),
                    SpecCycles = reader["SpecCycles"]?.ToString() ?? "",
                    ImageUrl = reader["ImageUrl"]?.ToString() ?? ""
                });
            }

            return list;
        }

        // =========================================================
        // GET MATERIAL REQUESTS (For Pending Requisitions)
        // =========================================================
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

        // =========================================================
        // GET CONSUMPTION LOGS (For Charts and Metrics)
        // =========================================================
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

        // =========================================================
        // GET PR/PO TRANSACTIONS
        // =========================================================
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

        // =========================================================
        // CONNECTION STRING
        // =========================================================
        private string GetConnectionString()
        {
            string? cs = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("DefaultConnection was not found.");
            return cs;
        }
    }
}