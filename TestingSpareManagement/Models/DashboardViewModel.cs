//namespace TestingSpareManagement.ViewModels
//{
//    public class DashboardViewModel
//    {

//        public int TotalConnectors { get; set; }

//        public int StockOutAlerts { get; set; }

//        public int PendingRequests { get; set; }

//        public decimal YtdSpend { get; set; }

//    }
//}

using System;
using System.Collections.Generic;

namespace TestingSpareManagement.Models
{
    public class DashboardViewModel
    {
        public int ActiveYear { get; set; } = DateTime.Now.Year;

        // ============================================
        // KPI Cards (Already existing - keep as is)
        // ============================================
        public int TotalConnectors { get; set; }
        public int LowStockAlerts { get; set; }
        public int PendingRequests { get; set; }
        public decimal YTDBurnINR { get; set; }
        public decimal YTDBurnRMB { get; set; }
        public decimal AllocatedBudgetINR { get; set; }
        public decimal AllocatedBudgetRMB { get; set; }

        // ============================================
        // Current Month Budget Chart (Already existing)
        // ============================================
        public decimal CurrentMonthAllocatedBudget { get; set; }
        public decimal CurrentMonthActualExpenses { get; set; }
        public decimal CurrentMonthBalanceRemaining { get; set; }
        public string CurrentMonthName { get; set; } = string.Empty;

        // ============================================
        // Yearly Budget Trend Chart (Already existing)
        // ============================================
        public List<int> YearlyLabels { get; set; } = new();
        public List<decimal> YearlyAllocatedBudget { get; set; } = new();
        public List<decimal> YearlyBudgetUtilized { get; set; } = new();
        public List<decimal> YearlyCostSaved { get; set; } = new();

        // ============================================
        // Active Projects (Already existing)
        // ============================================
        public List<ActiveProjectModel> ActiveProjects { get; set; } = new();

        // ============================================
        // 🆕 NEW: Equipment Consumables List
        // (Stock-Out Alerts & Live Requisitions kosam)
        // ============================================
        public List<EquipmentConsumableModel> EquipmentConsumables { get; set; } = new();

        // ============================================
        // 🆕 NEW: Material Requests Queue
        // (Pending Requisitions kosam)
        // ============================================
        public List<MaterialRequestModel> MaterialRequests { get; set; } = new();

        // ============================================
        // 🆕 NEW: Consumption Logs
        // (Charts & Metrics kosam)
        // ============================================
        public List<ConsumptionLogModel> ConsumptionLogs { get; set; } = new();

        // ============================================
        // 🆕 NEW: PR/PO Transactions
        // (Procurement tracking kosam)
        // ============================================
        public List<PRPOTransactionModel> PRPOTransactions { get; set; } = new();
    }

    // ============================================
    // Already existing models (keep as is)
    // ============================================
    public class ActiveProjectModel
    {
        public string ProjectCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "ONLINE";
    }

    public class DashboardKPIModel
    {
        public int TotalConnectors { get; set; }
        public int LowStockAlerts { get; set; }
        public int PendingRequests { get; set; }
        public decimal YTDBurnINR { get; set; }
        public decimal YTDBurnRMB { get; set; }
        public decimal AllocatedBudgetINR { get; set; }
        public decimal AllocatedBudgetRMB { get; set; }
    }

    public class YearlyBudgetTrendModel
    {
        public int Year { get; set; }
        public decimal AllocatedBudget { get; set; }
        public decimal BudgetUtilized { get; set; }
        public decimal CostSaved { get; set; }
    }

    // ============================================
    // 🆕 NEW MODELS (add these below)
    // ============================================

    public class EquipmentConsumableModel
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string TestItem { get; set; } = string.Empty;
        public string AssignedEquipment { get; set; } = string.Empty;
        public string StorageLocation { get; set; } = string.Empty;
        public int AvailableQty { get; set; }
        public int MinLimit { get; set; }
        public decimal UnitPriceINR { get; set; }
        public string SpecCycles { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class MaterialRequestModel
    {
        public int Id { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public string SpareDescription { get; set; } = string.Empty;
        public string PartNo { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string TargetMachine { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class ConsumptionLogModel
    {
        public int Id { get; set; }
        public DateTime ConsumptionDate { get; set; }
        public string Shift { get; set; } = string.Empty;
        public string ProjectCode { get; set; } = string.Empty;
        public string AssetKey { get; set; } = string.Empty;
        public string Station { get; set; } = string.Empty;
        public string TestEquipment { get; set; } = string.Empty;
        public int ConsumedQty { get; set; }
        public string LineTarget { get; set; } = string.Empty;
        public string DRIEngineer { get; set; } = string.Empty;
        public decimal ExpenseINR { get; set; }
    }

    public class PRPOTransactionModel
    {
        public int Id { get; set; }
        public DateTime RequisitionDate { get; set; }
        public string PRNumber { get; set; } = string.Empty;
        public string POStatus { get; set; } = string.Empty;
        public string ProjectCode { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string Buyer { get; set; } = string.Empty;
        public string AssetTarget { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string ShipmentStatus { get; set; } = string.Empty;
        public string ShipmentNo { get; set; } = string.Empty;
        public DateTime? DeliveryDate { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }
}