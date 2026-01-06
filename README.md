# Links Validation API

A high-performance C# Web API built with .NET 8 that validates web links at scale using MongoDB. Designed to efficiently handle anywhere from 1,000 to 10 million links with batch processing and parallel validation.

## Features

- **Scalable Architecture**: Batch processing with configurable batch sizes
- **Parallel Validation**: Configurable parallelism for maximum performance
- **MongoDB Integration**: Efficient data storage and retrieval
- **Comprehensive Logging**: Serilog file-based logging
- **API Key Authentication**: Simple and secure API key protection
- **Exception Handling**: Global exception handling middleware
- **Swagger Documentation**: Interactive API documentation
- **Smart Retry Logic**: Automatic retries for failed validations
- **Edge Case Handling**: Timeouts, redirects, unreachable domains

## Prerequisites

- .NET 8 SDK
- MongoDB running on localhost:27017
- Windows OS (for log path configuration)

## Configuration

### appsettings.json

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017/",
    "DatabaseName": "qualibardb",
    "CollectionName": "LinksCollection"
  },
  "ValidationSettings": {
    "BatchSize": 1000,
    "MaxDegreeOfParallelism": 10,
    "TimeoutSeconds": 5,
    "MaxRetries": 2
  },
  "ApiKey": "admin123"
}
```

### Performance Tuning

- **BatchSize**: Number of links processed per batch (default: 1000)
- **MaxDegreeOfParallelism**: Number of concurrent validations (default: 10)
- **TimeoutSeconds**: HTTP request timeout (default: 5)
- **MaxRetries**: Number of retry attempts (default: 2)

## API Endpoints

### 1. Add Links
**POST** `/api/links`

Add multiple links to the database.

**Headers:**
```
X-API-Key: admin123
Content-Type: application/json
```

**Request Body:**
```json
{
  "links": [
    "https://www.google.com",
    "https://www.github.com",
    "https://www.somenonexistentdomain.com/test"
  ]
}
```

**Response:**
```json
{
  "message": "Links added successfully",
  "count": 3,
  "links": [
    {
      "id": "507f1f77bcf86cd799439011",
      "links": "https://www.google.com",
      "status": "pending"
    }
  ]
}
```

### 2. Validate Links
**POST** `/api/links/validate`

Trigger validation of all stored links. This is the core endpoint that processes links in batches.

**Headers:**
```
X-API-Key: admin123
```

**Response:**
```json
{
  "message": "Validation completed successfully",
  "totalProcessed": 3,
  "validLinks": 2,
  "brokenLinks": 1,
  "durationMs": 1523.45,
  "durationSeconds": 1.52
}
```

### 3. Get Broken Links
**GET** `/api/links/broken`

Retrieve all broken links with failure details.

**Headers:**
```
X-API-Key: admin123
```

**Response:**
```json
{
  "count": 1,
  "brokenLinks": [
    {
      "id": "507f1f77bcf86cd799439011",
      "link": "https://www.somenonexistentdomain.com/test",
      "reason": "Network error: No such host is known",
      "lastValidated": "2026-01-06T10:30:00Z"
    }
  ]
}
```

## Running the API

### Development Mode

```bash
cd LinksApi
dotnet run
```

The API will start on:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

### Access Swagger UI

Navigate to: https://localhost:5001/swagger

**Authenticate in Swagger:**
1. Click "Authorize" button
2. Enter API Key: `admin123`
3. Click "Authorize"

## Architecture

### Batch Processing Flow

1. **Count Phase**: Determine total number of links
2. **Batch Calculation**: Calculate number of batches based on BatchSize
3. **Parallel Processing**: Each batch processes links in parallel
4. **Bulk Update**: Update database after each batch completes

### Validation Logic

- **HEAD Request First**: More efficient than GET
- **Fallback to GET**: If HEAD is not allowed (405)
- **Redirect Handling**: Automatically follows up to 5 redirects
- **Timeout Handling**: Configurable timeout with retry logic
- **Error Classification**: Detailed error reasons for broken links

### Database Schema

**Collection:** LinksCollection

```json
{
  "_id": ObjectId,
  "links": "https://example.com",
  "status": "pending|valid|broken",
  "reason": "valid|HTTP 404|Timeout|Network error",
  "createdat": ISODate,
  "updatedat": ISODate
}
```

## Performance Benchmarks

### Expected Performance

| Links Count | Batch Size | Parallelism | Estimated Time |
|-------------|------------|-------------|----------------|
| 1,000       | 1000       | 10          | ~10-15 seconds |
| 10,000      | 1000       | 10          | ~1-2 minutes   |
| 100,000     | 1000       | 20          | ~10-15 minutes |
| 1,000,000   | 1000       | 20          | ~2-3 hours     |

*Times vary based on network conditions and link responsiveness*

## Logging

Logs are written to: `C:\DriveD\Code\Repository\logs\linksapi-YYYYMMDD.log`

Log levels:
- **Information**: Normal operations, batch progress
- **Warning**: Invalid API keys, retry attempts
- **Error**: Unhandled exceptions, database errors

## Error Handling

### Common Error Responses

**401 Unauthorized:**
```json
{
  "error": "API Key is missing",
  "message": "Please provide a valid API key in the 'X-API-Key' header"
}
```

**400 Bad Request:**
```json
{
  "error": "Links array cannot be empty"
}
```

**500 Internal Server Error:**
```json
{
  "error": "An error occurred while processing your request.",
  "statusCode": 500,
  "timestamp": "2026-01-06T10:30:00Z"
}
```

## Testing Examples

### Using cURL

**Add Links:**
```bash
curl -X POST https://localhost:5001/api/links \
  -H "X-API-Key: admin123" \
  -H "Content-Type: application/json" \
  -d '{"links":["https://www.google.com","https://www.github.com"]}'
```

**Validate Links:**
```bash
curl -X POST https://localhost:5001/api/links/validate \
  -H "X-API-Key: admin123"
```

**Get Broken Links:**
```bash
curl -X GET https://localhost:5001/api/links/broken \
  -H "X-API-Key: admin123"
```

### Using PowerShell

```powershell
# Add Links
$headers = @{ "X-API-Key" = "admin123" }
$body = @{ links = @("https://www.google.com", "https://www.github.com") } | ConvertTo-Json
Invoke-RestMethod -Uri "https://localhost:5001/api/links" -Method Post -Headers $headers -Body $body -ContentType "application/json"

# Validate Links
Invoke-RestMethod -Uri "https://localhost:5001/api/links/validate" -Method Post -Headers $headers

# Get Broken Links
Invoke-RestMethod -Uri "https://localhost:5001/api/links/broken" -Method Get -Headers $headers
```

## Scalability Considerations

### For 10 Million Links

1. **Increase Batch Size**: Set to 5000-10000 for fewer database operations
2. **Increase Parallelism**: Set to 50-100 depending on system resources
3. **MongoDB Optimization**: 
   - Add indexes on `status` field
   - Use replica sets for read scalability
4. **Resource Monitoring**: Monitor CPU, memory, and network bandwidth
5. **Database Connection Pool**: MongoDB driver handles this automatically

### Recommended Settings for Large Scale

```json
{
  "ValidationSettings": {
    "BatchSize": 5000,
    "MaxDegreeOfParallelism": 50,
    "TimeoutSeconds": 3,
    "MaxRetries": 1
  }
}
```

## Security Notes

- API key authentication is basic - consider OAuth2 for production
- SSL/TLS certificate validation is disabled for link validation (to handle self-signed certificates)
- Always use HTTPS in production
- Store API keys in environment variables or Azure Key Vault

## Troubleshooting

### MongoDB Connection Issues
- Ensure MongoDB is running: `mongod --version`
- Check connection string in appsettings.json
- Verify database and collection names

### Performance Issues
- Reduce `MaxDegreeOfParallelism` if system is overloaded
- Increase `TimeoutSeconds` for slow networks
- Monitor logs for bottlenecks

### Memory Issues (Large Datasets)
- Increase batch size to reduce number of batches
- Ensure adequate system memory
- Monitor garbage collection in logs

## License

MIT License

## Author

Created for high-performance link validation at scale.
