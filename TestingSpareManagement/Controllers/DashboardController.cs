using Microsoft.AspNetCore.Mvc;
using TestingSpareManagement.ViewModels;

namespace TestingSpareManagement.Controllers
{
    public class DashboardController : Controller
    {

        public IActionResult Index()
        {

            DashboardViewModel model = new DashboardViewModel
            {
                TotalConnectors = 6,
                StockOutAlerts = 0,
                PendingRequests = 0,
                YtdSpend = 0
            };


            return View(model);

        }

    }
}