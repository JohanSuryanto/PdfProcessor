# Architecture Guide

This guide covers the architecture, service responsibilities, data flow, and design decisions for the PDF Processor application.

## Overview

The PDF Processor is a .NET 8 console application that monitors a local folder for PDF files, processes them, and sends notifications to an external API. The application follows a service-oriented architecture with clear separation of concerns.

## Architecture Pattern

The application uses a **Service-Oriented Architecture (SOA)** with the following characteristics:

- **Separation of Concerns**: Each service has a single, well-defined responsibility
- **Dependency Injection**: Services are injected via constructors
- **Async/Await**: Asynchronous processing for I/O operations
- **Polling-Based**: Configurable polling intervals trigger file detection

## Service Architecture

### Service Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        Program.cs                            │
│                    (Application Entry)                       │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ├──► FolderWatcherService
                     │      (Monitors Input folder)
                     │
                     ├──► PdfProcessorService
                     │      (Coordinates processing)
                     │
                     └──► ApiService
                            (API communication)
```

### Service Dependencies

```
Program.cs
    ├──► FolderWatcherService
    │        └──► PdfProcessorService
    │                 └──► ApiService
    │
    └──► PdfProcessorService
             └──► ApiService
```

## Service Responsibilities

### ApiService

**Purpose**: Handles HTTP communication with external API and file operations

**Responsibilities**:
- Send HTTP POST requests to API endpoint
- Manage success/failed counters
- Perform file operations (delete on success, move on failure)
- Generate unique filenames for failed files using GUID

**Key Methods**:
- `SendPdfNotification(string fileName, string filePath)` - Main processing method
- `ResetCounts()` - Reset success/failed counters
- `GetUniqueFileName(string folderPath, string fileName)` - Generate unique filename

**Dependencies**:
- `HttpClient` - For HTTP communication
- Configuration (BaseUrl, FailedFolderPath)

**Design Decisions**:
- Single HttpClient instance for connection pooling
- GUID-based naming for guaranteed uniqueness
- Counter reset after each batch for per-batch statistics

### FolderWatcherService

**Purpose**: Monitors Input folder for new PDF files using configurable polling

**Responsibilities**:
- Monitor Input folder using configurable polling intervals
- Detect new PDF files (.pdf extension) at regular intervals
- Wait for files to be fully copied before processing
- Debounce summary display (3-second delay after last file)
- Prevent overlapping processing cycles with processing flag

**Key Methods**:
- `Start()` - Start folder monitoring with polling timer
- `CheckForNewFiles()` - Periodic check for new files
- `WaitForFileReady(string filePath)` - Wait for file copy completion
- `ShowSummary()` - Display processing summary

**Dependencies**:
- `PdfProcessorService` - For file processing
- `ApiService` - For counter access
- Configuration (PollingIntervalSeconds)

**Design Decisions**:
- Configurable polling interval (default 60 seconds)
- Processing flag prevents overlapping cycles
- Retry mechanism (10 retries, 500ms delay) for file-ready check
- Debounce timer for summary display to avoid console spam
- FileShare.None for exclusive file access check
- Processed files tracking with HashSet to avoid duplicates

### PdfProcessorService

**Purpose**: Coordinates PDF processing workflow

**Responsibilities**:
- Coordinate PDF processing workflow
- Delegate API calls to ApiService
- Currently only passes file metadata (future: extract PDF content)

**Key Methods**:
- `ProcessPdf(string filePath)` - Main processing entry point

**Dependencies**:
- `ApiService` - For API communication

**Design Decisions**:
- Simple wrapper for now, will expand with PDF content extraction
- Async method for non-blocking processing

## Data Flow

### Complete Data Flow

```
User drops PDF file
        ↓
Polling timer fires (at configured interval)
        ↓
FolderWatcherService.CheckForNewFiles()
        ↓
Check for new files (skip if already processing)
        ↓
WaitForFileReady (retry mechanism: 10 retries, 500ms delay)
        ↓
PdfProcessorService.ProcessPdf(filePath)
        ↓
ApiService.SendPdfNotification(fileName, filePath)
        ↓
HTTP POST to API endpoint
        ↓
┌───────────────┬───────────────┐
│   Success     │    Failure    │
│   (2xx)       │   (4xx, 5xx)  │
└───────┬───────┴───────┬───────┘
        ↓               ↓
File.Delete      File.Move to Failed
(filePath)       (with GUID suffix)
        ↓               ↓
SuccessCount++   FailedCount++
        └───────┬───────┘
                ↓
        Summary Display
        (after 3 seconds of inactivity)
                ↓
        Counters Reset
                ↓
        Processing flag reset
```

### Startup Flow

```
Application starts
        ↓
Load configuration from appsettings.json and appsettings.folder.json
        ↓
Get polling interval from configuration
        ↓
Create Input and Failed folders
        ↓
Instantiate services (HttpClient, ApiService, PdfProcessorService, FolderWatcherService)
        ↓
Start FolderWatcherService with polling timer
        ↓
Process existing PDF files in Input folder (immediate check)
        ↓
Start periodic polling timer (at configured interval)
        ↓
Keep application running (await Task.Delay(Timeout.Infinite))
```

### File Processing Flow

```
File detected
        ↓
Wait for file to be ready (WaitForFileReady)
        ↓
Process file (ProcessPdf)
        ↓
Send notification to API (SendPdfNotification)
        ↓
┌───────────────┬───────────────┐
│   Success     │    Failure    │
└───────┬───────┴───────┬───────┘
        ↓               ↓
Delete file    Move to Failed folder
        ↓               ↓
Increment     Increment
SuccessCount  FailedCount
```

## Design Patterns

### Observer Pattern

**Used in**: FolderWatcherService

**Implementation**: FileSystemWatcher uses the Observer pattern to notify subscribers of file system changes.

```csharp
_watcher.Created += OnFileCreated;
```

### Dependency Injection

**Used in**: All services

**Implementation**: Constructor injection for dependencies.

```csharp
public FolderWatcherService(string folderPath, PdfProcessorService pdfProcessorService, ApiService apiService)
{
    _pdfProcessorService = pdfProcessorService;
    _apiService = apiService;
}
```

### Retry Pattern

**Used in**: FolderWatcherService.WaitForFileReady

**Implementation**: Retry mechanism with configurable attempts and delay.

```csharp
for (int i = 0; i < maxRetries; i++)
{
    try
    {
        // Try to open file exclusively
        return;
    }
    catch (IOException)
    {
        Thread.Sleep(retryDelayMs);
    }
}
```

### Debounce Pattern

**Used in**: FolderWatcherService.ShowSummary

**Implementation**: Timer that resets on each file event, fires after inactivity.

```csharp
_debounceTimer?.Dispose();
_debounceTimer = new System.Threading.Timer(_ => ShowSummary(), null, 3000, System.Threading.Timeout.Infinite);
```

## Configuration Management

### Configuration Flow

```
appsettings.json (API & polling settings)
        ↓
appsettings.folder.json (folder paths)
        ↓
ConfigurationBuilder
        ↓
IConfiguration
        ↓
Program.cs reads values
        ↓
Passed to services via constructors
```

### Configuration Hierarchy

1. **appsettings.json** - API and polling configuration
2. **appsettings.folder.json** - Folder path configuration
3. **Default values** - Fallback in code
4. **Environment variables** - (not currently implemented)

## Error Handling Strategy

### Exception Handling Layers

1. **API Layer** (ApiService)
   - HTTP exceptions
   - Network exceptions
   - File I/O exceptions

2. **Processing Layer** (PdfProcessorService)
   - Processing exceptions
   - Service exceptions

3. **Monitoring Layer** (FolderWatcherService)
   - File system exceptions
   - Event handling exceptions

### Error Handling Approach

- **Try-Catch blocks** around critical operations
- **Graceful degradation** on file operation failures
- **Console logging** for all errors
- **File move to Failed folder** on API failures

## Concurrency Model

### Async/Await Pattern

The application uses async/await for I/O operations:

- **File operations**: Async where possible
- **HTTP requests**: Async with HttpClient
- **Event handling**: Async void for FileSystemWatcher events

### Thread Safety

- **Counters**: Simple integer properties (not thread-safe, but acceptable for current use case)
- **File operations**: OS-level file locking
- **HttpClient**: Thread-safe by design

### Future Concurrency Considerations

- Add locking for counters if multiple threads process files
- Consider ConcurrentQueue for file processing
- Implement semaphore for rate limiting

## Scalability Considerations

### Current Limitations

- Single-threaded file processing
- No batching of API calls
- No rate limiting
- No parallel processing

### Future Scalability Improvements

- **Parallel Processing**: Process multiple files concurrently
- **Batch API Calls**: Send multiple files in single request
- **Rate Limiting**: Implement token bucket or sliding window
- **Queue System**: Use message queue for file processing
- **Distributed Processing**: Scale across multiple instances

## Security Considerations

### Current Security

- **File paths**: Passed as-is (potential path traversal risk)
- **API URLs**: Configured in appsettings.json
- **No authentication**: API calls are unauthenticated
- **No encryption**: HTTP (not HTTPS) by default

### Recommended Security Improvements

- **Path validation**: Sanitize file paths before sending to API
- **Authentication**: Add API key or OAuth
- **HTTPS**: Enforce HTTPS in production
- **Input validation**: Validate PDF file headers
- **Rate limiting**: Prevent abuse

## Performance Considerations

### Current Performance

- **File detection**: Near-instant (FileSystemWatcher)
- **File-ready check**: Up to 5 seconds (10 retries × 500ms)
- **API call**: Depends on network latency
- **File operations**: Depends on disk I/O

### Performance Optimizations

- **Connection pooling**: HttpClient reused (already implemented)
- **Async I/O**: Non-blocking operations (already implemented)
- **Debounce**: Reduces console output (already implemented)

### Future Performance Improvements

- **Batch processing**: Process multiple files in parallel
- **Caching**: Cache API responses if needed
- **Compression**: Compress large PDFs before sending
- **CDN**: Use CDN for file storage

## Technology Stack

### Core Technologies

- **.NET 8**: Runtime framework
- **C#**: Programming language
- **FileSystemWatcher**: File system monitoring
- **HttpClient**: HTTP communication
- **System.Text.Json**: JSON serialization

### External Libraries

- **Microsoft.Extensions.Configuration**: Configuration management
- **Microsoft.Extensions.Configuration.Json**: JSON configuration provider

## Project Structure

```
PdfProcessor/
├── Input/              # Monitored folder for PDF files
├── Failed/             # Failed PDF files moved here
├── Routes/             # API route definitions
│   └── ApiRoutes.cs    # Centralized route constants
├── Services/           # Application services
│   ├── ApiService.cs              # API communication
│   ├── FolderWatcherService.cs   # Folder monitoring
│   └── PdfProcessorService.cs    # Processing coordination
├── icon.ico            # Application icon
├── appsettings.json    # Configuration file
├── Program.cs          # Application entry point
└── PdfProcessor.csproj # Project file
```

## Design Decisions

### Why FileSystemWatcher?

- **Built-in**: No external dependencies
- **Efficient**: Event-driven, no polling
- **Cross-platform**: Works on Windows, Linux, macOS

### Why GUID-based Naming?

- **Uniqueness**: Guaranteed no collisions
- **No file checks**: No need to check if file exists
- **Thread-safe**: No race conditions
- **Simple**: Single operation

### Why Debounce Timer?

- **User experience**: Avoids console spam
- **Performance**: Reduces output frequency
- **Natural batching**: Groups related operations

### Why Separate Services?

- **Maintainability**: Easier to modify individual components
- **Testability**: Easier to unit test
- **Reusability**: Services can be reused in other contexts
- **Clarity**: Clear separation of concerns

## Future Architecture Enhancements

### Phase 6: PDF Content Extraction

- Add PDF parsing library (e.g., iTextSharp, PdfSharp)
- Extract text content from PDFs
- Parse structured information
- Add to API payload

### Phase 7: Database Integration

- Add database layer (e.g., Entity Framework)
- Store processing history
- Add retry logic for failed API calls
- Implement circuit breaker pattern

### Phase 8: Logging and Monitoring

- Add structured logging (e.g., Serilog)
- Log to file and external systems
- Add health checks
- Implement metrics collection
- Add configuration for retry policies

## Next Steps

- See [SETUP.md](SETUP.md) for installation and configuration
- See [API.md](API.md) for API integration details
- See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues
- See [TESTING.md](TESTING.md) for testing guide
