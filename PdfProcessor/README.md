# PDF Processor

A .NET 8 Console Application that monitors a local folder for PDF files, processes them, and sends notifications to an API.

## Overview

The PDF Processor is a service-oriented application that:
- Monitors an `Input` folder for PDF files using configurable polling
- Sends PDF notifications to a configurable API endpoint
- Deletes files on successful API response
- Moves failed files to a `Failed` folder with GUID-based naming
- Tracks and reports success/failed statistics
- Supports configurable polling intervals (e.g., 10s, 30s, 60s)

## Quick Start

```bash
# Install dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

For detailed setup instructions, see [SETUP.md](SETUP.md).

## Documentation

- **[SETUP.md](SETUP.md)** - Installation, configuration, and environment setup
- **[API.md](API.md)** - API integration details, payload format, and response handling
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Common issues, debugging tips, and solutions
- **[TESTING.md](TESTING.md)** - POC guide, mock API testing, and test scenarios
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Service responsibilities, data flow, and design decisions

## Implemented Features

### Phase 1: Basic PDF Detection
- Create `Input` folder automatically
- Scan for PDF files
- Print file names to console

### Phase 2: Long-Running Worker
- Continuous folder monitoring with configurable polling
- Automatic detection of new PDF files at configurable intervals
- File-ready check to prevent processing incomplete files
- Separated services: FolderWatcherService, PdfProcessorService
- Overlap prevention with processing flag

### Phase 3: API Integration
- HTTP POST notifications to external API
- Configurable API settings via appsettings.json
- Centralized route definitions in Routes/ApiRoutes.cs
- Error handling for API failures

### Phase 4: File Operations
- Delete files on successful API response
- Move files to Failed folder on failure
- GUID-based naming for duplicate handling
- Automatic Failed folder creation

### Phase 5: Tracking and Reporting
- Track success/failed file counts
- Display summary after processing
- Debounced summary display (3-second delay)
- Per-batch statistics with counter reset

## Technology Stack

- .NET 8 Console Application
- C#
- Microsoft.Extensions.Configuration for configuration management
- System.Text.Json for JSON serialization
- Configurable polling for folder monitoring

## Project Structure

```
PdfProcessor/
├── Input/              # Monitored folder for PDF files
├── Failed/             # Failed PDF files moved here
├── Routes/             # API route definitions
│   └── ApiRoutes.cs
├── Services/           # Application services
│   ├── ApiService.cs
│   ├── FolderWatcherService.cs
│   └── PdfProcessorService.cs
├── appsettings.json    # Configuration file
├── Program.cs          # Application entry point
└── PdfProcessor.csproj # Project file
```

## Configuration

Edit `appsettings.json` to configure API and polling settings:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5000/api",
    "Endpoint": "pdfnotifications"
  },
  "PollingSettings": {
    "IntervalSeconds": 60
  }
}
```

Edit `.env` file to configure folder paths (recommended method):

```env
PUBLIC_FOLDER_URL=Input
FAILED_FOLDER_URL=Failed
```

**Note**: You can paste Windows paths directly with single backslashes - no escaping needed.

For backward compatibility, you can also use `appsettings.folder.json`:

```json
{
  "FolderPaths": {
    "InputFolder": "Input",
    "FailedFolder": "Failed"
  }
}
```

See [SETUP.md](SETUP.md) for detailed configuration options.

## API Payload Format

```json
{
  "FileName": "document.pdf",
  "FilePath": "C:\\path\\to\\Input\\document.pdf",
  "DetectedAt": "2026-06-13T00:00:00Z"
}
```

See [API.md](API.md) for complete API integration details.

## Future Enhancements

### Phase 6
- Extract text content from PDF files
- Parse structured information from PDFs

### Phase 7
- Store extracted information in a database
- Add retry logic for failed API calls

### Phase 8
- Add logging to file
- Add configuration for retry policies and timeouts

## License

This project is provided as-is for educational and development purposes.
