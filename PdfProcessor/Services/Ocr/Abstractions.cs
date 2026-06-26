using PdfProcessor.Contracts;

namespace PdfProcessor.Services.Ocr;

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken);
}

public interface IPdfTextExtractor
{
    Task<PdfTextResult> TryExtractTextAsync(string pdfPath, CancellationToken cancellationToken);
}

public interface IPdfPageRenderer
{
    Task<PdfRenderResult> RenderPagesAsync(string pdfPath, string outputDirectory, CancellationToken cancellationToken);
}

public interface IOcrEngine
{
    Task<OcrPageResult> ExtractTextAsync(string imagePath, CancellationToken cancellationToken);
}

public interface IKkParser
{
    KkExtractionResponse Parse(string rawText, string sourceTag, double defaultConfidence);
}

public interface IKkExtractionService
{
    Task<KkExtractionResponse> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken);
}

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr, bool ExecutableNotFound);

public sealed record PdfTextResult(bool Success, bool ToolAvailable, string Text, string? Warning);

public sealed record PdfRenderResult(bool Success, bool ToolAvailable, IReadOnlyList<string> ImagePaths, string? Warning);

public sealed record OcrPageResult(bool Success, bool ToolAvailable, string Text, double MeanWordConfidence, string? Warning);
