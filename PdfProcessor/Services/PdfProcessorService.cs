using System.IO;
using System.Text.Json;
using PdfProcessor.Services.Ocr;
using PdfProcessor.Contracts;

namespace PdfProcessor.Services;

public class PdfProcessorService
{
    private readonly ApiService _apiService;
    private readonly IKkExtractionService _kkExtractionService;

    public PdfProcessorService(ApiService apiService, IKkExtractionService kkExtractionService)
    {
        _apiService = apiService;
        _kkExtractionService = kkExtractionService;
    }

    public async Task ProcessPdf(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        Console.WriteLine($"Detected PDF: {fileName}");

        // Send notification to API
        //await _apiService.SendPdfNotification(fileName, filePath);

        KkExtractionResponse? extractionResult;
        using (var fileStream = File.OpenRead(filePath))
        {
            extractionResult = await _kkExtractionService.ExtractAsync(fileStream, fileName, CancellationToken.None);
        }

        // Output extraction result as JSON
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(extractionResult, jsonOptions));

        // Send notification to API with extraction data
        await _apiService.SendPdfNotification(fileName, filePath, extractionResult);

    }
}
