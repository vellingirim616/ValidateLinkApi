using LinksApi.Models;
using LinksApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinksApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LinksController : ControllerBase
{
    private readonly IMongoDbService _mongoDbService;
    private readonly ILinkValidationService _validationService;
    private readonly ILogger<LinksController> _logger;

    public LinksController(
        IMongoDbService mongoDbService,
        ILinkValidationService validationService,
        ILogger<LinksController> logger)
    {
        _mongoDbService = mongoDbService;
        _validationService = validationService;
        _logger = logger;
    }

    /// <summary>
    /// Add multiple links to the database
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddLinks([FromBody] AddLinksRequest request)
    {
        if (request.Links == null || !request.Links.Any())
        {
            return BadRequest(new { error = "Links array cannot be empty" });
        }

        var links = request.Links.Select(url => new Link
        {
            Links = url,
            Status = "pending",
            Reason = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        await _mongoDbService.InsertLinksAsync(links);

        _logger.LogInformation("Added {Count} links to database", links.Count);

        return Ok(new
        {
            message = "Links added successfully",
            count = links.Count,
            links = links.Select(l => new { l.Id, l.Links, l.Status })
        });
    }

    /// <summary>
    /// Trigger validation of all links in the database
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateLinks()
    {
        _logger.LogInformation("Validation endpoint triggered");

        var result = await _validationService.ValidateAllLinksAsync();

        return Ok(new
        {
            message = "Validation completed successfully",
            totalProcessed = result.TotalProcessed,
            validLinks = result.ValidCount,
            brokenLinks = result.BrokenCount,
            durationMs = result.Duration.TotalMilliseconds,
            durationSeconds = result.Duration.TotalSeconds
        });
    }

    /// <summary>
    /// Get all broken links with details (paginated)
    /// </summary>
    [HttpGet("broken")]
    public async Task<IActionResult> GetBrokenLinks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 500)
    {
        // Validate pagination parameters
        if (page < 1)
        {
            return BadRequest(new { error = "Page must be greater than 0" });
        }

        if (pageSize < 1 || pageSize > 10000)
        {
            return BadRequest(new { error = "PageSize must be between 1 and 10000" });
        }

        var (brokenLinks, totalCount) = await _mongoDbService.GetBrokenLinksPaginatedAsync(page, pageSize);

        var response = brokenLinks.Select(link => new BrokenLinkResponse
        {
            Id = link.Id ?? string.Empty,
            Link = link.Links,
            Reason = link.Reason ?? "Unknown",
            LastValidated = link.UpdatedAt
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        _logger.LogInformation("Retrieved {Count} broken links (Page {Page}/{TotalPages})", 
            response.Count, page, totalPages);

        return Ok(new
        {
            pagination = new
            {
                currentPage = page,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = totalPages,
                hasNextPage = page < totalPages,
                hasPreviousPage = page > 1
            },
            brokenLinks = response
        });
    }
}
