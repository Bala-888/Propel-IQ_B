namespace Api.Data.Entities;

public class InsuranceRecord
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public DateOnly CoverageStart { get; set; }
    public DateOnly? CoverageEnd { get; set; }

    public Patient Patient { get; set; } = null!;
}
