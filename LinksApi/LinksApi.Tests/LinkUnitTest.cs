using LinksApi.Controllers;
using LinksApi.Models;
using LinksApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace LinksApi.Tests;

public class LinksControllerTests
{
    private static LinksController CreateController(
        Mock<IMongoDbService>? mongoDbMock = null,
        Mock<ILinkValidationService>? validationServiceMock = null)
    {
        mongoDbMock ??= new Mock<IMongoDbService>(MockBehavior.Strict);
        validationServiceMock ??= new Mock<ILinkValidationService>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<LinksController>>();

        return new LinksController(mongoDbMock.Object, validationServiceMock.Object, logger);
    }

    private static JsonElement ToJsonElement(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement;
    }

    // -------------------- POST /api/links --------------------

    [Fact]
    public async Task AddLinks_WhenLinksArrayEmpty_ReturnsBadRequest()
    {
        var mongoDbMock = new Mock<IMongoDbService>(MockBehavior.Strict);
        var validationServiceMock = new Mock<ILinkValidationService>(MockBehavior.Strict);
        var controller = CreateController(mongoDbMock, validationServiceMock);

        var result = await controller.AddLinks(new AddLinksRequest { Links = new List<string>() });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var body = ToJsonElement(badRequest.Value!);
        Assert.Equal("Links array cannot be empty", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AddLinks_WhenLinksProvided_InsertsAndReturnsOkWithCount()
    {
        var mongoDbMock = new Mock<IMongoDbService>(MockBehavior.Strict);
        var validationServiceMock = new Mock<ILinkValidationService>(MockBehavior.Strict);

        List<Link>? insertedLinks = null;
        mongoDbMock
            .Setup(m => m.InsertLinksAsync(It.IsAny<List<Link>>()))
            .Callback<List<Link>>(links => insertedLinks = links)
            .Returns(Task.CompletedTask);

        var controller = CreateController(mongoDbMock, validationServiceMock);

        var result = await controller.AddLinks(new AddLinksRequest
        {
            Links = new List<string> { "https://example.com", "https://contoso.com" }
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ToJsonElement(ok.Value!);

        Assert.Equal("Links added successfully", body.GetProperty("message").GetString());
        Assert.Equal(2, body.GetProperty("count").GetInt32());
        Assert.Equal(2, body.GetProperty("links").GetArrayLength());

        Assert.NotNull(insertedLinks);
        Assert.Equal(2, insertedLinks!.Count);
        Assert.All(insertedLinks, l => Assert.Equal("pending", l.Status));
        Assert.All(insertedLinks, l => Assert.Null(l.Reason));

        mongoDbMock.Verify(m => m.InsertLinksAsync(It.IsAny<List<Link>>()), Times.Once);
    }

    [Fact]
    public async Task AddLinks_SetsCreatedAndUpdatedTimestamps()
    {
        var mongoDbMock = new Mock<IMongoDbService>(MockBehavior.Strict);
        var validationServiceMock = new Mock<ILinkValidationService>(MockBehavior.Strict);

        List<Link>? insertedLinks = null;
        mongoDbMock
            .Setup(m => m.InsertLinksAsync(It.IsAny<List<Link>>()))
            .Callback<List<Link>>(links => insertedLinks = links)
            .Returns(Task.CompletedTask);

        var controller = CreateController(mongoDbMock, validationServiceMock);
        var before = DateTime.UtcNow;

        _ = await controller.AddLinks(new AddLinksRequest
        {
            Links = new List<string> { "https://example.com" }
        });

        var after = DateTime.UtcNow;

        Assert.NotNull(insertedLinks);
        Assert.Single(insertedLinks!);
        var link = insertedLinks![0];

        Assert.InRange(link.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.InRange(link.UpdatedAt, before.AddSeconds(-1), after.AddSeconds(1));

        mongoDbMock.Verify(m => m.InsertLinksAsync(It.IsAny<List<Link>>()), Times.Once);
    }

    // -------------------- POST /api/links/validate --------------------

    [Fact]
    public async Task ValidateLinks_ReturnsOkWithSummary()
    {
        var mongoDbMock = new Mock<IMongoDbService>(MockBehavior.Strict);
        var validationServiceMock = new Mock<ILinkValidationService>(MockBehavior.Strict);

        validationServiceMock
            .Setup(v => v.ValidateAllLinksAsync())
            .ReturnsAsync((TotalProcessed: 100, ValidCount: 80, BrokenCount: 20, Duration: TimeSpan.FromSeconds(2)));

        var controller = CreateController(mongoDbMock, validationServiceMock);

        var result = await controller.ValidateLinks();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ToJsonElement(ok.Value!);

        Assert.Equal("Validation completed successfully", body.GetProperty("message").GetString());
        Assert.Equal(100, body.GetProperty("totalProcessed").GetInt32());
        Assert.Equal(80, body.GetProperty("validLinks").GetInt32());
        Assert.Equal(20, body.GetProperty("brokenLinks").GetInt32());

        validationServiceMock.Verify(v => v.ValidateAllLinksAsync(), Times.Once);
    }

    [Fact]
    public async Task ValidateLinks_IncludesDurationFields()
    {
        var mongoDbMock = new Mock<IMongoDbService>(MockBehavior.Strict);
        var validationServiceMock = new Mock<ILinkValidationService>(MockBehavior.Strict);

        validationServiceMock
            .Setup(v => v.ValidateAllLinksAsync())
            .ReturnsAsync((TotalProcessed: 1, ValidCount: 1, BrokenCount: 0, Duration: TimeSpan.FromMilliseconds(1234)));

        var controller = CreateController(mongoDbMock, validationServiceMock);

        var result = await controller.ValidateLinks();
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ToJsonElement(ok.Value!);

        Assert.True(body.TryGetProperty("durationMs", out _));
        Assert.True(body.TryGetProperty("durationSeconds", out _));
    }

    // -------------------- GET /api/links/broken --------------------

    [Fact]
    public async Task GetBrokenLinks_WhenPageIsLessThanOne_ReturnsBadRequest()
    {
        var mongoDbMock = new Mock<IMongoDbService>(MockBehavior.Strict);
        var validationServiceMock = new Mock<ILinkValidationService>(MockBehavior.Strict);
        var controller = CreateController(mongoDbMock, validationServiceMock);

        var result = await controller.GetBrokenLinks(page: 0, pageSize: 500);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var body = ToJsonElement(badRequest.Value!);
        Assert.Equal("Page must be greater than 0", body.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public async Task GetBrokenLinks_WhenPageSizeOutOfRange_ReturnsBadRequest(int pageSize)
    {
        var mongoDbMock = new Mock<IMongoDbService>(MockBehavior.Strict);
        var validationServiceMock = new Mock<ILinkValidationService>(MockBehavior.Strict);
        var controller = CreateController(mongoDbMock, validationServiceMock);

        var result = await controller.GetBrokenLinks(page: 1, pageSize: pageSize);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var body = ToJsonElement(badRequest.Value!);
        Assert.Equal("PageSize must be between 1 and 10000", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetBrokenLinks_WhenValidRequest_ReturnsPaginatedResponse()
    {
        var mongoDbMock = new Mock<IMongoDbService>(MockBehavior.Strict);
        var validationServiceMock = new Mock<ILinkValidationService>(MockBehavior.Strict);

        var brokenLinks = new List<Link>
        {
            new Link { Id = "1", Links = "https://bad.example/1", Status = "broken", Reason = "HTTP 404", UpdatedAt = DateTime.UtcNow },
            new Link { Id = "2", Links = "https://bad.example/2", Status = "broken", Reason = "Timeout", UpdatedAt = DateTime.UtcNow },
        };

        mongoDbMock
            .Setup(m => m.GetBrokenLinksPaginatedAsync(1, 2))
            .ReturnsAsync((brokenLinks, TotalCount: 3));

        var controller = CreateController(mongoDbMock, validationServiceMock);

        var result = await controller.GetBrokenLinks(page: 1, pageSize: 2);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ToJsonElement(ok.Value!);

        var pagination = body.GetProperty("pagination");
        Assert.Equal(1, pagination.GetProperty("currentPage").GetInt32());
        Assert.Equal(2, pagination.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, pagination.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, pagination.GetProperty("totalPages").GetInt32());
        Assert.True(pagination.GetProperty("hasNextPage").GetBoolean());
        Assert.False(pagination.GetProperty("hasPreviousPage").GetBoolean());

        Assert.Equal(2, body.GetProperty("brokenLinks").GetArrayLength());
        mongoDbMock.Verify(m => m.GetBrokenLinksPaginatedAsync(1, 2), Times.Once);
    }
}
