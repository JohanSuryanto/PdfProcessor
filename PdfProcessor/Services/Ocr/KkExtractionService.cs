using System.Diagnostics;
using Microsoft.Extensions.Options;
using PdfProcessor.Contracts;
using PdfProcessor.Options;

namespace PdfProcessor.Services.Ocr;

public sealed class KkExtractionService : IKkExtractionService
{
    private readonly OcrToolOptions _options;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IPdfPageRenderer _pdfPageRenderer;
    private readonly IOcrEngine _ocrEngine;
    private readonly IKkParser _kkParser;

    public KkExtractionService(
        IOptions<OcrToolOptions> options,
        IPdfTextExtractor pdfTextExtractor,
        IPdfPageRenderer pdfPageRenderer,
        IOcrEngine ocrEngine,
        IKkParser kkParser)
    {
        _options = options.Value;
        _pdfTextExtractor = pdfTextExtractor;
        _pdfPageRenderer = pdfPageRenderer;
        _ocrEngine = ocrEngine;
        _kkParser = kkParser;
    }

    public async Task<KkExtractionResponse> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kk_extract_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var warnings = new List<string>();
        var usedDirectPdfText = false;
        var usedImageOcr = false;
        var pdfToTextAvailable = true;
        var pdfToPpmAvailable = true;
        var tesseractAvailable = true;

        try
        {
            var pdfPath = Path.Combine(tempRoot, SanitizeFileName(fileName));
            await using (var fileStream = File.Create(pdfPath))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            var directTextResult = await _pdfTextExtractor.TryExtractTextAsync(pdfPath, cancellationToken);
            pdfToTextAvailable = directTextResult.ToolAvailable;
            if (!string.IsNullOrWhiteSpace(directTextResult.Warning))
            {
                warnings.Add(directTextResult.Warning);
            }

            if (directTextResult.Success && directTextResult.Text.Length >= _options.MinimumTextLength)
            {
                usedDirectPdfText = true;
                return BuildResponse(
                    _kkParser.Parse(directTextResult.Text, sourceTag: "pdf_text", defaultConfidence: 0.9),
                    warnings,
                    usedDirectPdfText,
                    usedImageOcr,
                    pdfToTextAvailable,
                    pdfToPpmAvailable,
                    tesseractAvailable,
                    stopwatch);
            }

            var imageOutputDir = Path.Combine(tempRoot, "pages");
            var renderResult = await _pdfPageRenderer.RenderPagesAsync(pdfPath, imageOutputDir, cancellationToken);
            pdfToPpmAvailable = renderResult.ToolAvailable;
            if (!string.IsNullOrWhiteSpace(renderResult.Warning))
            {
                warnings.Add(renderResult.Warning);
            }

            if (!renderResult.Success)
            {
                return BuildResponse(
                    _kkParser.Parse(directTextResult.Text, sourceTag: "empty", defaultConfidence: 0),
                    warnings,
                    usedDirectPdfText,
                    usedImageOcr,
                    pdfToTextAvailable,
                    pdfToPpmAvailable,
                    tesseractAvailable,
                    stopwatch);
            }

            usedImageOcr = true;
            var ocrTextParts = new List<string>();
            var pageConfidences = new List<double>();

            foreach (var pageImage in renderResult.ImagePaths)
            {
                var pageOcr = await _ocrEngine.ExtractTextAsync(pageImage, cancellationToken);
                tesseractAvailable = pageOcr.ToolAvailable;
                if (!string.IsNullOrWhiteSpace(pageOcr.Warning))
                {
                    warnings.Add(pageOcr.Warning);
                }

                if (!string.IsNullOrWhiteSpace(pageOcr.Text))
                {
                    ocrTextParts.Add(pageOcr.Text);
                    pageConfidences.Add(pageOcr.MeanWordConfidence);
                }
            }

            var ocrText = string.Join(Environment.NewLine, ocrTextParts);
            var confidence = pageConfidences.Count == 0 ? 0.5 : pageConfidences.Average();
            var parsed = _kkParser.Parse(ocrText, sourceTag: "image_ocr", defaultConfidence: confidence);

            return BuildResponse(
                parsed,
                warnings,
                usedDirectPdfText,
                usedImageOcr,
                pdfToTextAvailable,
                pdfToPpmAvailable,
                tesseractAvailable,
                stopwatch);
        }
        finally
        {
            if (!_options.KeepTemporaryFiles && Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safe = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "upload.pdf" : safe;
    }

    private static KkExtractionResponse BuildResponse(
        KkExtractionResponse parsed,
        IReadOnlyList<string> warnings,
        bool usedDirectPdfText,
        bool usedImageOcr,
        bool pdfToTextAvailable,
        bool pdfToPpmAvailable,
        bool tesseractAvailable,
        Stopwatch stopwatch) =>
        parsed with
        {
            ProcessingMs = (int)stopwatch.ElapsedMilliseconds,
            Diagnostics = new OcrDiagnosticsDto
            {
                UsedDirectPdfText = usedDirectPdfText,
                UsedImageOcr = usedImageOcr,
                PdfToTextAvailable = pdfToTextAvailable,
                PdfToPpmAvailable = pdfToPpmAvailable,
                TesseractAvailable = tesseractAvailable,
                Warnings = warnings
            }
        };
}
