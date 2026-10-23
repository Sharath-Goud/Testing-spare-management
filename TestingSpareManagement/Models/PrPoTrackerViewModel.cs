
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestingSpareManagement.Models
{
    
    public class PrPoTrackerViewModel
    {
        public int ActiveYear { get; set; } = DateTime.Now.Year;

        [Required(ErrorMessage = "Requisition Date is required")]
        public DateTime RequisitionDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Project Code is required")]
        public string? ProjectCode { get; set; }

        [Required(ErrorMessage = "PR Number is required")]
        public string? PrNumber { get; set; }

        [Required(ErrorMessage = "PO Status is required")]
        public string? PoStatus { get; set; }

        [Required(ErrorMessage = "Purchase Origin is required")]
        public string? PurchaseOriginRegion { get; set; }

        [Required(ErrorMessage = "Buyer is required")]
        public string? BuyerName { get; set; }

        [Required(ErrorMessage = "Asset Target is required")]
        public string? AssetTarget { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit Price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        public decimal TaxPercent { get; set; } = 0;

        public string? ShipmentTrackingNo { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string? ShipmentStatus { get; set; }
        public string? Remarks { get; set; }

        public List<ProjectMasterModel> Projects { get; set; } = new List<ProjectMasterModel>();
        public List<BuyerMasterModel> Buyers { get; set; } = new List<BuyerMasterModel>();
        public List<OriginMasterModel> Origins { get; set; } = new List<OriginMasterModel>();
        public List<PoStatusMasterModel> PoStatuses { get; set; } = new List<PoStatusMasterModel>();
        public List<ShipmentStatusMasterModel> ShipmentStatuses { get; set; } = new List<ShipmentStatusMasterModel>();

        public List<PrPoItem> Records { get; set; } = new List<PrPoItem>();
    }

    public class ProjectMasterModel
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class BuyerMasterModel
    {
        public int Id { get; set; }
        public string BuyerName { get; set; } = "";
    }

    public class OriginMasterModel
    {
        public int Id { get; set; }
        public string OriginName { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public decimal TaxPercent { get; set; }
        public string CurrencySymbol { get; set; } = "₹";
    }

    public class PoStatusMasterModel
    {
        public int Id { get; set; }
        public string StatusName { get; set; } = "";
    }

    public class ShipmentStatusMasterModel
    {
        public int Id { get; set; }
        public string StatusName { get; set; } = "";
    }

    public class PrPoItem
    {
        public int Id { get; set; }

        // Material Request details
        public int? MaterialRequestId { get; set; }
        public string? ReqId { get; set; }
        public string? RequestedBy { get; set; }
        public string? SpareDescription { get; set; }
        public string? PartNumber { get; set; }
        public string? Brand { get; set; }
        public string? TargetMachine { get; set; }
        public string? PriorityLevel { get; set; }
        public string? Justification { get; set; }

        public DateTime RequisitionDate { get; set; }
        public string ProjectCode { get; set; } = "";
        public string PrNumber { get; set; } = "";
        public string PoStatus { get; set; } = "";
        public string PurchaseOriginRegion { get; set; } = "";
        public string BuyerName { get; set; } = "";
        public string AssetTarget { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxPercent { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalCost { get; set; }
        public string? ShipmentTrackingNo { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string? ShipmentStatus { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}