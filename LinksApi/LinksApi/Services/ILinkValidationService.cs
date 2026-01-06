namespace LinksApi.Services;

public interface ILinkValidationService
{
    Task<(int TotalProcessed, int ValidCount, int BrokenCount, TimeSpan Duration)> ValidateAllLinksAsync();
}
