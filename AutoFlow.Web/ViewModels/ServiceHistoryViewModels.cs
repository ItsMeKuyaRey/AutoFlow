using AutoFlow.Web.Models;

namespace AutoFlow.Web.ViewModels;

public class ServiceHistoryItemViewModel
{
    public ServiceRecord ServiceRecord { get; set; } = new();
    public JobOrder? JobOrder { get; set; }

    public int ServiceRecordId => ServiceRecord.ServiceRecordId;
    public string CustomerName => ServiceRecord.Vehicle?.Customer == null
        ? "Customer unavailable"
        : $"{ServiceRecord.Vehicle.Customer.FirstName} {ServiceRecord.Vehicle.Customer.LastName}".Trim();
    public string? CustomerImageUrl => ServiceRecord.Vehicle?.Customer?.ImageUrl;
    public string VehicleName => ServiceRecord.Vehicle == null
        ? "Vehicle unavailable"
        : $"{ServiceRecord.Vehicle.Make} {ServiceRecord.Vehicle.Model}".Trim();
    public string? VehicleImageUrl => ServiceRecord.Vehicle?.ImageUrl;
    public string PlateNumber => ServiceRecord.Vehicle?.PlateNumber ?? "No plate";
    public string? VIN => ServiceRecord.Vehicle?.VIN;
    public string Status => string.IsNullOrWhiteSpace(ServiceRecord.Status) ? "Completed" : ServiceRecord.Status;
    public bool IsArchived => Status.Equals("Archived", StringComparison.OrdinalIgnoreCase);
}

public class ServiceHistoryPageViewModel
{
    public List<ServiceHistoryItemViewModel> Records { get; set; } = new();
    public List<ServiceHistoryItemViewModel> ArchivedRecords { get; set; } = new();
    public ServiceHistoryItemViewModel? DrawerRecord { get; set; }
    public string Open { get; set; } = string.Empty;
}
