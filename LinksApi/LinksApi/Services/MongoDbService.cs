using LinksApi.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LinksApi.Services;

public class MongoDbService : IMongoDbService
{
    private readonly IMongoCollection<Link> _linksCollection;
    private readonly ILogger<MongoDbService> _logger;

    public MongoDbService(IOptions<MongoDbSettings> settings, ILogger<MongoDbService> logger)
    {
        _logger = logger;
        
        var mongoClient = new MongoClient(settings.Value.ConnectionString);
        var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
        _linksCollection = database.GetCollection<Link>(settings.Value.CollectionName);
        
        _logger.LogInformation("MongoDB connection established to {Database}/{Collection}", 
            settings.Value.DatabaseName, settings.Value.CollectionName);
    }

    public async Task<List<Link>> GetAllLinksAsync()
    {
        return await _linksCollection.Find(_ => true).ToListAsync();
    }

    public async Task<List<Link>> GetBrokenLinksAsync()
    {
        var filter = Builders<Link>.Filter.Eq(l => l.Status, "broken");
        return await _linksCollection.Find(filter).ToListAsync();
    }

    public async Task<(List<Link> Links, long TotalCount)> GetBrokenLinksPaginatedAsync(int page, int pageSize)
    {
        var filter = Builders<Link>.Filter.Eq(l => l.Status, "broken");
        
        var totalCount = await _linksCollection.CountDocumentsAsync(filter);
        
        var links = await _linksCollection
            .Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (links, totalCount);
    }

    public async Task<long> GetTotalLinksCountAsync()
    {
        return await _linksCollection.CountDocumentsAsync(_ => true);
    }

    public async Task<long> GetLinksCountByStatusAsync(string status)
    {
        var filter = Builders<Link>.Filter.Eq(l => l.Status, status);
        return await _linksCollection.CountDocumentsAsync(filter);
    }

    public async Task<List<Link>> GetLinksBatchAsync(int skip, int limit)
    {
        return await _linksCollection
            .Find(_ => true)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Link>> GetLinksBatchByStatusAsync(string status, int skip, int limit)
    {
        var filter = Builders<Link>.Filter.Eq(l => l.Status, status);

        return await _linksCollection
            .Find(filter)
            .SortBy(l => l.Id)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task InsertLinksAsync(List<Link> links)
    {
        if (links.Any())
        {
            await _linksCollection.InsertManyAsync(links);
            _logger.LogInformation("Inserted {Count} links into database", links.Count);
        }
    }

    public async Task UpdateLinksBatchAsync(List<Link> links)
    {
        if (!links.Any()) return;

        var bulkOps = links.Select(link =>
        {
            var filter = Builders<Link>.Filter.Eq(l => l.Id, link.Id);
            var update = Builders<Link>.Update
                .Set(l => l.Status, link.Status)
                .Set(l => l.Reason, link.Reason)
                .Set(l => l.UpdatedAt, link.UpdatedAt);
            
            return new UpdateOneModel<Link>(filter, update);
        }).ToList();

        var result = await _linksCollection.BulkWriteAsync(bulkOps);
        
        _logger.LogInformation("Updated {Count} links in database", result.ModifiedCount);
    }
}
