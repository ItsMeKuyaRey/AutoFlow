namespace AutoFlow.Web.ViewModels;

public class ReportsViewModel
{
    public decimal TotalSalesToday { get; set; }
    public decimal TotalSalesThisMonth { get; set; }
    public decimal OutstandingPayments { get; set; }

    public int CompletedRepairs { get; set; }
    public int ActiveRepairJobs { get; set; }
    public int TotalAppointments { get; set; }

    public int TotalParts { get; set; }
    public int LowStockParts { get; set; }

    public List<ReportRowViewModel> DailySales { get; set; } = new();
    public List<ReportRowViewModel> MonthlySales { get; set; } = new();
    public List<ReportRowViewModel> CompletedRepairRows { get; set; } = new();
    public List<ReportRowViewModel> ActiveRepairRows { get; set; } = new();
    public List<ReportRowViewModel> AppointmentRows { get; set; } = new();
    public List<ReportRowViewModel> LowStockPartRows { get; set; } = new();
}

public class ReportRowViewModel
{
    public string Label { get; set; } = string.Empty;
    public string SecondaryLabel { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int Count { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? Date { get; set; }
}