namespace PdfProcessor.Options;

public sealed class OcrToolOptions
{
    public const string SectionName = "OcrTools";

    public string PdfToTextPath { get; set; } = "pdftotext";
    public string PdfToPpmPath { get; set; } = "pdftoppm";
    public string TesseractPath { get; set; } = "tesseract";
    public string TesseractLanguages { get; set; } = "ind+eng";
    public int PdfRenderDpi { get; set; } = 300;
    public int MaxFileSizeMb { get; set; } = 10;
    public int MinimumTextLength { get; set; } = 120;
    public bool KeepTemporaryFiles { get; set; }
}
