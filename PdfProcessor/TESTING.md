# Testing Guide

This guide covers testing strategies, POC setup, mock API testing, and test scenarios for the PDF Processor application.

## Quick Start POC

This section provides a complete walkthrough to set up and test the PDF Processor with a mock API.

### Step 1: Set Up the PDF Processor

```bash
# Clone or navigate to the project directory
cd PdfProcessor

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Update appsettings.json with your API settings
# (See SETUP.md for configuration details)
```

### Step 2: Create a Test API

#### Option A: Simple C# API (Minimal)

Create a new minimal API project:

```bash
# In a new directory
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

Run the test API:
```bash
dotnet run
```

#### Option B: Node.js/Express API

```bash
# Install Node.js if not already installed
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

Run the test API:
```bash
node server.js
```

### Step 3: Configure PDF Processor

Update `appsettings.json` in the PdfProcessor project:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5000/api",
    "Endpoint": "pdf/upload"
  }
}
```

### Step 4: Create Test PDF Files

#### Method 1: Create Empty PDF Files (for testing file detection)

```bash
# Windows PowerShell
New-Item -Path "Input\test1.pdf" -ItemType File
New-Item -Path "Input\test2.pdf" -ItemType File
New-Item -Path "Input\test3.pdf" -ItemType File
```

#### Method 2: Use Existing PDF Files

Copy any existing PDF files to the `Input` folder:
```bash
# Windows
copy "C:\path\to\your\file.pdf" "Input\"

# Linux/macOS
cp /path/to/your/file.pdf Input/
```

#### Method 3: Generate PDF with Content (optional)

Use a PDF library or online tool to create test PDFs with actual content.

### Step 5: Run the PDF Processor

```bash
# In the PdfProcessor directory
dotnet run
```

### Step 6: Test the Flow

1. **Startup Processing**: The application will process any existing PDF files in the Input folder
2. **Add New Files**: Drop new PDF files into the Input folder while the application is running
3. **Monitor Console**: Watch for processing messages and summary output
4. **Check Folders**:
   - Successful files should be deleted from Input
   - Failed files should appear in Failed folder with GUID suffix

### Step 7: Verify API Integration

Check the test API console output to confirm it received the PDF notifications:
```
Received: test1.pdf at 2026-06-13T00:00:00Z
Received: test2.pdf at 2026-06-13T00:00:05Z
```

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
6. Run the PDF Processor

### Using WireMock.NET

1. Install WireMock.NET:
```bash
dotnet add package WireMock.Net
```

2. Create a simple mock server:

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

3. Run the mock server, then run the PDF Processor

## Sample Test Data

### Test Scenarios

#### Scenario 1: Successful Processing

**Setup**:
- API server running and returning 200 OK
- Input folder empty

**Files**: 3 PDF files (test1.pdf, test2.pdf, test3.pdf)

**Expected Behavior**:
- All files processed successfully
- Files deleted from Input folder
- API receives 3 notifications
- Summary shows: Total: 3, Success: 3, Failed: 0

**Verification**:
```powershell
# Check Input folder is empty
Get-ChildItem Input

# Check Failed folder is empty
Get-ChildItem Failed

# Check API console for 3 received messages
```

#### Scenario 2: API Failure

**Setup**:
- API server stopped or returning 500 error
- Input folder empty

**Files**: 2 PDF files (test1.pdf, test2.pdf)

**Expected Behavior**:
- Files fail to send to API
- Files moved to Failed folder with GUID suffix
- Summary shows: Total: 2, Success: 0, Failed: 2

**Verification**:
```powershell
# Check Input folder is empty
Get-ChildItem Input

# Check Failed folder has 2 files with GUID suffix
Get-ChildItem Failed

# Check console for error messages
```

#### Scenario 3: Mixed Results

**Setup**:
- API server configured to return 200 for first file, 500 for second
- Input folder empty

**Files**: 2 PDF files (test1.pdf, test2.pdf)

**Expected Behavior**:
- First file succeeds and is deleted
- Second file fails and is moved to Failed
- Summary shows: Total: 2, Success: 1, Failed: 1

**Verification**:
```powershell
# Check Input folder is empty
Get-ChildItem Input

# Check Failed folder has 1 file
Get-ChildItem Failed

# Check console for mixed success/failure messages
```

#### Scenario 4: Duplicate Filenames

**Setup**:
- API server running
- Input folder empty

**Files**: 3 files all named `document.pdf`

**Expected Behavior**:
- All files processed
- Failed files get GUID suffix
- Failed folder contains: `document_a1b2c3d4.pdf`, `document_e5f6g7h8.pdf`, etc.

**Verification**:
```powershell
# Check Failed folder for GUID suffixes
Get-ChildItem Failed | Select-Object Name
```

#### Scenario 5: Large File Copy

**Setup**:
- API server running
- Input folder empty

**Files**: 1 large PDF file (>100MB)

**Expected Behavior**:
- File copy completes before processing
- Built-in retry mechanism waits for file to be ready
- File processed successfully

**Verification**:
```powershell
# Monitor console for "Processing file" message
# Verify file is deleted after processing
```

### Creating Test PDF Files Programmatically

#### C# Example

```csharp
// Create a simple test PDF file
using System.IO;
using System.Text;

var testPdfPath = "Input/test.pdf";
File.WriteAllText(testPdfPath, "Test PDF content");
File.Move(testPdfPath, testPdfPath + ".pdf");
```

#### PowerShell Example

```powershell
# Create multiple test files
1..10 | ForEach-Object {
    New-Item -Path "Input\test$_.pdf" -ItemType File
}
```

#### Bash Example

```bash
# Create multiple test files
for i in {1..10}; do
    touch "Input/test$i.pdf"
done
```

## Integration Testing

### End-to-End Test Script

Create a PowerShell script `test-pdfprocessor.ps1`:

```powershell
# Stop any running instances
Get-Process dotnet | Where-Object {$_.MainWindowTitle -like "*PdfProcessor*"} | Stop-Process -Force

# Clean up folders
Remove-Item -Path "Input\*" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "Failed\*" -Force -ErrorAction SilentlyContinue

# Create test files
New-Item -Path "Input\test1.pdf" -ItemType File
New-Item -Path "Input\test2.pdf" -ItemType File
New-Item -Path "Input\test3.pdf" -ItemType File

# Start the application
Start-Process dotnet -ArgumentList "run" -WorkingDirectory "."
Write-Host "Application started. Check console output."
Write-Host "Test files: test1.pdf, test2.pdf, test3.pdf"
```

Run the test:
```powershell
.\test-pdfprocessor.ps1
```

### Automated Test with Verification

```powershell
# Stop any running instances
Get-Process dotnet | Where-Object {$_.MainWindowTitle -like "*PdfProcessor*"} | Stop-Process -Force

# Clean up folders
Remove-Item -Path "Input\*" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "Failed\*" -Force -ErrorAction SilentlyContinue

# Create test files
New-Item -Path "Input\test1.pdf" -ItemType File
New-Item -Path "Input\test2.pdf" -ItemType File
New-Item -Path "Input\test3.pdf" -ItemType File

# Start the application
$process = Start-Process dotnet -ArgumentList "run" -WorkingDirectory "." -PassThru

# Wait for processing
Start-Sleep -Seconds 10

# Stop the application
Stop-Process -Id $process.Id -Force

# Verify results
$inputFiles = Get-ChildItem Input
$failedFiles = Get-ChildItem Failed

Write-Host "Input files remaining: $($inputFiles.Count)"
Write-Host "Failed files: $($failedFiles.Count)"

if ($inputFiles.Count -eq 0 -and $failedFiles.Count -eq 0) {
    Write-Host "Test PASSED: All files processed successfully"
} else {
    Write-Host "Test FAILED: Some files not processed"
}
```

## Unit Testing

### Testing ApiService

```csharp
using Xunit;
using Moq;
using System.Net;
using System.Net.Http;

public class ApiServiceTests
{
    [Fact]
    public async Task SendPdfNotification_Success_IncrementsSuccessCount()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var apiService = new ApiService(httpClient, "http://localhost:5000/api", "Failed");

        // Act
        await apiService.SendPdfNotification("test.pdf", "C:\\test.pdf");

        // Assert
        Assert.Equal(1, apiService.SuccessCount);
        Assert.Equal(0, apiService.FailedCount);
    }

    [Fact]
    public async Task SendPdfNotification_Failure_IncrementsFailedCount()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var apiService = new ApiService(httpClient, "http://localhost:5000/api", "Failed");

        // Act
        await apiService.SendPdfNotification("test.pdf", "C:\\test.pdf");

        // Assert
        Assert.Equal(0, apiService.SuccessCount);
        Assert.Equal(1, apiService.FailedCount);
    }
}
```

## Performance Testing

### Load Testing

```powershell
# Create 100 test files
1..100 | ForEach-Object {
    New-Item -Path "Input\test$_.pdf" -ItemType File
}

# Monitor processing time
$startTime = Get-Date
# Run application and wait for completion
$endTime = Get-Date
$duration = $endTime - $startTime
Write-Host "Processed 100 files in $($duration.TotalSeconds) seconds"
```

### Memory Testing

```bash
# Monitor memory usage
dotnet-counters monitor --process-id <PID> System.Runtime
```

## Next Steps

- See [SETUP.md](SETUP.md) for installation and configuration
- See [API.md](API.md) for API integration details
- See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues
- See [ARCHITECTURE.md](ARCHITECTURE.md) for architecture overview
