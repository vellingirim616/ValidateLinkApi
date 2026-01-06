using LinksApi.Models;

namespace LinksApi.Services;

public interface IMongoDbService
{
    Task<List<Link>> GetAllLinksAsync();
    Task<List<Link>> GetBrokenLinksAsync();
    Task<(List<Link> Links, long TotalCount)> GetBrokenLinksPaginatedAsync(int page, int pageSize);
    Task<long> GetTotalLinksCountAsync();
    Task<long> GetLinksCountByStatusAsync(string status);
    Task<List<Link>> GetLinksBatchAsync(int skip, int limit);
    Task<List<Link>> GetLinksBatchByStatusAsync(string status, int skip, int limit);
    Task InsertLinksAsync(List<Link> links);
    Task UpdateLinksBatchAsync(List<Link> links);
}
