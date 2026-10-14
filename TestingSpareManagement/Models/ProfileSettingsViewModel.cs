using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestingSpareManagement.Models
{
    public class ProfileSettingsViewModel
    {
        public UserProfileModel UserProfile { get; set; } = new UserProfileModel();

        public List<SystemUserModel> SystemUsers { get; set; } = new();

        public List<TestingStationModel> TestingStations { get; set; } = new();

        // ⚠️ Uses ProjectMasterModel from PrPoTrackerViewModel.cs
        public List<ProjectMasterModel> Projects { get; set; } = new();

        public List<BuyerRosterModel> Buyers { get; set; } = new();

        public List<YearlyBudgetModel> YearlyBudgets { get; set; } = new();

        public SystemUserFormModel NewUser { get; set; } = new();
        public string? NewStationCode { get; set; }
        public string? NewProjectCode { get; set; }
        public string? NewBuyerName { get; set; }

        public string SelectedTheme { get; set; } = "Dark Charcoal";
    }

    public class UserProfileModel
    {
        public int Id { get; set; }
        public string LoginId { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string ClearanceTier { get; set; } = string.Empty;
        public string ThemeColor { get; set; } = "Dark Charcoal";
        public string? AvatarPath { get; set; }
    }

    public class SystemUserModel
    {
        public int Id { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string OfficialEmail { get; set; } = string.Empty;
        public string AssignedRole { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class SystemUserFormModel
    {
        [Required]
        public string EmployeeId { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? Password { get; set; }

        [Required]
        [EmailAddress]
        public string OfficialEmail { get; set; } = string.Empty;

        [Required]
        public string AssignedRole { get; set; } = string.Empty;
    }

    public class TestingStationModel
    {
        public int Id { get; set; }
        public string StationCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    // ❌ REMOVED ProjectMasterModel - Already exists in PrPoTrackerViewModel.cs

    public class BuyerRosterModel
    {
        public int Id { get; set; }
        public string BuyerName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class YearlyBudgetModel
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public decimal AllocatedBudget { get; set; }
        public decimal BudgetUtilized { get; set; }
        public decimal CostSaved { get; set; }
    }
}