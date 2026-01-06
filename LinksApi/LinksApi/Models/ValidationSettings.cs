namespace LinksApi.Models;

public class ValidationSettings
{
    public int BatchSize { get; set; } = 1000;
    public int MaxDegreeOfParallelism { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 5;
    public int MaxRetries { get; set; } = 2;
}
