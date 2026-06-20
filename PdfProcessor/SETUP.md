# Setup Guide

This guide covers installation, configuration, and environment setup for the PDF Processor application.

## Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 (optional) or any code editor
- Windows 10/11 or Linux or macOS
- 100MB free disk space minimum

## Installation

### Clone or Navigate to Project

```bash
cd PdfProcessor
```

### Restore Dependencies

```bash
dotnet restore
```

### Build the Project

```bash
dotnet build
```

### Run the Application

```bash
# Development mode
dotnet run

# Release mode
dotnet run --configuration Release
```

### Running from Visual Studio

- Press F5 to run with debugging
- Press Ctrl+F5 to run without debugging

## Configuration

### appsettings.json

The application uses `appsettings.json` for API configuration:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5000/api",
    "Endpoint": "pdfnotifications"
  }
}
```

### .env File

The application uses `.env` file for all configuration (folder paths, polling, and scheduling). This is the recommended method as it doesn't require JSON escaping:

```env
PUBLIC_FOLDER_URL=C:\Users\YourName\Desktop\PdfFolder\Input
FAILED_FOLDER_URL=C:\Users\YourName\Desktop\PdfFolder\Failed
SCHEDULE_MODE=INTERVAL
POLLING_INTERVAL_SECONDS=60
SPECIFIC_TIME=00:00:00
```

**Note**: You can paste Windows paths directly with single backslashes - no escaping needed.

**Schedule Modes**:
- `INTERVAL` - Polls at regular intervals (default). Scans for files on startup and at each interval.
- `SPECIFIC_TIME` - Runs once daily at the specified time (24-hour format). Skips initial scan on startup, only runs at the scheduled time.

### appsettings.folder.json (Legacy)

The application still supports `appsettings.folder.json` for backward compatibility, but `.env` is preferred:

```json
{
  "FolderPaths": {
    "InputFolder": "Input",
    "FailedFolder": "Failed"
  }
}
```

### Configuration Settings

#### API Settings
- **BaseUrl**: The base URL of your API endpoint
- **Endpoint**: The specific route for PDF notifications

#### Schedule Settings (.env)
- **SCHEDULE_MODE**: Schedule mode - `INTERVAL` or `SPECIFIC_TIME` (default: INTERVAL)
- **POLLING_INTERVAL_SECONDS**: Polling interval in seconds when using INTERVAL mode (default: 60)
- **SPECIFIC_TIME**: Specific time to run daily when using SPECIFIC_TIME mode, format: HH:mm:ss (default: 00:00:00)

#### Folder Path Settings (.env)
- **PUBLIC_FOLDER_URL**: Path to the Input folder (supports relative or absolute paths)
- **FAILED_FOLDER_URL**: Path to the Failed folder (supports relative or absolute paths)

#### Folder Path Settings (JSON - Legacy)
- **InputFolder**: Path to the Input folder (supports relative or absolute paths)
- **FailedFolder**: Path to the Failed folder (supports relative or absolute paths)

### Configuration Loading

The application loads configuration in the following priority order:
1. `.env` file in the project root (all settings - highest priority)
2. `appsettings.json` in the project root (API settings only)
3. `appsettings.folder.json` in the project root (folder paths - fallback)
4. Default values (Input/Failed folders, INTERVAL mode, 60 seconds)

Files are copied to output directory on build (PreserveNewest). Configuration is loaded at application startup.

### Updating Configuration

**Using the Settings GUI (Recommended)**:
1. Right-click the system tray icon
2. Select "Settings"
3. Configure folder paths, schedule mode, and polling interval
4. Click "Save" - the folder watcher automatically restarts with new settings

**Manual Configuration (.env file)**:
1. Edit `.env` file in the project root
2. No rebuild required - just restart the application
3. Paste Windows paths directly with single backslashes

**For API settings**:
1. Edit `appsettings.json` in the project root
2. Rebuild the application: `dotnet build`
3. Restart the application for changes to take effect

### Folder Path Configuration

The application supports both relative and absolute folder paths via the `.env` file:

**Relative Paths** (relative to application directory):
```env
PUBLIC_FOLDER_URL=Input
FAILED_FOLDER_URL=Failed
```

**Absolute Paths** (any location on your system):
```env
PUBLIC_FOLDER_URL=C:\Users\YourName\Desktop\PdfFolder\Input
FAILED_FOLDER_URL=C:\Users\YourName\Desktop\PdfFolder\Failed
```

**Note**: Unlike JSON, `.env` files don't require escaping backslashes. You can paste Windows paths directly.

## Dependencies

### NuGet Packages

- `DotNetEnv` (3.1.1) - .env file loading
- `Microsoft.Extensions.Configuration` (10.0.9) - Configuration management
- `Microsoft.Extensions.Configuration.Json` (10.0.9) - JSON configuration provider

### Built-in .NET Libraries

- `System.Text.Json` - JSON serialization
- `System.IO` - File system operations
- `System.Net.Http` - HTTP client for API calls

## Environment Requirements

### Development Environment

- **OS**: Windows 10/11 or Linux or macOS
- **SDK**: .NET 8.0 SDK
- **Disk Space**: 100MB minimum
- **Permissions**: Read/write access to project directory

### Production Environment

- **Runtime**: .NET 8.0 Runtime (if not self-contained)
- **Network**: Stable connection to API endpoint
- **File System**: Write permissions for Input/Failed folders
- **Disk Space**: Sufficient space for PDF files

## Folder Structure

The application automatically creates the following folders:

```
PdfProcessor/
├── Input/              # Monitored folder for PDF files
├── Failed/             # Failed PDF files moved here
├── icon.ico            # Application icon
├── bin/                # Build output
└── obj/                # Build intermediates
```

### Input Folder

- Location: `Input/` in the application directory
- Purpose: Monitored folder for PDF files
- Auto-created: Yes, if it doesn't exist
- Permissions: Read/write required

### Failed Folder

- Location: `Failed/` in the application directory
- Purpose: Stores PDF files that failed API processing
- Auto-created: Yes, if it doesn't exist
- Naming: Failed files get GUID suffix (e.g., `document_a1b2c3d4.pdf`)
- Permissions: Read/write required

## Build Configuration

### Debug Build

```bash
dotnet build --configuration Debug
```

- Includes debug symbols
- Optimizations disabled
- Faster build time
- Larger output size

### Release Build

```bash
dotnet build --configuration Release
```

- No debug symbols
- Optimizations enabled
- Slower build time
- Smaller output size
- Better performance

### Publish for Deployment

```bash
# Self-contained (includes runtime)
dotnet publish -c Release -r win-x64 --self-contained

# Framework-dependent (requires runtime)
dotnet publish -c Release -r win-x64
```

## Project File

The `PdfProcessor.csproj` file contains:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DotNetEnv" Version="3.1.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.9" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="appsettings.folder.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update=".env">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

## System Tray

The application runs in the system tray with the following options:

- **Show Console** - Toggle the console window visibility
- **Settings** - Open the Settings dialog to configure:
  - Input folder path (with Browse button)
  - Failed folder path (with Browse button)
  - Schedule mode (Interval or Specific Time)
  - Polling interval (seconds) or specific time (24-hour format)
- **Exit** - Stop the application

Settings changes take effect immediately - the folder watcher restarts automatically.

## Known Limitations

1. **No Retry Logic**: Failed API calls are not retried automatically
2. **Console-Only Logging**: No file-based logging
3. **No PDF Content Extraction**: Only file metadata is sent to API
4. **Single API Endpoint**: Only one endpoint is configured
5. **No Database**: No persistent storage of processing history

## Next Steps

- See [API.md](API.md) for API integration details
- See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues
- See [TESTING.md](TESTING.md) for testing guide
- See [ARCHITECTURE.md](ARCHITECTURE.md) for architecture overview
