namespace PdfProcessor.Contracts;

public sealed record KkExtractionResponse
{
    public KkHeaderDto Header { get; init; } = new();
    public KkAddressDto Address { get; init; } = new();
    public IReadOnlyList<KkMemberDto> Members { get; init; } = [];
    public IReadOnlyList<FieldConfidenceDto> ConfidenceByField { get; init; } = [];
    public string RawText { get; init; } = string.Empty;
    public int ProcessingMs { get; init; }
    public OcrDiagnosticsDto Diagnostics { get; init; } = new();
}

public sealed class KkHeaderDto
{
    public string? KkNumber { get; init; }
    public string? HeadOfFamilyName { get; init; }
    public string? IssuedDate { get; init; }
}

public sealed class KkAddressDto
{
    public string? Province { get; init; }
    public string? RegencyOrCity { get; init; }
    public string? District { get; init; }
    public string? VillageOrSubdistrict { get; init; }
    public string? AddressLine { get; init; }
    public string? RtRw { get; init; }
    public string? PostalCode { get; init; }
}

public sealed class KkMemberDto
{
    public int? RowNo { get; init; }
    public string? FullName { get; init; }
    public string? Nik { get; init; }
    public string? Gender { get; init; }
    public string? PlaceOfBirth { get; init; }
    public string? DateOfBirth { get; init; }
    public string? Religion { get; init; }
    public string? Education { get; init; }
    public string? Occupation { get; init; }
    public string? BloodType { get; init; }
    public string? MaritalStatus { get; init; }
    public string? MarriageDate { get; init; }
    public string? FamilyRelationshipStatus { get; init; }
    public string? Citizenship { get; init; }
    public string? PassportNumber { get; init; }
    public string? KitapNumber { get; init; }
    public string? FatherName { get; init; }
    public string? MotherName { get; init; }
}

public sealed class FieldConfidenceDto
{
    public string Field { get; init; } = string.Empty;
    public string? Value { get; init; }
    public double Confidence { get; init; }
    public string Source { get; init; } = string.Empty;
}

public sealed class OcrDiagnosticsDto
{
    public bool UsedDirectPdfText { get; init; }
    public bool UsedImageOcr { get; init; }
    public bool PdfToTextAvailable { get; init; }
    public bool PdfToPpmAvailable { get; init; }
    public bool TesseractAvailable { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
