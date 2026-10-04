using Microsoft.AspNetCore.Identity;

namespace TestingSpareManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public string Designation { get; set; }

    }
}