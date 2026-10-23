namespace TestingSpareManagement.Models
{
    public class MasterConnector
    {
        public int Id { get; set; }

        public string StationProfile { get; set; } = string.Empty;

        public string ConnectorCodeType { get; set; } = string.Empty;

        public string DescriptionPartNo { get; set; } = string.Empty;

        public string? PhotoPath { get; set; }

        public string? LifeCycleLimit { get; set; }

        public int BaseStock { get; set; }

        public int AlertThreshold { get; set; }

        public decimal UnitCostINR { get; set; }

        public decimal? UnitCostCNY { get; set; }

        public bool IsActive { get; set; }

        public string? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        public string? StockStatus { get; set; }
        public string? ProjectCode { get; set; }
    }
}