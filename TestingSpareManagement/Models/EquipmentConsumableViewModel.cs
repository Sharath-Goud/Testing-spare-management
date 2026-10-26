using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TestingSpareManagement.Models
{
    public class EquipmentConsumableViewModel
    {
        public int Id { get; set; }

        [Required]
        public string ProjectCode { get; set; } = string.Empty;

        [Required]
        public string TestItemName { get; set; } = string.Empty;

        public string? PartNumber { get; set; }

        public string? BrandManufacturer { get; set; }

        [Required]
        public string TargetStation { get; set; } = string.Empty;

        [Required]
        public string StorageLocation { get; set; } = string.Empty;

        [Required]
        [Range(0, int.MaxValue)]
        public int AlertThresholdMin { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int AvailableQuantity { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal UnitPriceINR { get; set; }

        public int? SpecCyclesLimit { get; set; }

        public IFormFile? PhotoFile { get; set; }

        public List<EquipmentItem> Equipments { get; set; }
            = new List<EquipmentItem>();

        public List<string> TestingStations { get; set; }
            = new List<string>();
    }

    public class EquipmentItem
    {
        public int Id { get; set; }

        public string? PhotoPath { get; set; }

        public string ProjectCode { get; set; } = string.Empty;

        public string TestItemName { get; set; } = string.Empty;

        public string? PartNumber { get; set; }

        public string? BrandManufacturer { get; set; }

        public string AssignedEquipment { get; set; } = string.Empty;

        public string StorageLocation { get; set; } = string.Empty;

        public int AvailableQuantity { get; set; }

        public int AlertThreshold { get; set; }

        public decimal UnitPriceINR { get; set; }

        public decimal UnitPriceRMB { get; set; }

        public int SpecCycles { get; set; }

        public string Status =>
            AvailableQuantity <= AlertThreshold ? "LOW STOCK" : "SAFE";

        public bool IsActive { get; set; }

        public string? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }
    }
}