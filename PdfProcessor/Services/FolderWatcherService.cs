using System.IO;
using System.Text.Json;
using PdfProcessor.Contracts;

namespace PdfProcessor.Services;

public class FolderWatcherService
{
    private readonly string _folderPath;
    private readonly PdfProcessorService _pdfProcessorService;
    private readonly ApiService _apiService;
    private readonly int _pollingIntervalSeconds;
    private readonly string _scheduleMode;
    private readonly TimeSpan _specificTime;
    private readonly HashSet<string> _processedFiles;
    private System.Threading.Timer? _pollingTimer;
    private System.Threading.Timer? _debounceTimer;
    private readonly object _lock = new object();
    private bool _isProcessing = false;

    public FolderWatcherService(string folderPath, PdfProcessorService pdfProcessorService, ApiService apiService, int pollingIntervalSeconds = 60, string scheduleMode = "INTERVAL", string specificTime = "00:00:00")
    {
        _folderPath = folderPath;
        _pdfProcessorService = pdfProcessorService;
        _apiService = apiService;
        _pollingIntervalSeconds = pollingIntervalSeconds;
        _scheduleMode = scheduleMode;
        _specificTime = TimeSpan.TryParse(specificTime, out var time) ? time : TimeSpan.Zero;
        _processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Ensure the folder exists
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
    }

    public void Start()
    {
        Console.WriteLine($"Watching folder: {_folderPath}");
        
        if (_scheduleMode == "SPECIFIC_TIME")
        {
            Console.WriteLine($"Schedule mode: Specific Time at {_specificTime:hh\\:mm\\:ss}");
        }
        else
        {
            Console.WriteLine($"Schedule mode: Interval ({_pollingIntervalSeconds} seconds)");
        }
        
        Console.WriteLine("Press Ctrl+C to stop...");

        // Process existing files on startup only for interval mode
        if (_scheduleMode != "SPECIFIC_TIME")
        {
            Task.Run(async () => await CheckForNewFiles());
        }

        // Start timer based on schedule mode
        if (_scheduleMode == "SPECIFIC_TIME")
        {
            ScheduleSpecificTime();
        }
        else
        {
            // Start polling timer for interval mode
            _pollingTimer = new System.Threading.Timer(
                async _ => await CheckForNewFiles(),
                null,
                _pollingIntervalSeconds * 1000,
                _pollingIntervalSeconds * 1000);
        }
    }

    private async Task CheckForNewFiles()
    {
        // Skip if already processing
        lock (_lock)
        {
            if (_isProcessing)
            {
                return;
            }
            _isProcessing = true;
        }

        try
        {
            var pdfFiles = Directory.GetFiles(_folderPath, "*.pdf");
            var newFiles = new List<string>();

            lock (_lock)
            {
                // Remove entries for files that no longer exist (were deleted after processing)
                var filesToRemove = _processedFiles.Where(path => !File.Exists(path)).ToList();
                foreach (var fileToRemove in filesToRemove)
                {
                    _processedFiles.Remove(fileToRemove);
                }

                // Check for new files
                foreach (var file in pdfFiles)
                {
                    if (!_processedFiles.Contains(file))
                    {
                        newFiles.Add(file);
                        _processedFiles.Add(file);
                    }
                }
            }

            if (newFiles.Count > 0)
            {
                Console.WriteLine($"Found {newFiles.Count} new PDF file(s) to process");

                var batchResults = new List<(string FileName, KkExtractionResponse? Result)>();

                foreach (var file in newFiles)
                {
                    // Wait for file to be fully copied before processing
                    WaitForFileReady(file);

                    try
                    {
                        // Process the PDF file
                        var result = await _pdfProcessorService.ProcessPdf(file);
                        batchResults.Add((Path.GetFileName(file), result));

                        // Delete the file after successful processing
                        File.Delete(file);
                        Console.WriteLine($"Deleted: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to process {Path.GetFileName(file)}: {ex.Message}");

                        // Move failed file to the Failed folder
                        try
                        {
                            var failedFileName = $"{Path.GetFileNameWithoutExtension(file)}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{Path.GetExtension(file)}";
                            var failedFilePath = Path.Combine(_apiService.FailedFolderPath, failedFileName);
                            File.Move(file, failedFilePath);
                            Console.WriteLine($"Moved to Failed folder: {failedFileName}");
                        }
                        catch (Exception moveEx)
                        {
                            Console.WriteLine($"Error moving file to Failed folder: {moveEx.Message}");
                        }
                    }
                    finally
                    {
                        // Remove from processed set
                        lock (_lock)
                        {
                            _processedFiles.Remove(file);
                        }
                    }
                }

                // Write batch results to PROCESSED FILES folder
                WriteBatchResultsToFile(batchResults);

                // Reset debounce timer to show summary after 3 seconds of inactivity
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(_ => ShowSummary(), null, 3000, System.Threading.Timeout.Infinite);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for new files: {ex.Message}");
        }
        finally
        {
            lock (_lock)
            {
                _isProcessing = false;
            }
        }
    }

    private void WriteBatchResultsToFile(List<(string FileName, KkExtractionResponse? Result)> batchResults)
    {
        try
        {
            var outputFolder = Path.Combine(_folderPath, "RESULTS");
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputFilePath = Path.Combine(outputFolder, $"PROCESSED_DATA_{timestamp}.txt");

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            using var writer = new StreamWriter(outputFilePath, append: false, System.Text.Encoding.UTF8);

            writer.WriteLine($"Processed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Total files: {batchResults.Count}");

            for (int i = 0; i < batchResults.Count; i++)
            {
                writer.WriteLine($"[{i + 1}] {batchResults[i].FileName}");
            }

            writer.WriteLine(new string('-', 60));
            writer.WriteLine();

            var resultList = batchResults.Select(r => r.Result).ToList();
            writer.WriteLine(JsonSerializer.Serialize(resultList, jsonOptions));

            Console.WriteLine($"Batch results written to: {outputFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing batch results to file: {ex.Message}");
        }
    }

    private void ShowSummary()
    {
        var total = _apiService.SuccessCount + _apiService.FailedCount;
        if (total > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Total files processed: {total}");
            Console.WriteLine($"Successfully sent: {_apiService.SuccessCount}");
            Console.WriteLine($"Failed to send: {_apiService.FailedCount}");
            Console.WriteLine();

            // Reset counters after showing summary
            _apiService.ResetCounts();
        }
    }

    private void WaitForFileReady(string filePath)
    {
        const int maxRetries = 10;
        const int retryDelayMs = 500;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // Try to open the file exclusively to check if it's still being copied
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // If we can open it exclusively, the file is ready
                    return;
                }
            }
            catch (IOException)
            {
                // File is still being copied, wait and retry
                Thread.Sleep(retryDelayMs);
            }
        }
    }

    private void ScheduleSpecificTime()
    {
        var now = DateTime.Now;
        var scheduledTime = DateTime.Today.Add(_specificTime);
        
        // If the scheduled time has already passed today, schedule for tomorrow
        if (now > scheduledTime)
        {
            scheduledTime = scheduledTime.AddDays(1);
        }
        
        var delay = (scheduledTime - now).TotalMilliseconds;
        
        Console.WriteLine($"Next run scheduled for: {scheduledTime:yyyy-MM-dd HH:mm:ss}");
        
        _pollingTimer = new System.Threading.Timer(
            async _ => 
            {
                await CheckForNewFiles();
                // Reschedule for next day
                ScheduleSpecificTime();
            },
            null,
            (long)delay,
            System.Threading.Timeout.Infinite);
    }

    public void Stop()
    {
        _pollingTimer?.Dispose();
        _debounceTimer?.Dispose();
    }
}
