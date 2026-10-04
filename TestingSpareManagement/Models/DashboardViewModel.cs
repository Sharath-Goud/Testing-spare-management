namespace TestingSpareManagement.ViewModels
{
    public class DashboardViewModel
    {

        public int TotalConnectors { get; set; }

        public int StockOutAlerts { get; set; }

        public int PendingRequests { get; set; }

        public decimal YtdSpend { get; set; }

    }
}