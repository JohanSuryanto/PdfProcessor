# Troubleshooting Guide

This guide covers common issues, debugging tips, and solutions for the PDF Processor application.

## Common Issues

### API Connection Refused

**Error Message**:
```
No connection could be made because the target machine actively refused it. (localhost:5000)
```

**Possible Causes**:
- API server is not running
- Incorrect BaseUrl in appsettings.json
- Firewall blocking the connection
- Wrong port number
- API server is listening on a different interface

**Solutions**:
1. Verify the API server is running
2. Check `BaseUrl` in appsettings.json matches the API server URL
3. Ensure firewall allows connections to the API port
4. Verify the port is correct (default: 5000)
5. Check if API server is listening on localhost vs 0.0.0.0

**Debug Steps**:
```bash
# Test if API is reachable
curl http://localhost:5000/api/pdf/upload

# Check if port is in use
netstat -an | findstr :5000  # Windows
lsof -i :5000               # Linux/macOS
```

### File Not Moving to Failed Folder

**Error Message**:
File remains in Input folder after API failure

**Possible Causes**:
- Insufficient file permissions on Input or Failed folders
- Failed folder doesn't exist
- Disk space issues
- Application doesn't have write permissions
- File is locked by another process

**Solutions**:
1. Check file permissions on Input and Failed folders
2. Ensure Failed folder exists (should be auto-created)
3. Check available disk space
4. Verify the application has write permissions
5. Check if file is locked by another process

**Debug Steps**:
```powershell
# Check folder permissions
Get-Acl Input | Format-List
Get-Acl Failed | Format-List

# Check disk space
Get-PSDrive C

# Check for locked files
Get-Process | Where-Object {$_.MainWindowTitle -like "*pdf*"}
```

### Duplicate File Names

**Issue**: Files with same name overwrite each other

**Solution**: 
The application automatically applies GUID-based naming to all failed files:
- Format: `filename_8charGUID.pdf`
- Example: `document_a1b2c3d4.pdf`, `document_e5f6g7h8.pdf`

**Verification**:
```powershell
# Check Failed folder for GUID suffixes
Get-ChildItem Failed | Select-Object Name
```

### Configuration Not Loading

**Error Message**: API settings use default values instead of appsettings.json

**Possible Causes**:
- appsettings.json not in project root
- Invalid JSON syntax
- File not set to copy to output directory
- Configuration cached from previous run

**Solutions**:
1. Ensure appsettings.json is in the project root
2. Verify JSON syntax is valid (use JSON validator)
3. Check that file is set to "Copy to Output Directory: PreserveNewest"
4. Restart the application after configuration changes
5. Delete bin/obj folders and rebuild

**Debug Steps**:
```csharp
// Add to Program.cs for debugging
Console.WriteLine($"API Base URL: {apiBaseUrl}");
Console.WriteLine($"Failed Folder: {failedFolderPath}");
Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
```

### File Still Being Copied

**Issue**: File is processed before copy completes

**Possible Causes**:
- Large file size
- Slow network copy
- Insufficient retry delay

**Solution**:
The application has built-in retry mechanism (10 retries, 500ms delay). For large files, increase these values in `FolderWatcherService.cs`:

```csharp
private void WaitForFileReady(string filePath)
{
    const int maxRetries = 20;  // Increase from 10
    const int retryDelayMs = 1000;  // Increase from 500
    // ... rest of code
}
```

### Build Errors

**Error**: File locked by another process during build

**Error Message**:
```
MSB3027: Could not copy "apphost.exe" to "PdfProcessor.exe". Exceeded retry count of 10.
The file is locked by: "PdfProcessor (XXXX)"
```

**Solution**:
Stop the running application before building:

```powershell
# Stop by process ID
Stop-Process -Id XXXX -Force

# Or stop by name
Get-Process dotnet | Where-Object {$_.MainWindowTitle -like "*PdfProcessor*"} | Stop-Process -Force
```

## Debugging Tips

### Enable Detailed Logging

Currently, all output goes to console. To debug:

1. Run application from command line to see all console output
2. Look for specific message patterns:
   - "Processing file:" - File is being processed
   - "Successfully sent" - API call succeeded
   - "Failed to send" - API call failed
   - "Error sending" - Exception occurred
   - "Moved failed file to:" - File operation completed
   - "Deleted file:" - File deletion completed

### Monitor Console Output

Key console messages to watch:

```
Processing file: {fileName}                    # File processing started
Successfully sent {apiUrl} for {fileName}       # API success
Failed to send {apiUrl} for {fileName}         # API failure
Error sending {apiUrl} for {fileName}: {error} # Exception
Moved failed file to: {fullPath}                # File moved
Deleted file: {fileName}                        # File deleted
Total files processed: {total}                  # Summary
Successfully sent: {success}                    # Summary
Failed to send: {failed}                        # Summary
```

### Monitor Folders

**Input Folder**:
- Check for files that should be processed
- Verify files are being deleted on success
- Check for files that remain after processing

**Failed Folder**:
- Check for files that failed processing
- Verify GUID suffix naming
- Check for duplicate handling

### Check Configuration

Add debug output to Program.cs to verify configuration:

```csharp
Console.WriteLine($"API Base URL: {apiBaseUrl}");
Console.WriteLine($"Failed Folder: {failedFolderPath}");
Console.WriteLine($"Input Folder: {inputFolderPath}");
Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
```

### Test API Endpoint Independently

Use curl or Postman to test the API endpoint without the PDF Processor:

```bash
curl -X POST https://localhost:5000/api/pdf/upload \
  -H "Content-Type: application/json" \
  -d '{"FileName":"test.pdf","FilePath":"C:\\test.pdf","DetectedAt":"2026-06-13T00:00:00Z"}'
```

### Enable .NET Logging

Add logging to Program.cs for detailed diagnostics:

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Application starting");
```

## Performance Issues

### Slow File Processing

**Symptoms**: Files take a long time to process

**Possible Causes**:
- Slow API response times
- Network latency
- Large file sizes
- Disk I/O issues

**Solutions**:
1. Monitor API response times
2. Check network connectivity
3. Optimize API endpoint
4. Use faster storage

### High Memory Usage

**Symptoms**: Application uses excessive memory

**Possible Causes**:
- Memory leak in HTTP client
- Large number of files in queue
- Not disposing resources properly

**Solutions**:
1. Use a single HttpClient instance (already implemented)
2. Process files in batches
3. Monitor memory usage with dotnet-counters

```bash
dotnet-counters monitor --process-id <PID>
```

## Network Issues

### Intermittent API Failures

**Symptoms**: API calls sometimes succeed, sometimes fail

**Possible Causes**:
- Unstable network connection
- API server load balancing issues
- Rate limiting
- DNS resolution issues

**Solutions**:
1. Implement retry logic (planned for Phase 7)
2. Check network stability
3. Monitor API server health
4. Use stable DNS servers

### DNS Resolution Issues

**Symptoms**: Cannot resolve API hostname

**Solutions**:
1. Use IP address instead of hostname
2. Check DNS configuration
3. Try alternative DNS servers
4. Add entry to hosts file

## File System Issues

### Permission Denied

**Symptoms**: Cannot create or access folders

**Solutions**:
1. Run application with appropriate permissions
2. Check folder permissions
3. Use different folder location
4. Run as administrator (Windows)

### Path Too Long

**Symptoms**: File operations fail due to long paths

**Solutions**:
1. Use shorter folder names
2. Use UNC paths for Windows
3. Enable long path support in .NET

## Getting Help

### Collect Diagnostic Information

When reporting issues, collect:

1. Console output
2. appsettings.json (with sensitive data removed)
3. .NET version: `dotnet --version`
4. OS version
5. Error messages
6. Steps to reproduce

### Useful Commands

```bash
# Check .NET version
dotnet --version

# List running processes
dotnet-counters list

# Check environment variables
echo $env:PATH  # PowerShell
echo $PATH      # Linux/macOS

# Check file system
Get-ChildItem -Recurse  # PowerShell
ls -R              # Linux/macOS
```

## Next Steps

- See [SETUP.md](SETUP.md) for installation and configuration
- See [API.md](API.md) for API integration details
- See [TESTING.md](TESTING.md) for testing guide
- See [ARCHITECTURE.md](ARCHITECTURE.md) for architecture overview
