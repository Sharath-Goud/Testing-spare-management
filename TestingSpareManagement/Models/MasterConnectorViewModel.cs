using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using TestingSpareManagement.Models;

namespace TestingSpareManagement.ViewModels
{
    public class MasterConnectorViewModel
    {
        public MasterConnector Connector { get; set; }
            = new MasterConnector();

        public List<MasterConnector> Connectors { get; set; }
            = new List<MasterConnector>();

        public IFormFile? PhotoFile { get; set; }

        public int CurrentPage { get; set; } = 1;

        public int PageSize { get; set; } = 25;

        public int TotalRecords { get; set; }

        public List<SelectListItem> Projects { get; set; } = new();

        public int TotalPages
        {
            get
            {
                if (TotalRecords <= 0)
                    return 1;

                return (int)Math.Ceiling(
                    TotalRecords / (double)PageSize);
            }
        }

        public int StartRecord
        {
            get
            {
                if (TotalRecords <= 0)
                    return 0;

                return ((CurrentPage - 1) * PageSize) + 1;
            }
        }

        public int EndRecord
        {
            get
            {
                if (TotalRecords <= 0)
                    return 0;

                return Math.Min(
                    CurrentPage * PageSize,
                    TotalRecords);
            }
        }
    }
}