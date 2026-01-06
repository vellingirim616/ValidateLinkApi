using LinksApi.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace LinksApi.Services;

public class LinkValidationService : ILinkValidationService
{
    private readonly IMongoDbService _mongoDbService;
    private readonly ValidationSettings _validationSettings;
    private readonly ILogger<LinkValidationService> _logger;
    private readonly HttpClient _httpClient;

    public LinkValidationService(
        IMongoDbService mongoDbService,
        IOptions<ValidationSettings> validationSettings,
        ILogger<LinkValidationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _mongoDbService = mongoDbService;
        _validationSettings = validationSettings.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("LinkValidator");
        _httpClient.Timeout = TimeSpan.FromSeconds(_validationSettings.TimeoutSeconds);
    }

    public async Task<(int TotalProcessed, int ValidCount, int BrokenCount, TimeSpan Duration)> ValidateAllLinksAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("Starting link validation process");

        // Validate only pending links
        var totalCount = await _mongoDbService.GetLinksCountByStatusAsync("pending");
        var batchCount = (int)Math.Ceiling((double)totalCount / _validationSettings.BatchSize);

        _logger.LogInformation("Total pending links: {TotalCount}, Batch size: {BatchSize}, Estimated number of batches: {BatchCount}",
            totalCount, _validationSettings.BatchSize, batchCount);

        if (totalCount == 0)
        {
            stopwatch.Stop();
            _logger.LogInformation("No pending links to validate");
            return (0, 0, 0, stopwatch.Elapsed);
        }

        int totalProcessed = 0;
        int totalValid = 0;
        int totalBroken = 0;

        // Process batches until there are no pending links left.
        // Note: Avoid Skip-based paging here because the status of documents changes from 'pending' to 'valid'/'broken'
        // during processing, which would cause Skip to skip remaining pending documents.
        var batchIndex = 0;
        while (true)
        {
            var batchLinks = await _mongoDbService.GetLinksBatchByStatusAsync("pending", skip: 0, limit: _validationSettings.BatchSize);

            if (!batchLinks.Any())
                break;

            batchIndex++;

            _logger.LogInformation("Processing batch {BatchIndex}/{TotalBatches} with {LinkCount} links",
                batchIndex, batchCount, batchLinks.Count);

            // Validate links in parallel within the batch
            var validatedLinks = await ValidateLinksBatchAsync(batchLinks);

            // Update database for the entire batch
            await _mongoDbService.UpdateLinksBatchAsync(validatedLinks);

            var validCount = validatedLinks.Count(l => l.Status == "valid");
            var brokenCount = validatedLinks.Count(l => l.Status == "broken");

            totalProcessed += validatedLinks.Count;
            totalValid += validCount;
            totalBroken += brokenCount;

            _logger.LogInformation("Batch {BatchIndex} completed: {Valid} valid, {Broken} broken",
                batchIndex, validCount, brokenCount);
        }

        stopwatch.Stop();
        
        _logger.LogInformation("Validation completed: {TotalProcessed} links processed in {Duration}ms. Valid: {Valid}, Broken: {Broken}",
            totalProcessed, stopwatch.ElapsedMilliseconds, totalValid, totalBroken);

        return (totalProcessed, totalValid, totalBroken, stopwatch.Elapsed);
    }

    private async Task<List<Link>> ValidateLinksBatchAsync(List<Link> links)
    {
        var validatedLinks = new ConcurrentBag<Link>();
        
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _validationSettings.MaxDegreeOfParallelism
        };

        await Parallel.ForEachAsync(links, parallelOptions, async (link, cancellationToken) =>
        {
            var validationResult = await ValidateLinkWithRetryAsync(link.Links, cancellationToken);
            
            link.Status = validationResult.IsValid ? "valid" : "broken";
            link.Reason = validationResult.Reason;
            link.UpdatedAt = DateTime.UtcNow;
            
            validatedLinks.Add(link);
        });

        return validatedLinks.ToList();
    }

    private async Task<(bool IsValid, string? Reason)> ValidateLinkWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        int retries = 0;
        Exception? lastException = null;

        while (retries <= _validationSettings.MaxRetries)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "valid");
                }
                else if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    // Try GET request if HEAD is not allowed
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return (true, "valid");
                    }
                }

                // Handle redirects (should be automatic, but documenting)
                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                {
                    return (true, "valid");
                }

                return (false, $"HTTP {(int)response.StatusCode} - {response.StatusCode}");
            }
            catch (TaskCanceledException)
            {
                lastException = new TimeoutException($"Request timeout after {_validationSettings.TimeoutSeconds} seconds");
                retries++;
                
                if (retries <= _validationSettings.MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * retries), cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                
                // Check for specific network errors
                if (ex.InnerException is SocketException socketEx)
                {
                    return (false, $"Network error: {socketEx.Message}");
                }
                
                retries++;
                
                if (retries <= _validationSettings.MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * retries), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error validating URL: {Url}", url);
                return (false, $"Error: {ex.Message}");
            }
        }

        // All retries exhausted
        if (lastException is TimeoutException)
        {
            return (false, "Timeout - Request exceeded timeout limit");
        }

        return (false, $"Failed after {_validationSettings.MaxRetries} retries: {lastException?.Message ?? "Unknown error"}");
    }
}
