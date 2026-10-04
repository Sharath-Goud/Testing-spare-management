using Microsoft.AspNetCore.Http;
using TestingSpareManagement.Models;

namespace TestingSpareManagement.ViewModels
{
    public class MasterConnectorViewModel
    {
        // =====================================================
        // CONNECTOR FORM
        // =====================================================

        public MasterConnector Connector { get; set; }
            = new MasterConnector();


        // =====================================================
        // CONNECTOR REPORT - CURRENT PAGE DATA
        // =====================================================

        public List<MasterConnector> Connectors { get; set; }
            = new List<MasterConnector>();


        // =====================================================
        // PHOTO UPLOAD
        // =====================================================

        public IFormFile? PhotoFile { get; set; }


        // =====================================================
        // PAGINATION
        // =====================================================

        /// <summary>
        /// Current page number.
        /// Example: 1, 2, 3...
        /// </summary>
        public int CurrentPage { get; set; } = 1;


        /// <summary>
        /// Number of records displayed per page.
        /// Default = 25.
        /// </summary>
        public int PageSize { get; set; } = 25;


        /// <summary>
        /// Total number of records available in database.
        /// </summary>
        public int TotalRecords { get; set; }


        /// <summary>
        /// Total number of pages.
        /// </summary>
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


        /// <summary>
        /// First record number displayed on current page.
        /// Example:
        /// Page 1, PageSize 25 => 1
        /// Page 2, PageSize 25 => 26
        /// </summary>
        public int StartRecord
        {
            get
            {
                if (TotalRecords <= 0)
                    return 0;

                return ((CurrentPage - 1) * PageSize) + 1;
            }
        }


        /// <summary>
        /// Last record number displayed on current page.
        /// </summary>
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