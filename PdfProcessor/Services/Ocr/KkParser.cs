using System.Globalization;
using System.Text.RegularExpressions;
using PdfProcessor.Contracts;

namespace PdfProcessor.Services.Ocr;

public sealed partial class KkParser : IKkParser
{
    private static readonly string[] AddressLabels =
    [
        "PROVINSI",
        "KABUPATEN/KOTA",
        "KABUPATEN",
        "KECAMATAN",
        "DESA/KELURAHAN",
        "KELURAHAN",
        "ALAMAT",
        "RT/RW",
        "KODE POS",
        "NAMA KEPALA KELUARGA"
    ];

    private static readonly string[] EducationVocabulary =
    [
        "TIDAK/BELUM SEKOLAH",
        "TIDAK/BLM SEKOLAH",
        "BELUM TAMAT SD/SEDERAJAT",
        "TAMAT SD/SEDERAJAT",
        "SLTP/SEDERAJAT",
        "SLTA/SEDERAJAT",
        "DIPLOMA I/II",
        "AKADEMI/DIPLOMA III/SARJANA MUDA",
        "DIPLOMA IV/STRATA I",
        "STRATA III",
        "STRATA II",
        "D4",
        "D3",
        "D2",
        "D1",
        "S3",
        "S2",
        "S1",
        "SD",
        "SMP",
        "SMA"
    ];

    private static readonly string[] MaritalStatusVocabulary =
    [
        "KAWIN BELUM TERCATAT",
        "KAWIN TERCATAT",
        "BELUM KAWIN",
        "CERAI HIDUP",
        "CERAI MATI",
        "KAWIN"
    ];

    private static readonly string[] FamilyRelationVocabulary =
    [
        "KEPALA KELUARGA",
        "FAMILI LAIN",
        "ORANG TUA",
        "MENANTU",
        "MERTUA",
        "SUAMI",
        "ISTRI",
        "ANAK",
        "CUCU"
    ];

    private static readonly string[] ReligionVocabulary =
    [
        "KEPERCAYAAN",
        "KONGHUCU",
        "KRISTEN",
        "KATOLIK",
        "BUDDHA",
        "HINDU",
        "ISLAM"
    ];

    public KkExtractionResponse Parse(string rawText, string sourceTag, double defaultConfidence)
    {
        var normalizedText = rawText.Replace("\r\n", "\n").Replace('\r', '\n');
        var rawLines = normalizedText
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        var compactLines = rawLines.Select(CollapseSpaces).ToArray();
        var compactText = string.Join('\n', compactLines);

        var kkNumber = ExtractKkNumber(compactText, compactLines);
        var headOfFamily = ExtractHeadOfFamily(compactLines, kkNumber);
        var issuedDate = ExtractIssuedDate(compactText, compactLines);

        var address = new KkAddressDto
        {
            Province = ExtractAddressLabelValue(compactLines, "PROVINSI"),
            RegencyOrCity = ExtractAddressLabelValue(compactLines, "KABUPATEN/KOTA", "KABUPATEN"),
            District = ExtractAddressLabelValue(compactLines, "KECAMATAN"),
            VillageOrSubdistrict = ExtractAddressLabelValue(compactLines, "DESA/KELURAHAN", "KELURAHAN"),
            AddressLine = ExtractAddressLabelValue(compactLines, "ALAMAT"),
            RtRw = ExtractAddressLabelValue(compactLines, "RT/RW"),
            PostalCode = ExtractPostalCode(compactLines)
        };

        var members = ParseMembers(rawLines, compactLines);

        return new KkExtractionResponse
        {
            Header = new KkHeaderDto
            {
                KkNumber = kkNumber,
                HeadOfFamilyName = headOfFamily,
                IssuedDate = issuedDate
            },
            Address = address,
            Members = members,
            ConfidenceByField = BuildConfidence(sourceTag, defaultConfidence, kkNumber, headOfFamily, issuedDate, address, members.Count),
            RawText = compactText
        };
    }

    private static IReadOnlyList<KkMemberDto> ParseMembers(IReadOnlyList<string> rawLines, IReadOnlyList<string> compactLines)
    {
        var builders = new Dictionary<int, MemberBuilder>();
        var knownFamilyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var secondaryColumns = DetectSecondaryColumns(rawLines);

        for (var index = 0; index < compactLines.Count; index++)
        {
            var line = compactLines[index];
            var primaryMatch = PrimaryRowRegex().Match(line);
            if (primaryMatch.Success)
            {
                var rowNo = int.Parse(primaryMatch.Groups["no"].Value, CultureInfo.InvariantCulture);
                var builder = GetOrCreate(builders, rowNo);

                var tail = primaryMatch.Groups["tail"].Value.Trim();
                var (education, occupation, bloodType) = ParseEducationOccupationAndBlood(tail);

                builder.RowNo = rowNo;
                builder.FullName = primaryMatch.Groups["name"].Value.Trim().NullIfDash();
                builder.Nik = primaryMatch.Groups["nik"].Value.Trim().NullIfDash();
                builder.Gender = NormalizeGender(primaryMatch.Groups["gender"].Value.Trim());
                builder.PlaceOfBirth = primaryMatch.Groups["birthPlace"].Value.Trim().NullIfDash();
                builder.DateOfBirth = primaryMatch.Groups["birthDate"].Value.Trim().NullIfDash();
                builder.Religion = NormalizeReligion(primaryMatch.Groups["religion"].Value.Trim().NullIfDash());
                builder.Education = education;
                builder.Occupation = occupation;
                builder.BloodType = bloodType;
                if (!string.IsNullOrWhiteSpace(builder.FullName))
                {
                    knownFamilyNames.Add(builder.FullName);
                }
                continue;
            }
        }

        for (var index = 0; index < compactLines.Count; index++)
        {
            var line = compactLines[index];
            var supplementaryMatch = SecondaryRowRegex().Match(line);
            if (!supplementaryMatch.Success)
            {
                continue;
            }

            var supRowNo = int.Parse(supplementaryMatch.Groups["no"].Value, CultureInfo.InvariantCulture);
            var supBuilder = GetOrCreate(builders, supRowNo);

            supBuilder.RowNo = supRowNo;
            supBuilder.MaritalStatus = supplementaryMatch.Groups["marital"].Value.Trim().NullIfDash();
            supBuilder.MarriageDate = supplementaryMatch.Groups["marriageDate"].Value.Trim().NullIfDash();
            supBuilder.FamilyRelationshipStatus = supplementaryMatch.Groups["relation"].Value.Trim().NullIfDash();
            supBuilder.Citizenship = supplementaryMatch.Groups["citizenship"].Value.Trim().NullIfDash();

            var rawLine = rawLines[index];
            var rawSupplementary = SecondaryRowRawRegex().Match(rawLine);
            var (passport, kitap, fatherName, motherName) = ParseDocumentAndParents(
                supplementaryMatch.Groups["tail"].Value.Trim(),
                rawSupplementary.Success ? rawSupplementary.Groups["tail"].Value.Trim() : null,
                rawLine,
                rawSupplementary.Success ? rawSupplementary.Groups["tail"].Index : -1,
                secondaryColumns,
                knownFamilyNames);
            supBuilder.PassportNumber = passport;
            supBuilder.KitapNumber = kitap;
            supBuilder.FatherName = fatherName;
            supBuilder.MotherName = motherName;
        }

        return builders.Values
            .Where(member => !string.IsNullOrWhiteSpace(member.Nik))
            .OrderBy(member => member.RowNo)
            .Select(member => member.ToDto())
            .ToArray();
    }

    private static (string? Education, string? Occupation, string? BloodType) ParseEducationOccupationAndBlood(string tail)
    {
        var tailMatch = TailBloodRegex().Match(tail);
        var blood = tailMatch.Success ? tailMatch.Groups["blood"].Value.Trim().NullIfDash() : null;
        var eduOcc = tailMatch.Success ? tailMatch.Groups["eduOcc"].Value.Trim() : tail;

        var education = FindLongestPrefix(eduOcc, EducationVocabulary);
        if (education is null)
        {
            return (null, eduOcc.NullIfDash(), blood);
        }

        var occupation = eduOcc[education.Length..].Trim().NullIfDash();
        return (education.NullIfDash(), occupation, blood);
    }

    private static (string? Passport, string? Kitap, string? FatherName, string? MotherName) ParseDocumentAndParents(
        string compactTail,
        string? rawTail,
        string rawLine,
        int rawTailStart,
        SecondaryColumns? secondaryColumns,
        IReadOnlyCollection<string> knownFamilyNames)
    {
        if (string.IsNullOrWhiteSpace(compactTail))
        {
            return (null, null, null, null);
        }

        var tokens = compactTail.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return (null, null, null, null);
        }

        var cursor = 0;
        string? passport = null;
        string? kitap = null;

        if (tokens[cursor] != "-")
        {
            passport = tokens[cursor].NullIfDash();
        }
        cursor++;

        if (cursor < tokens.Length && LooksLikeDocumentToken(tokens[cursor]))
        {
            if (tokens[cursor] != "-")
            {
                kitap = tokens[cursor];
            }
            cursor++;
        }

        var parentTokens = tokens[cursor..];
        var parentLine = string.Join(' ', parentTokens).NullIfDash();
        if (string.IsNullOrWhiteSpace(parentLine))
        {
            return (passport.NullIfDash(), kitap.NullIfDash(), null, null);
        }

        var rawParentLine = SliceRawTailAfterTokenCount(rawTail, cursor);
        var split = SplitParentNames(parentLine, rawParentLine, knownFamilyNames);

        if (TryParseWithColumnBoundaries(rawLine, rawTailStart, secondaryColumns, out var byColumn))
        {
            var resolvedPassport = byColumn.Passport.NullIfDash() ?? passport.NullIfDash();
            var resolvedKitap = byColumn.Kitap.NullIfDash() ?? kitap.NullIfDash();
            var resolvedFather = byColumn.FatherName.NullIfDash();
            var resolvedMother = byColumn.MotherName.NullIfDash();

            if (ShouldPreferHeuristicParentSplit(resolvedFather, resolvedMother, split.FatherName, split.MotherName, rawParentLine))
            {
                resolvedFather = split.FatherName;
                resolvedMother = split.MotherName;
            }

            return (resolvedPassport, resolvedKitap, resolvedFather, resolvedMother);
        }

        return (passport.NullIfDash(), kitap.NullIfDash(), split.FatherName, split.MotherName);
    }

    private static bool ShouldPreferHeuristicParentSplit(
        string? boundaryFather,
        string? boundaryMother,
        string? heuristicFather,
        string? heuristicMother,
        string? rawParentLine)
    {
        if (string.IsNullOrWhiteSpace(heuristicFather) || string.IsNullOrWhiteSpace(heuristicMother))
        {
            return false;
        }

        if (HasClearParentGap(rawParentLine))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(boundaryFather) || string.IsNullOrWhiteSpace(boundaryMother))
        {
            return true;
        }

        var boundaryMotherTokens = CountTokens(boundaryMother);
        var heuristicMotherTokens = CountTokens(heuristicMother);
        var boundaryFatherTokens = CountTokens(boundaryFather);
        var heuristicFatherTokens = CountTokens(heuristicFather);

        return boundaryMotherTokens <= 1 &&
               heuristicMotherTokens >= 2 &&
               boundaryFatherTokens >= 2 &&
               heuristicFatherTokens < boundaryFatherTokens;
    }

    private static bool HasClearParentGap(string? rawParentLine) =>
        !string.IsNullOrWhiteSpace(rawParentLine) && Regex.IsMatch(rawParentLine, @"\S\s{2,}\S");

    private static int CountTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return value.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static bool TryParseWithColumnBoundaries(
        string rawLine,
        int rawTailStart,
        SecondaryColumns? secondaryColumns,
        out (string? Passport, string? Kitap, string? FatherName, string? MotherName) result)
    {
        result = (null, null, null, null);
        if (secondaryColumns is null || string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        var passportStart = Math.Max(secondaryColumns.PassportStart, Math.Max(0, rawTailStart));
        if (passportStart >= secondaryColumns.KitapStart || secondaryColumns.KitapStart >= secondaryColumns.FatherStart || secondaryColumns.FatherStart >= secondaryColumns.MotherStart)
        {
            return false;
        }

        var line = rawLine.PadRight(secondaryColumns.MotherStart + 1);
        var passport = ExtractColumn(line, passportStart, secondaryColumns.KitapStart).NullIfDash();
        var kitap = ExtractColumn(line, secondaryColumns.KitapStart, secondaryColumns.FatherStart).NullIfDash();
        var father = ExtractColumn(line, secondaryColumns.FatherStart, secondaryColumns.MotherStart).NullIfDash();
        var mother = ExtractColumn(line, secondaryColumns.MotherStart, null).NullIfDash();

        result = (passport, kitap, father, mother);
        return true;
    }

    private static string ExtractColumn(string line, int start, int? end)
    {
        var safeStart = Math.Clamp(start, 0, line.Length);
        var safeEnd = end is null ? line.Length : Math.Clamp(end.Value, safeStart, line.Length);
        if (safeEnd <= safeStart)
        {
            return string.Empty;
        }

        return CollapseSpaces(line[safeStart..safeEnd].Trim());
    }

    private static SecondaryColumns? DetectSecondaryColumns(IReadOnlyList<string> rawLines)
    {
        foreach (var line in rawLines)
        {
            var passport = Regex.Match(line, @"No\.\s*Paspor", RegexOptions.IgnoreCase);
            var kitap = Regex.Match(line, @"No\.\s*KITAP", RegexOptions.IgnoreCase);
            var father = Regex.Match(line, @"\bAyah\b", RegexOptions.IgnoreCase);
            var mother = Regex.Match(line, @"\bIbu\b", RegexOptions.IgnoreCase);

            if (!passport.Success || !kitap.Success || !father.Success || !mother.Success)
            {
                continue;
            }

            if (passport.Index < kitap.Index && kitap.Index < father.Index && father.Index < mother.Index)
            {
                return new SecondaryColumns(passport.Index, kitap.Index, father.Index, mother.Index);
            }
        }

        return null;
    }

    private static (string? FatherName, string? MotherName) SplitParentNames(
        string parentLine,
        string? rawParentLine,
        IReadOnlyCollection<string> knownFamilyNames)
    {
        var parentNikMatches = NikRegex().Matches(parentLine);
        if (parentNikMatches.Count >= 2)
        {
            return (parentNikMatches[0].Value.NullIfDash(), parentNikMatches[1].Value.NullIfDash());
        }

        if (!string.IsNullOrWhiteSpace(rawParentLine))
        {
            var columns = Regex.Split(rawParentLine.Trim(), @"\s{2,}")
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(CollapseSpaces)
                .ToArray();

            if (columns.Length >= 2)
            {
                return (columns[0].NullIfDash(), string.Join(' ', columns.Skip(1)).NullIfDash());
            }
        }

        var normalizedParentLine = CollapseSpaces(parentLine);
        var known = knownFamilyNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(CollapseSpaces)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(name => name.Length)
            .ToArray();

        foreach (var first in known)
        {
            if (!normalizedParentLine.StartsWith(first, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remaining = normalizedParentLine[first.Length..].Trim();
            if (string.IsNullOrWhiteSpace(remaining))
            {
                return (first, null);
            }

            foreach (var second in known)
            {
                if (remaining.Equals(second, StringComparison.OrdinalIgnoreCase))
                {
                    return (first, second);
                }
            }
        }

        var tokens = normalizedParentLine
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 2)
        {
            if (tokens.Length == 2)
            {
                return (tokens[0].NullIfDash(), tokens[1].NullIfDash());
            }

            if (tokens.Length == 3)
            {
                return (string.Join(' ', tokens[0], tokens[1]).NullIfDash(), tokens[2].NullIfDash());
            }

            var splitIndex = tokens.Length / 2;
            var father = string.Join(' ', tokens[..splitIndex]).NullIfDash();
            var mother = string.Join(' ', tokens[splitIndex..]).NullIfDash();
            return (father, mother);
        }

        var candidates = Regex.Matches(parentLine, @"[A-Z][A-Z\s\.\-]{2,}")
            .Select(match => CollapseSpaces(match.Value).NullIfDash())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            return (null, null);
        }

        if (candidates.Length == 1)
        {
            return (candidates[0], null);
        }

        return (candidates[0], candidates[1]);
    }

    private static string? SliceRawTailAfterTokenCount(string? rawTail, int skipTokenCount)
    {
        if (string.IsNullOrWhiteSpace(rawTail))
        {
            return null;
        }

        var working = rawTail.AsSpan().Trim();
        var skip = Math.Max(0, skipTokenCount);
        for (var i = 0; i < skip; i++)
        {
            var tokenMatch = LeadingTokenRegex().Match(working.ToString());
            if (!tokenMatch.Success)
            {
                return null;
            }

            working = working[tokenMatch.Length..].TrimStart();
        }

        var remaining = working.ToString().Trim();
        return remaining.NullIfDash();
    }

    private static string? ExtractKkNumber(string compactText, IReadOnlyList<string> lines)
    {
        var labeled = Regex.Match(compactText, @"NO\.?\s*(KK)?\s*:?\s*(\d{16})", RegexOptions.IgnoreCase);
        if (labeled.Success)
        {
            return labeled.Groups[2].Value;
        }

        foreach (var line in lines.Take(12))
        {
            var match = NikRegex().Match(line);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    private static string? ExtractHeadOfFamily(IReadOnlyList<string> lines, string? kkNumber)
    {
        if (!string.IsNullOrWhiteSpace(kkNumber))
        {
            for (var i = 0; i < lines.Count - 1; i++)
            {
                if (!lines[i].Contains(kkNumber, StringComparison.Ordinal))
                {
                    continue;
                }

                var next = lines[i + 1];
                if (!next.StartsWith(":", StringComparison.Ordinal))
                {
                    continue;
                }

                var namePart = next[1..].Trim();
                namePart = StripEmbeddedLabels(namePart);
                return namePart.NullIfDash();
            }
        }

        for (var i = 0; i < lines.Count - 1; i++)
        {
            if (!lines[i].Contains("NAMA KEPALA KELUARGA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = StripEmbeddedLabels(lines[i + 1]).NullIfDash();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ExtractIssuedDate(string compactText, IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (!line.Contains("DIKELUARKAN TANGGAL", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var date = FirstDateRegex().Match(line).Value.NullIfDash();
            if (!string.IsNullOrWhiteSpace(date))
            {
                return date;
            }
        }

        var fromText = Regex.Match(compactText, @"DIKELUARKAN\s+TANGGAL\s*:?\s*(\d{2}[-/]\d{2}[-/]\d{4})", RegexOptions.IgnoreCase);
        return fromText.Success ? fromText.Groups[1].Value : null;
    }

    private static string? ExtractAddressLabelValue(IReadOnlyList<string> lines, params string[] labels)
    {
        foreach (var label in labels)
        {
            foreach (var line in lines)
            {
                var value = ExtractLabelFromLine(line, label);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? ExtractPostalCode(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var value = ExtractLabelFromLine(line, "KODE POS");
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var postal = Regex.Match(value, @"\b\d{5}\b").Value;
            if (!string.IsNullOrWhiteSpace(postal))
            {
                return postal;
            }
        }

        return null;
    }

    private static string? ExtractLabelFromLine(string line, string label)
    {
        var allLabelsPattern = string.Join('|', AddressLabels.Select(Regex.Escape));
        var pattern = $@"\b{Regex.Escape(label)}\b\s*:\s*(?<value>.*?)\s*(?=(?:\b(?:{allLabelsPattern})\b\s*:)|$)";
        var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var value = StripEmbeddedLabels(match.Groups["value"].Value);
        return value.NullIfDash();
    }

    private static string StripEmbeddedLabels(string value)
    {
        var cleaned = value.Trim().Trim(':').Trim();
        foreach (var label in AddressLabels)
        {
            var marker = Regex.Match(cleaned, $@"(?:^|\s)\b{Regex.Escape(label)}\b\s*:", RegexOptions.IgnoreCase);
            if (marker.Success && marker.Index >= 0)
            {
                cleaned = cleaned[..marker.Index].Trim();
            }
        }

        return cleaned.Trim().Trim(':').Trim();
    }

    private static string? NormalizeGender(string? gender)
    {
        if (string.IsNullOrWhiteSpace(gender))
        {
            return null;
        }

        if (gender.Equals("L", StringComparison.OrdinalIgnoreCase))
        {
            return "LAKI-LAKI";
        }

        if (gender.Equals("P", StringComparison.OrdinalIgnoreCase))
        {
            return "PEREMPUAN";
        }

        return gender;
    }

    private static string? NormalizeReligion(string? religion)
    {
        if (string.IsNullOrWhiteSpace(religion))
        {
            return null;
        }

        var normalized = FindMatchFromVocabulary(religion, ReligionVocabulary);
        return normalized ?? religion;
    }

    private static bool LooksLikeDocumentToken(string token) =>
        token == "-" || Regex.IsMatch(token, @"^(?=.*\d)[A-Z0-9./-]{4,}$", RegexOptions.IgnoreCase);

    private static string? FindLongestPrefix(string input, IReadOnlyList<string> vocabulary)
    {
        foreach (var candidate in vocabulary.OrderByDescending(item => item.Length))
        {
            if (input.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindMatchFromVocabulary(string input, IReadOnlyList<string> vocabulary)
    {
        foreach (var candidate in vocabulary.OrderByDescending(item => item.Length))
        {
            if (Regex.IsMatch(input, $@"\b{Regex.Escape(candidate)}\b", RegexOptions.IgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static MemberBuilder GetOrCreate(IDictionary<int, MemberBuilder> builders, int rowNo)
    {
        if (!builders.TryGetValue(rowNo, out var builder))
        {
            builder = new MemberBuilder { RowNo = rowNo };
            builders[rowNo] = builder;
        }

        return builder;
    }

    private static IReadOnlyList<FieldConfidenceDto> BuildConfidence(
        string sourceTag,
        double defaultConfidence,
        string? kkNumber,
        string? headOfFamily,
        string? issuedDate,
        KkAddressDto address,
        int memberCount)
    {
        return
        [
            CreateConfidence("header.kkNumber", kkNumber, defaultConfidence, sourceTag),
            CreateConfidence("header.headOfFamilyName", headOfFamily, defaultConfidence, sourceTag),
            CreateConfidence("header.issuedDate", issuedDate, defaultConfidence, sourceTag),
            CreateConfidence("address.province", address.Province, defaultConfidence, sourceTag),
            CreateConfidence("address.regencyOrCity", address.RegencyOrCity, defaultConfidence, sourceTag),
            CreateConfidence("address.district", address.District, defaultConfidence, sourceTag),
            CreateConfidence("address.villageOrSubdistrict", address.VillageOrSubdistrict, defaultConfidence, sourceTag),
            CreateConfidence("address.addressLine", address.AddressLine, defaultConfidence, sourceTag),
            CreateConfidence("address.rtRw", address.RtRw, defaultConfidence, sourceTag),
            CreateConfidence("address.postalCode", address.PostalCode, defaultConfidence, sourceTag),
            new FieldConfidenceDto
            {
                Field = "members.count",
                Value = memberCount.ToString(CultureInfo.InvariantCulture),
                Confidence = memberCount == 0 ? 0 : defaultConfidence,
                Source = sourceTag
            }
        ];
    }

    private static FieldConfidenceDto CreateConfidence(string field, string? value, double defaultConfidence, string sourceTag) =>
        new()
        {
            Field = field,
            Value = value,
            Confidence = string.IsNullOrWhiteSpace(value) ? 0 : defaultConfidence,
            Source = sourceTag
        };

    private static string CollapseSpaces(string line) =>
        MultiWhitespaceRegex().Replace(line.Trim(), " ");

    [GeneratedRegex(@"\b\d{16}\b")]
    private static partial Regex NikRegex();

    [GeneratedRegex(@"\d{2}[-/]\d{2}[-/]\d{4}")]
    private static partial Regex FirstDateRegex();

    [GeneratedRegex(@"^(?<no>\d{1,2})\s+(?<name>.+?)\s+(?<nik>\d{16})\s+(?<gender>LAKI-LAKI|PEREMPUAN|L|P)\s+(?<birthPlace>.+?)\s+(?<birthDate>\d{2}[-/]\d{2}[-/]\d{4})\s+(?<religion>[A-Z/]+)\s+(?<tail>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PrimaryRowRegex();

    [GeneratedRegex(@"^(?<eduOcc>.+?)\s+(?<blood>TIDAK TAHU|AB|A|B|O)$", RegexOptions.IgnoreCase)]
    private static partial Regex TailBloodRegex();

    [GeneratedRegex(@"^(?<no>\d{1,2})\s+(?<marital>BELUM KAWIN|KAWIN TERCATAT|KAWIN BELUM TERCATAT|KAWIN|CERAI HIDUP|CERAI MATI)(?:\s+(?<marriageDate>\d{2}[-/]\d{2}[-/]\d{4}|-))?\s+(?<relation>KEPALA KELUARGA|FAMILI LAIN|ORANG TUA|MENANTU|MERTUA|SUAMI|ISTRI|ANAK|CUCU)\s+(?<citizenship>WNI|WNA)\s+(?<tail>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex SecondaryRowRegex();

    [GeneratedRegex(@"^(?<no>\d{1,2})\s+(?<marital>BELUM KAWIN|KAWIN TERCATAT|KAWIN BELUM TERCATAT|KAWIN|CERAI HIDUP|CERAI MATI)(?:\s+(?<marriageDate>\d{2}[-/]\d{2}[-/]\d{4}|-))?\s+(?<relation>KEPALA KELUARGA|FAMILI LAIN|ORANG TUA|MENANTU|MERTUA|SUAMI|ISTRI|ANAK|CUCU)\s+(?<citizenship>WNI|WNA)\s+(?<tail>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SecondaryRowRawRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();

    [GeneratedRegex(@"^\S+\s*")]
    private static partial Regex LeadingTokenRegex();

    private sealed class MemberBuilder
    {
        public int RowNo { get; set; }
        public string? FullName { get; set; }
        public string? Nik { get; set; }
        public string? Gender { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Religion { get; set; }
        public string? Education { get; set; }
        public string? Occupation { get; set; }
        public string? BloodType { get; set; }
        public string? MaritalStatus { get; set; }
        public string? MarriageDate { get; set; }
        public string? FamilyRelationshipStatus { get; set; }
        public string? Citizenship { get; set; }
        public string? PassportNumber { get; set; }
        public string? KitapNumber { get; set; }
        public string? FatherName { get; set; }
        public string? MotherName { get; set; }

        public KkMemberDto ToDto() =>
            new()
            {
                RowNo = RowNo,
                FullName = FullName,
                Nik = Nik,
                Gender = Gender,
                PlaceOfBirth = PlaceOfBirth,
                DateOfBirth = DateOfBirth,
                Religion = Religion,
                Education = Education,
                Occupation = Occupation,
                BloodType = BloodType,
                MaritalStatus = MaritalStatus,
                MarriageDate = MarriageDate,
                FamilyRelationshipStatus = FamilyRelationshipStatus,
                Citizenship = Citizenship,
                PassportNumber = PassportNumber,
                KitapNumber = KitapNumber,
                FatherName = FatherName,
                MotherName = MotherName
            };
    }

    private sealed record SecondaryColumns(int PassportStart, int KitapStart, int FatherStart, int MotherStart);
}

file static class KkParserTextExtensions
{
    public static string? NullIfDash(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed == "-" ? null : trimmed;
    }
}
