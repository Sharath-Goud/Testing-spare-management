
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace TestingSpareManagement.ViewComponents
{
    public class PendingMaterialRequestsViewComponent : ViewComponent
    {
        private readonly IConfiguration _configuration;

        public PendingMaterialRequestsViewComponent(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            string connectionString =
                _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection was not found.");

            int pendingCount = 0;

            using var connection =
                new SqlConnection(connectionString);

            string query = @"
                SELECT COUNT(1)
                FROM dbo.MaterialRequest
                WHERE IsActive = 1
                  AND (
                      Status IN ('Pending', 'Pending Admin Approval')
                      OR Status IS NULL
                      OR LTRIM(RTRIM(Status)) = ''
                  );";

            using var command =
                new SqlCommand(query, connection);

            await connection.OpenAsync();

            pendingCount = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            return View(pendingCount);
        }
    }
}
