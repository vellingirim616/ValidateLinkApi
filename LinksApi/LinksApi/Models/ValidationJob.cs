namespace LinksApi.Models;

public class ValidationJob
{
    public string JobId { get; set; } = Guid.NewGuid().ToString();
    public string Status { get; set; } = "queued"; // queued, running, completed, failed
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalLinks { get; set; }
    public int ProcessedLinks { get; set; }
    public int ValidLinks { get; set; }
    public int BrokenLinks { get; set; }
    public int CurrentBatch { get; set; }
    public int TotalBatches { get; set; }
    public double ProgressPercentage => TotalLinks > 0 ? (ProcessedLinks * 100.0 / TotalLinks) : 0;
    public string? ErrorMessage { get; set; }
}
