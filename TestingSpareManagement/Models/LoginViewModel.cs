using System.ComponentModel.DataAnnotations;

namespace TestingSpareManagement.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Employee ID is required.")]
        [Display(Name = "Employee ID")]
        public string EmployeeId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}