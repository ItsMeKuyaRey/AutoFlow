namespace AutoFlow.Web.ViewModels;

public class DashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int ActiveJobCount { get; set; }
    public int PendingAppointmentCount { get; set; }
    public int LowStockAlertCount { get; set; }
    public int TotalJobCount { get; set; }
    public int TodayJobCount { get; set; }
    public int RescheduledAppointmentCount { get; set; }
    public List<JobStatusCount> JobStatusCounts { get; set; } = new();
    public List<RecentJobDto> RecentJobs { get; set; } = new();
    public List<FollowUpDto> FollowUps { get; set; } = new();
    public int ActiveBayCount { get; set; }
    public int TotalBays { get; set; } = 4;
}

public class JobStatusCount
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class RecentJobDto
{
    public int JobOrderId { get; set; }
    public string VehicleMake { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalCost { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class FollowUpDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string VehicleInfo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AvatarInitials { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "blue";
}
