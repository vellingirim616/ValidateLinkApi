namespace LinksApi.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly string _apiKey;
    private const string API_KEY_HEADER = "X-API-Key";

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _apiKey = configuration["ApiKey"] ?? "admin123";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for Swagger endpoints
        if (context.Request.Path.StartsWithSegments("/swagger") || 
            context.Request.Path.StartsWithSegments("/index.html"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(API_KEY_HEADER, out var extractedApiKey))
        {
            _logger.LogWarning("API Key is missing from request");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "API Key is missing",
                message = $"Please provide a valid API key in the '{API_KEY_HEADER}' header"
            });
            return;
        }

        if (!_apiKey.Equals(extractedApiKey))
        {
            _logger.LogWarning("Invalid API Key provided");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid API Key",
                message = "The provided API key is not valid"
            });
            return;
        }

        await _next(context);
    }
}
