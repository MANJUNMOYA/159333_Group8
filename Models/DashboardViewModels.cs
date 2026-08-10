namespace CampusCoffeeSystem.Models;

public class CustomerDashboardViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<CustomerOrder> Orders { get; set; } = [];
}

public class MerchantDashboardViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<Product> Products { get; set; } = [];
    public IReadOnlyList<CustomerOrder> Orders { get; set; } = [];
    public decimal TodayRevenue { get; set; }
    public int TodayOrderCount { get; set; }
}

public class AdministratorDashboardViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<AdministratorUserViewModel> Users { get; set; } = [];
    public IReadOnlyList<MerchantApplication> MerchantApplications { get; set; } = [];
    public IReadOnlyList<Product> Products { get; set; } = [];
    public IReadOnlyList<CustomerOrder> Orders { get; set; } = [];
}

public class AdministratorUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

