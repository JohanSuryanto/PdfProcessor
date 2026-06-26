using System.Text;
using Microsoft.Extensions.Options;
using PdfProcessor.Options;

namespace PdfProcessor.Services.Ocr;

public sealed class PdfTextExtractor : IPdfTextExtractor
{
    private readonly ICommandRunner _commandRunner;
    private readonly OcrToolOptions _options;

    public PdfTextExtractor(ICommandRunner commandRunner, IOptions<OcrToolOptions> options)
    {
        _commandRunner = commandRunner;
        _options = options.Value;
    }

    public async Task<PdfTextResult> TryExtractTextAsync(string pdfPath, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"kk_text_{Guid.NewGuid():N}.txt");

        var result = await _commandRunner.RunAsync(
            _options.PdfToTextPath,
            ["-layout", pdfPath, outputPath],
            workingDirectory: null,
            cancellationToken);

        if (result.ExecutableNotFound)
        {
            return new PdfTextResult(
                Success: false,
                ToolAvailable: false,
                Text: string.Empty,
                Warning: "pdftotext was not found. Install Poppler/Xpdf utils and ensure pdftotext is in PATH.");
        }

        if (result.ExitCode != 0 || !File.Exists(outputPath))
        {
            return new PdfTextResult(
                Success: false,
                ToolAvailable: true,
                Text: string.Empty,
                Warning: $"pdftotext failed: {result.StdErr}".Trim());
        }

        var text = await File.ReadAllTextAsync(outputPath, Encoding.UTF8, cancellationToken);
        TryDelete(outputPath);

        return new PdfTextResult(
            Success: !string.IsNullOrWhiteSpace(text),
            ToolAvailable: true,
            Text: text,
            Warning: string.IsNullOrWhiteSpace(text) ? "pdftotext produced empty text." : null);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
