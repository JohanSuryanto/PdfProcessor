# API Integration Guide

This guide covers API integration details, payload format, and response handling for the PDF Processor application.

## API Endpoint Configuration

### Configuration

The API endpoint is configured in `appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5000/api",
    "Endpoint": "pdfnotifications"
  }
}
```

### Route Definitions

API routes are centralized in `Routes/ApiRoutes.cs`:

```csharp
namespace PdfProcessor.Routes;

public static class ApiRoutes
{
    public const string PdfNotifications = "pdfnotifications";
    public const string PdfUpload = "pdf/upload";
    public const string PdfProcess = "pdf/process";
    public const string PdfStatus = "pdf/status/{id}";
}
```

### Current Endpoint

The application currently uses `ApiRoutes.PdfUpload`:
- Full URL: `{BaseUrl}/{PdfUpload}`
- Example: `https://localhost:5000/api/pdf/upload`

## API Payload Format

### Request Structure

When a PDF file is detected, the application sends a POST request with the following JSON payload:

```json
{
  "FileName": "document.pdf",
  "FilePath": "C:\\path\\to\\Input\\document.pdf",
  "DetectedAt": "2026-06-13T00:00:00Z"
}
```

### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| FileName | string | The name of the PDF file (including extension) |
| FilePath | string | The full path to the PDF file on the local system |
| DetectedAt | DateTime (ISO 8601) | UTC timestamp when the file was detected |

### Example Payloads

#### Example 1: Windows Path
```json
{
  "FileName": "invoice_001.pdf",
  "FilePath": "C:\\Users\\johan\\source\\repos\\PdfProcessor\\PdfProcessor\\Input\\invoice_001.pdf",
  "DetectedAt": "2026-06-13T12:30:45Z"
}
```

#### Example 2: Linux Path
```json
{
  "FileName": "report.pdf",
  "FilePath": "/home/user/PdfProcessor/Input/report.pdf",
  "DetectedAt": "2026-06-13T12:30:45Z"
}
```

## Response Handling

### Success Response (2xx)

**Status Code**: 200-299

**Behavior**:
- File is deleted from Input folder
- Success counter is incremented
- Console output: "Successfully sent {apiUrl} for {fileName} to API"
- Console output: "Deleted file: {fileName}"

**Example Response**:
```json
{
  "Status": "Received",
  "Timestamp": "2026-06-13T12:30:45Z"
}
```

### Failure Response (4xx, 5xx)

**Status Code**: 400-599

**Behavior**:
- File is moved to Failed folder with GUID suffix
- Failed counter is incremented
- Console output: "Failed to send {apiUrl} for {fileName}. Status: {statusCode}"
- Console output: "Moved failed file to: {fullPath}"

**Example Response**:
```json
{
  "Error": "Invalid request",
  "StatusCode": 400
}
```

### Exception Handling

**Behavior**:
- Any exception during API call triggers failure handling
- File is moved to Failed folder with GUID suffix
- Failed counter is incremented
- Console output: "Error sending {apiUrl} for {fileName}: {exceptionMessage}"
- Console output: "Moved failed file to: {fullPath}"

**Common Exceptions**:
- `HttpRequestException`: Network connectivity issues
- `TaskCanceledException`: Request timeout
- `IOException`: File system errors

## HTTP Client Configuration

### Current Configuration

The application uses a default `HttpClient` instance:

```csharp
var httpClient = new HttpClient();
var apiService = new ApiService(httpClient, apiBaseUrl, failedFolderPath);
```

### HTTP Headers

The application sends the following headers:

```
Content-Type: application/json
```

### Timeout

Default timeout is 100 seconds. To customize, add:

```csharp
var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromSeconds(30);
```

## Testing the API Endpoint

### Using curl

```bash
curl -X POST https://localhost:5000/api/pdf/upload \
  -H "Content-Type: application/json" \
  -d '{"FileName":"test.pdf","FilePath":"C:\\test.pdf","DetectedAt":"2026-06-13T00:00:00Z"}'
```

### Using PowerShell

```powershell
$body = @{
    FileName = "test.pdf"
    FilePath = "C:\test.pdf"
    DetectedAt = Get-Date -Format "o"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5000/api/pdf/upload" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

### Using Postman

1. Create a new POST request
2. URL: `https://localhost:5000/api/pdf/upload`
3. Headers: `Content-Type: application/json`
4. Body (raw JSON):
```json
{
  "FileName": "test.pdf",
  "FilePath": "C:\\test.pdf",
  "DetectedAt": "2026-06-13T00:00:00Z"
}
```

## Creating a Test API

### Option A: C# Minimal API

```bash
dotnet new webapi -n TestPdfApi
cd TestPdfApi
```

Update `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/api/pdf/upload", (PdfNotification notification) =>
{
    Console.WriteLine($"Received: {notification.FileName} at {notification.DetectedAt}");
    return Results.Ok(new { Status = "Received", Timestamp = DateTime.UtcNow });
});

app.Run();

record PdfNotification(string FileName, string FilePath, DateTime DetectedAt);
```

Run: `dotnet run`

### Option B: Node.js/Express API

```bash
npm init -y
npm install express body-parser
```

Create `server.js`:

```javascript
const express = require('express');
const bodyParser = require('body-parser');
const app = express();

app.use(bodyParser.json());

app.post('/api/pdf/upload', (req, res) => {
    const { FileName, FilePath, DetectedAt } = req.body;
    console.log(`Received: ${FileName} at ${DetectedAt}`);
    res.json({ Status: 'Received', Timestamp: new Date().toISOString() });
});

app.listen(5000, () => {
    console.log('Test API running on http://localhost:5000');
});
```

Run: `node server.js`

## Mock API Testing

### Using Postman Mock Server

1. Create a Postman account
2. Create a new mock server: https://mock-server.postman.com/
3. Add endpoint: `POST /api/pdf/upload`
4. Set response to: `200 OK` with JSON body:
```json
{
  "Status": "Received",
  "Timestamp": "2026-06-13T00:00:00Z"
}
```
5. Update `appsettings.json` with the mock server URL

### Using WireMock.NET

```bash
dotnet add package WireMock.Net
```

Create a mock server:

```csharp
using WireMock.Server;
using WireMock.Settings;

var server = WireMockServer.Start(new WireMockServerSettings
{
    Port = 5000
});

server
    .Given(Request.Create()
        .WithPath("/api/pdf/upload")
        .UsingPost())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithBodyAsJson(new { Status = "Received", Timestamp = DateTime.UtcNow }));

Console.WriteLine("Mock API running on http://localhost:5000");
Console.ReadLine();
```

## API Integration Best Practices

### Security Considerations

- Use HTTPS in production
- Validate API responses
- Implement authentication if required
- Sanitize file paths before sending to API

### Error Handling

- Implement retry logic for transient failures
- Log detailed error information
- Implement circuit breaker pattern for repeated failures
- Handle rate limiting from API

### Performance

- Consider batch processing for multiple files
- Implement async/await properly
- Use connection pooling for HTTP client
- Monitor API response times

## Next Steps

- See [SETUP.md](SETUP.md) for installation and configuration
- See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common API issues
- See [TESTING.md](TESTING.md) for testing guide
- See [ARCHITECTURE.md](ARCHITECTURE.md) for architecture overview
