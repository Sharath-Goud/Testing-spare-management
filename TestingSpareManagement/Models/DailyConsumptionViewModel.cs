
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestingSpareManagement.Models
{
    public class DailyConsumptionViewModel
    {
        // Form Fields
        [Required]
        [Display(Name = "Consumption Date")]
        public DateTime SelectedDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Working Shift")]
        public string WorkingShift { get; set; } = "Shift-A (Day Shift)";

        [Required]
        [Display(Name = "Project Code")]
        public string ProjectCode { get; set; } = "A246F";

        [Required]
        [Display(Name = "Spare/Connector Details")]
        [StringLength(100)]
        public string SpareDetails { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Equipment Station")]
        public string EquipmentStation { get; set; } = "OS";

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        [Display(Name = "Quantity Used")]
        public int QuantityUsed { get; set; }

        [Required]
        [Display(Name = "Line Target")]
        public string LineTarget { get; set; } = "Line-01";

        [Required]
        [Display(Name = "DRI Engineer")]
        public string DriEngineer { get; set; } = string.Empty;

        // For Edit / Delete
        public int Id { get; set; }

        // ✅ FIX: Filter
        public string? FilterDate { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);

        // ✅ FIX: Dropdown data from database
        public List<string> AvailableDates { get; set; } = new List<string>();
        public List<string> ProjectCodes { get; set; } = new List<string>();
        public List<string> EquipmentStations { get; set; } = new List<string>();
        public List<UserDropdownItem> DriEngineers { get; set; } = new List<UserDropdownItem>();
        public List<string> LineTargets { get; set; } = new List<string>();

        // Table Data
        public List<DailyConsumptionItem> Consumptions { get; set; } = new List<DailyConsumptionItem>();
    }

    public class UserDropdownItem
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class DailyConsumptionItem
    {
        public int Id { get; set; }
        public DateTime ConsumptionDate { get; set; }
        public string WorkingShift { get; set; } = string.Empty;
        public string ProjectCode { get; set; } = string.Empty;
        public string SpareDetails { get; set; } = string.Empty;
        public string EquipmentStation { get; set; } = string.Empty;
        public int QuantityUsed { get; set; }
        public string LineTarget { get; set; } = string.Empty;
        public string DriEngineer { get; set; } = string.Empty;
        public decimal? UnitCostINR { get; set; }
        public decimal? UnitCostCNY { get; set; }
        public decimal? TotalCostINR { get; set; }
        public decimal? TotalCostCNY { get; set; }
        public bool IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}