using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestingSpareManagement.Models
{
    public class MaterialRequestViewModel
    {
        // Form Fields
        [Required]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Target Project Code")]
        public string ProjectCode { get; set; } = "A246F";

        [Required]
        [Display(Name = "Spare / Connector Description")]
        [StringLength(250)]
        public string SpareDescription { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Part Number")]
        [StringLength(100)]
        public string PartNumber { get; set; } = string.Empty;

        [Display(Name = "Brand / Manufacturer")]
        [StringLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Machine / Target Station")]
        public string TargetMachine { get; set; } = "OS";

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        [Display(Name = "Requested Quantity")]
        public int RequestedQuantity { get; set; }

        [Required]
        [Display(Name = "Requirement Priority Level")]
        public string PriorityLevel { get; set; } = "Normal Reorder";

        [Display(Name = "Justification / Reason")]
        [StringLength(500)]
        public string Justification { get; set; } = string.Empty;

        // For Edit / Delete
        public int Id { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);

        // Table Data
        public List<MaterialRequestItem> Requests { get; set; } = new List<MaterialRequestItem>();

        public List<SelectListItem> ProjectCodes { get; set; } = new List<SelectListItem>();
        public List<string> TestingStations { get; set; } = new List<string>();
    }


    public class MaterialRequestItem
    {
        public int Id { get; set; }
        public string ReqId { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public string SpareDescription { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string TargetMachine { get; set; } = string.Empty;
        public int RequestedQuantity { get; set; }
        public string PriorityLevel { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string AdminActions { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}