namespace LinksApi.Models;

public class BrokenLinkResponse
{
    public string Id { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime LastValidated { get; set; }
}
