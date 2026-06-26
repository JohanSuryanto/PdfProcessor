using Microsoft.Extensions.Options;
using PdfProcessor.Options;

namespace PdfProcessor.Services.Ocr;

public sealed class PdfPageRenderer : IPdfPageRenderer
{
    private readonly ICommandRunner _commandRunner;
    private readonly OcrToolOptions _options;

    public PdfPageRenderer(ICommandRunner commandRunner, IOptions<OcrToolOptions> options)
    {
        _commandRunner = commandRunner;
        _options = options.Value;
    }

    public async Task<PdfRenderResult> RenderPagesAsync(string pdfPath, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputPrefix = Path.Combine(outputDirectory, "page");

        var result = await _commandRunner.RunAsync(
            _options.PdfToPpmPath,
            ["-r", _options.PdfRenderDpi.ToString(), "-png", pdfPath, outputPrefix],
            workingDirectory: null,
            cancellationToken);

        if (result.ExecutableNotFound)
        {
            return new PdfRenderResult(
                Success: false,
                ToolAvailable: false,
                ImagePaths: [],
                Warning: "pdftoppm was not found. Install Poppler/Xpdf utils and ensure pdftoppm is in PATH.");
        }

        var generatedImages = Directory
            .GetFiles(outputDirectory, "page-*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (result.ExitCode != 0 || generatedImages.Length == 0)
        {
            return new PdfRenderResult(
                Success: false,
                ToolAvailable: true,
                ImagePaths: [],
                Warning: $"pdftoppm failed: {result.StdErr}".Trim());
        }

        return new PdfRenderResult(
            Success: true,
            ToolAvailable: true,
            ImagePaths: generatedImages,
            Warning: null);
    }
}
