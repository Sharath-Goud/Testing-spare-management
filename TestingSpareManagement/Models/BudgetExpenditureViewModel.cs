//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace TestingSpareManagement.Models
//{
//    // =====================================================
//    // VIEW MODEL (for Chart + Cards + Table)
//    // =====================================================
//    public class BudgetExpenditureViewModel
//    {
//        public int ActiveYear { get; set; } = DateTime.Now.Year;

//        // Summary Cards (Auto-Calculated from Ledger)
//        public decimal TotalAllocatedBudget => MonthlyLedger?.Sum(x => x.AllocatedBudgetINR) ?? 0;
//        public decimal TotalAllocatedBudgetRMB => Math.Round(TotalAllocatedBudget / 11.66m, 2);
//        public decimal CumulativeExpensesYTD => MonthlyLedger?.Sum(x => x.ActualExpensesINR) ?? 0;
//        public decimal CumulativeExpensesYTDRMB => Math.Round(CumulativeExpensesYTD / 11.66m, 2);
//        public decimal TotalCostSaved => TotalAllocatedBudget - CumulativeExpensesYTD;
//        public decimal TotalCostSavedRMB => Math.Round(TotalCostSaved / 11.66m, 2);

//        public string BudgetAllocationHealth =>
//            CumulativeExpensesYTD <= TotalAllocatedBudget ? "STABLE CAPACITY" : "OVER BUDGET";

//        // Chart Data (Auto-Calculated)
//        public List<string> ChartLabels => MonthlyLedger?.Select(x => x.MonthName).ToList() ?? new List<string>();
//        public List<decimal> ChartBudgetLine => MonthlyLedger?.Select(x => x.AllocatedBudgetINR).ToList() ?? new List<decimal>();
//        public List<decimal> ChartActualExpenses => MonthlyLedger?.Select(x => x.ActualExpensesINR).ToList() ?? new List<decimal>();

//        // Ledger Table
//        public List<MonthlyLedgerItem> MonthlyLedger { get; set; } = new List<MonthlyLedgerItem>();
//    }


//    // =====================================================
//    // DATABASE ENTITY (maps to BudgetExpenditure table)
//    // =====================================================
//    public class MonthlyLedgerItem
//    {
//        public int Id { get; set; }
//        public int Year { get; set; }
//        public int MonthNumber { get; set; }
//        public string MonthName { get; set; } = string.Empty;

//        // Display: "January (2026)"
//        public string Month => $"{MonthName} ({Year})";

//        public decimal AllocatedBudgetINR { get; set; }
//        public decimal ActualExpensesINR { get; set; }
//        public decimal ActualExpensesRMB { get; set; }

//        // Auto-Calculated
//        public decimal RemainingAmountINR => AllocatedBudgetINR - ActualExpensesINR;
//        public decimal TotalCostSavedINR => RemainingAmountINR;
//        public string Status => RemainingAmountINR >= 0 ? "SAFE" : "DANGER";

//        public bool IsActive { get; set; }
//        public string? CreatedBy { get; set; }
//        public DateTime CreatedDate { get; set; }
//        public string? ModifiedBy { get; set; }
//        public DateTime? ModifiedDate { get; set; }
//    }
//}

using System;
using System.Collections.Generic;

namespace TestingSpareManagement.Models
{
    public class BudgetExpenditureViewModel
    {
        public int ActiveYear { get; set; } = DateTime.Now.Year;

        // Monthly Ledger
        public List<MonthlyLedgerItem> MonthlyLedger { get; set; } = new List<MonthlyLedgerItem>();

        // Summary Cards
        public decimal TotalAllocatedBudget { get; set; }
        public decimal TotalAllocatedBudgetRMB { get; set; }
        public decimal CumulativeExpensesYTD { get; set; }
        public decimal CumulativeExpensesYTDRMB { get; set; }
        public decimal TotalCostSaved { get; set; }
        public decimal TotalCostSavedRMB { get; set; }
        public string BudgetAllocationHealth { get; set; } = "STABLE CAPACITY";

        // Chart Data
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartBudgetLine { get; set; } = new List<decimal>();
        public List<decimal> ChartActualExpenses { get; set; } = new List<decimal>();
    }

    public class MonthlyLedgerItem
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int MonthNumber { get; set; }
        public string MonthName { get; set; } = "";
        public decimal AllocatedBudgetINR { get; set; }
        public decimal ActualExpensesINR { get; set; }
        public decimal ActualExpensesRMB { get; set; }
        public bool IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Computed Properties
        public string Month => $"{MonthName} ({Year})";
        public decimal RemainingAmountINR => AllocatedBudgetINR - ActualExpensesINR;
        public decimal TotalCostSavedINR => RemainingAmountINR > 0 ? RemainingAmountINR : 0;
        public string Status => ActualExpensesINR > AllocatedBudgetINR ? "DANGER" : "SAFE";
    }
}