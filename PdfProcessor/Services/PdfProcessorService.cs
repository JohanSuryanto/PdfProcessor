using System.IO;

namespace PdfProcessor.Services;

public class PdfProcessorService
{
    private readonly ApiService _apiService;

    public PdfProcessorService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task ProcessPdf(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        Console.WriteLine($"Detected PDF: {fileName}");
        
        // Send notification to API
        await _apiService.SendPdfNotification(fileName, filePath);
        
        // TODO: In future phases, implement PDF text extraction here
    }
}
