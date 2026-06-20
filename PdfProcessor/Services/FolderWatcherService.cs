using System.IO;

namespace PdfProcessor.Services;

public class FolderWatcherService
{
    private readonly string _folderPath;
    private readonly PdfProcessorService _pdfProcessorService;
    private readonly ApiService _apiService;
    private readonly int _pollingIntervalSeconds;
    private readonly HashSet<string> _processedFiles;
    private System.Threading.Timer? _pollingTimer;
    private System.Threading.Timer? _debounceTimer;
    private readonly object _lock = new object();
    private bool _isProcessing = false;

    public FolderWatcherService(string folderPath, PdfProcessorService pdfProcessorService, ApiService apiService, int pollingIntervalSeconds = 60)
    {
        _folderPath = folderPath;
        _pdfProcessorService = pdfProcessorService;
        _apiService = apiService;
        _pollingIntervalSeconds = pollingIntervalSeconds;
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
        Console.WriteLine($"Polling interval: {_pollingIntervalSeconds} seconds");
        Console.WriteLine("Press Ctrl+C to stop...");

        // Process existing files on startup
        Task.Run(async () => await CheckForNewFiles());

        // Start polling timer
        _pollingTimer = new System.Threading.Timer(
            async _ => await CheckForNewFiles(),
            null,
            _pollingIntervalSeconds * 1000,
            _pollingIntervalSeconds * 1000);
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

                foreach (var file in newFiles)
                {
                    // Wait for file to be fully copied before processing
                    WaitForFileReady(file);

                    // Process the PDF file
                    await _pdfProcessorService.ProcessPdf(file);

                    // Remove from processed set after processing (file has been moved/deleted)
                    lock (_lock)
                    {
                        _processedFiles.Remove(file);
                    }
                }

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

    public void Stop()
    {
        _pollingTimer?.Dispose();
        _debounceTimer?.Dispose();
    }
}
