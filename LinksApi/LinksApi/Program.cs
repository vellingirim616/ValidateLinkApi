using LinksApi.Middleware;
using LinksApi.Models;
using LinksApi.Services;
using Microsoft.OpenApi.Models;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Starting Links API application");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Configure MongoDB settings
    builder.Services.Configure<MongoDbSettings>(
        builder.Configuration.GetSection("MongoDbSettings"));

    // Configure Validation settings
    builder.Services.Configure<ValidationSettings>(
        builder.Configuration.GetSection("ValidationSettings"));

    // Register services
    builder.Services.AddSingleton<IMongoDbService, MongoDbService>();
    builder.Services.AddScoped<ILinkValidationService, LinkValidationService>();

    // Configure HttpClient for link validation
    builder.Services.AddHttpClient("LinkValidator", client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "LinksApi/1.0");
        client.DefaultRequestHeaders.Add("Accept", "*/*");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Configure Swagger with API Key authentication
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Links Validation API",
            Version = "v1",
            Description = "High-performance API for validating web links at scale. Capable of handling 1K to 10M links with batch processing and parallel validation."
        });

        c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "API Key authentication. Use 'admin123' as the key.",
            Type = SecuritySchemeType.ApiKey,
            Name = "X-API-Key",
            In = ParameterLocation.Header,
            Scheme = "ApiKeyScheme"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    },
                    In = ParameterLocation.Header
                },
                new List<string>()
            }
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Links API V1");
        });
    }

    app.UseHttpsRedirection();

    // Add API Key authentication middleware
    app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    Log.Information("Links API started successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
