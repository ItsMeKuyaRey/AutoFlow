namespace AutoFlow.Web.ViewModels;

public class DashboardViewModel
{
    // Financial overview
    public decimal TotalRevenue { get; set; }
    public decimal TotalBilled { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal CollectionRate { get; set; }
    public int TotalBillingCount { get; set; }
    public int OutstandingInvoiceCount { get; set; }

    // Core records
    public int TotalCustomerCount { get; set; }
    public int TotalVehicleCount { get; set; }
    public int TotalAppointmentCount { get; set; }
    public int TotalJobCount { get; set; }
    public int TotalPartCount { get; set; }
    public int TotalServiceRecordCount { get; set; }

    // Operational metrics
    public int ActiveJobCount { get; set; }
    public int PendingAppointmentCount { get; set; }
    public int LowStockAlertCount { get; set; }
    public int TodayJobCount { get; set; }
    public int TodayAppointmentCount { get; set; }
    public int RescheduledAppointmentCount { get; set; }

    // Job status metrics
    public int CompletedJobCount { get; set; }
    public int OpenJobCount { get; set; }
    public int InProgressJobCount { get; set; }

    // Reporting metrics
    public int ScheduledThisWeek { get; set; }
    public int CompletedThisMonth { get; set; }

    // Inventory
    public decimal InventoryValue { get; set; }
    public int TotalStockQuantity { get; set; }
    public string TopMovingCategory { get; set; } = "Lubricants & Fluids";
    public decimal TopMovingCategoryPercentage { get; set; }

    // Service bays
    public int ActiveBayCount { get; set; }
    public int TotalBays { get; set; } = 4;

    // Dashboard collections
    public List<JobStatusCount> JobStatusCounts { get; set; } = new();
    public List<RecentJobDto> RecentJobs { get; set; } = new();
    public List<FollowUpDto> FollowUps { get; set; } = new();

    public List<TodayAppointmentDto> TodayAppointments { get; set; } = new();
    public List<ActivityFeedDto> RecentActivities { get; set; } = new();
    public List<ReportSnapshotDto> ReportSnapshot { get; set; } = new();

    public List<DashboardTrendPointDto> AppointmentTrend { get; set; } = new();
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

public class TodayAppointmentDto
{
    public int AppointmentId { get; set; }

    // Full appointment date used by the dashboard
    public DateTime AppointmentDate { get; set; }

    // Display time used by some dashboard sections
    public string Time { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string VehicleInfo { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Concern { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ActivityFeedDto
{
    public string ActivityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public string ActivityColor { get; set; } = "purple";
}

public class ReportSnapshotDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
}

public class DashboardTrendPointDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}