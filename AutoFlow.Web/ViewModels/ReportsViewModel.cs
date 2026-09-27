namespace AutoFlow.Web.ViewModels;

public class ReportsViewModel
{
    public string SelectedReport { get; set; } = "Monthly Sales";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public string ReportDescription { get; set; } = string.Empty;
    public string ChartTitle { get; set; } = string.Empty;
    public string ChartSubtitle { get; set; } = string.Empty;
    public string SummaryLabel { get; set; } = "Total Sales";

    public decimal TotalSalesToday { get; set; }
    public decimal TotalSalesThisMonth { get; set; }
    public decimal OutstandingPayments { get; set; }

    public int CompletedRepairs { get; set; }
    public int ActiveRepairJobs { get; set; }
    public int TotalAppointments { get; set; }

    public int TotalParts { get; set; }
    public int LowStockParts { get; set; }

    public decimal ReportTotal { get; set; }
    public decimal LaborRevenue { get; set; }
    public decimal PartsRevenue { get; set; }
    public int ReportTransactionCount { get; set; }
    public decimal AverageTicket { get; set; }

    public List<string> ChartLabels { get; set; } = new();
    public List<decimal> ChartValues { get; set; } = new();

    public List<ReportRowViewModel> ReportRows { get; set; } = new();

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
    public string Reference { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string Vehicle { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public decimal LaborAmount { get; set; }
    public decimal PartsAmount { get; set; }

    public int Count { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? Date { get; set; }
}
