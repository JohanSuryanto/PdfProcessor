using System.Text;
using System.Text.Json;
using PdfProcessor.Routes;
using PdfProcessor.Contracts;

namespace PdfProcessor.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly string _failedFolderPath;
    public int FailedCount { get; private set; }
    public int SuccessCount { get; private set; }

    public ApiService(HttpClient httpClient, string apiBaseUrl = "https://localhost:5000/api", string failedFolderPath = "Failed")
    {
        _httpClient = httpClient;
        _apiBaseUrl = apiBaseUrl;
        _failedFolderPath = failedFolderPath;
        FailedCount = 0;
        SuccessCount = 0;
    }

    public void ResetCounts()
    {
        FailedCount = 0;
        SuccessCount = 0;
    }

    private string GetUniqueFileName(string folderPath, string fileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
        var newFileName = $"{fileNameWithoutExtension}_{guid}{extension}";
        
        return Path.Combine(folderPath, newFileName);
    }

    public async Task SendPdfNotification(string fileName, string filePath, KkExtractionResponse? extractionResult = null)
    {
        var apiUrl = $"{_apiBaseUrl}/{ApiRoutes.PdfUpload}";

        try
        {
            Console.WriteLine($"Processing file: {fileName}");

            var payload = new
            {
                FileName = fileName,
                FilePath = filePath,
                DetectedAt = DateTime.UtcNow,
                ExtractionData = extractionResult
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Using ApiRoutes constant instead of hardcoded string
            var response = await _httpClient.PostAsync($"{apiUrl}", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Successfully sent {apiUrl} for {fileName} to API");
                SuccessCount++;
                
                // Delete the file on successful API response
                try
                {
                    File.Delete(filePath);
                    Console.WriteLine($"Deleted file: {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting file {fileName}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Failed to send {apiUrl} for {fileName}. Status: {response.StatusCode}");
                FailedCount++;
                
                // Move file to Failed folder on API failure
                try
                {
                    var failedFilePath = GetUniqueFileName(_failedFolderPath, fileName);
                    File.Move(filePath, failedFilePath);
                    Console.WriteLine($"Moved failed file to: {failedFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error moving file {fileName} to Failed folder: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending {apiUrl} for {fileName}: {ex.Message}");
            FailedCount++;
            
            // Move file to Failed folder on exception
            try
            {
                var failedFilePath = GetUniqueFileName(_failedFolderPath, fileName);
                File.Move(filePath, failedFilePath);
                Console.WriteLine($"Moved failed file to: {failedFilePath}");
            }
            catch (Exception moveEx)
            {
                Console.WriteLine($"Error moving file {fileName} to Failed folder: {moveEx.Message}");
            }
        }
    }
}
