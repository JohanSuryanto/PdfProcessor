using System.Globalization;
using Microsoft.Extensions.Options;
using PdfProcessor.Options;

namespace PdfProcessor.Services.Ocr;

public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly ICommandRunner _commandRunner;
    private readonly OcrToolOptions _options;

    public TesseractOcrEngine(ICommandRunner commandRunner, IOptions<OcrToolOptions> options)
    {
        _commandRunner = commandRunner;
        _options = options.Value;
    }

    public async Task<OcrPageResult> ExtractTextAsync(string imagePath, CancellationToken cancellationToken)
    {
        var outputBase = Path.Combine(Path.GetTempPath(), $"kk_ocr_{Guid.NewGuid():N}");
        var tsvOutputPath = $"{outputBase}.tsv";

        var result = await _commandRunner.RunAsync(
            _options.TesseractPath,
            [
                imagePath,
                outputBase,
                "-l",
                _options.TesseractLanguages,
                "--psm",
                "6",
                "tsv"
            ],
            workingDirectory: null,
            cancellationToken);

        if (result.ExecutableNotFound)
        {
            return new OcrPageResult(
                Success: false,
                ToolAvailable: false,
                Text: string.Empty,
                MeanWordConfidence: 0,
                Warning: "tesseract was not found. Install Tesseract OCR and ensure tesseract is in PATH.");
        }

        if (result.ExitCode != 0 || !File.Exists(tsvOutputPath))
        {
            return new OcrPageResult(
                Success: false,
                ToolAvailable: true,
                Text: string.Empty,
                MeanWordConfidence: 0,
                Warning: $"tesseract failed: {result.StdErr}".Trim());
        }

        var tsv = await File.ReadAllLinesAsync(tsvOutputPath, cancellationToken);
        TryDelete(tsvOutputPath);

        var words = new List<TsvWord>();
        for (var i = 1; i < tsv.Length; i++)
        {
            var cols = tsv[i].Split('\t');
            if (cols.Length < 12)
            {
                continue;
            }

            var text = cols[11].Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!double.TryParse(cols[10], NumberStyles.Any, CultureInfo.InvariantCulture, out var conf))
            {
                conf = 0;
            }

            if (conf < 0)
            {
                conf = 0;
            }

            if (!int.TryParse(cols[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageNum))
            {
                pageNum = 1;
            }

            if (!int.TryParse(cols[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var blockNum))
            {
                blockNum = 0;
            }

            if (!int.TryParse(cols[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parNum))
            {
                parNum = 0;
            }

            if (!int.TryParse(cols[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNum))
            {
                lineNum = 0;
            }

            if (!int.TryParse(cols[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var wordNum))
            {
                wordNum = 0;
            }

            words.Add(new TsvWord(pageNum, blockNum, parNum, lineNum, wordNum, text, conf));
        }

        var groupedLines = words
            .GroupBy(word => (word.PageNum, word.BlockNum, word.ParagraphNum, word.LineNum))
            .OrderBy(group => group.Key.PageNum)
            .ThenBy(group => group.Key.BlockNum)
            .ThenBy(group => group.Key.ParagraphNum)
            .ThenBy(group => group.Key.LineNum)
            .Select(group =>
                string.Join(' ', group
                    .OrderBy(word => word.WordNum)
                    .Select(word => word.Text)));

        var combinedText = string.Join(Environment.NewLine, groupedLines);
        var meanConfidence = words.Count == 0 ? 0 : words.Average(word => word.Confidence) / 100d;

        return new OcrPageResult(
            Success: words.Count > 0,
            ToolAvailable: true,
            Text: combinedText,
            MeanWordConfidence: meanConfidence,
            Warning: words.Count == 0 ? "Tesseract returned no words from this page." : null);
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

    private sealed record TsvWord(
        int PageNum,
        int BlockNum,
        int ParagraphNum,
        int LineNum,
        int WordNum,
        string Text,
        double Confidence);
}
