using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using SystemKnowledgeHub.Api.Features.Attachments.Application.Models;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;

namespace SystemKnowledgeHub.Api.Features.Attachments.Application;

public sealed class AttachmentPreviewService(
    AttachmentOptions options,
    AttachmentStorage storage)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<AttachmentPreviewResult> Create(
        Attachment attachment,
        AttachmentMetadataResponse metadata,
        string? requestedSheet,
        CancellationToken cancellationToken)
    {
        if (!await storage.Verify(attachment, cancellationToken))
        {
            return new AttachmentPreviewResult(null, null, AttachmentFailure.StorageUnavailable);
        }

        var mode = AttachmentFilePolicy.GetPreviewMode(attachment);
        try
        {
            switch (mode)
            {
                case PreviewMode.Pdf:
                    return new AttachmentPreviewResult(
                        null,
                        new AttachmentContent(
                            storage.OpenRead(attachment.StorageKey),
                            attachment.ContentType,
                            attachment.OriginalFileName,
                            attachment.SizeBytes),
                        AttachmentFailure.None);
                case PreviewMode.Text:
                case PreviewMode.Markdown:
                    await using (var stream = storage.OpenRead(attachment.StorageKey))
                    {
                        var response = await ReadText(metadata, mode, stream, cancellationToken);
                        return new AttachmentPreviewResult(response, null, AttachmentFailure.None);
                    }
                case PreviewMode.Csv:
                    await using (var stream = storage.OpenRead(attachment.StorageKey))
                    {
                        var response = await ReadCsv(metadata, stream, cancellationToken);
                        return new AttachmentPreviewResult(response, null, AttachmentFailure.None);
                    }
                case PreviewMode.Spreadsheet:
                    if (attachment.SizeBytes > options.PreviewSpreadsheetMaxWorkbookBytes)
                    {
                        return new AttachmentPreviewResult(null, null, AttachmentFailure.PreviewLimitExceeded);
                    }
                    await using (var stream = storage.OpenRead(attachment.StorageKey))
                    {
                        var response = ReadSpreadsheet(metadata, stream, requestedSheet);
                        return new AttachmentPreviewResult(response, null, AttachmentFailure.None);
                    }
                default:
                    return new AttachmentPreviewResult(null, null, AttachmentFailure.PreviewNotSupported);
            }
        }
        catch (PreviewLimitException)
        {
            return new AttachmentPreviewResult(null, null, AttachmentFailure.PreviewLimitExceeded);
        }
        catch (RequestedSheetNotFoundException)
        {
            return new AttachmentPreviewResult(null, null, AttachmentFailure.Validation);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or XmlException or DecoderFallbackException or FormatException or OverflowException)
        {
            return new AttachmentPreviewResult(null, null, AttachmentFailure.StorageUnavailable);
        }
    }

    private async Task<AttachmentTextPreviewResponse> ReadText(
        AttachmentMetadataResponse metadata,
        PreviewMode mode,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var maximum = options.PreviewTextMaxBytes;
        var length = (int)Math.Min(stream.Length, maximum);
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        var validLength = bytes.Length;
        string text;
        while (true)
        {
            try
            {
                text = StrictUtf8.GetString(bytes.AsSpan(0, validLength));
                break;
            }
            catch (DecoderFallbackException) when (stream.Length > maximum && validLength > Math.Max(0, bytes.Length - 4))
            {
                validLength--;
            }
        }
        if (text.Length > 0 && text[0] == '\ufeff') text = text[1..];
        return new AttachmentTextPreviewResponse(
            metadata,
            mode.ToString(),
            text,
            stream.Length > validLength,
            validLength,
            maximum);
    }

    private async Task<AttachmentCsvPreviewResponse> ReadCsv(
        AttachmentMetadataResponse metadata,
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, StrictUtf8, false, 4096, leaveOpen: true);
        var buffer = new char[options.PreviewCsvMaxCharacters + 1];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken);
            if (count == 0) break;
            read += count;
        }

        var characterTruncated = read > options.PreviewCsvMaxCharacters;
        var textLength = Math.Min(read, options.PreviewCsvMaxCharacters);
        var parsed = ParseCsv(buffer.AsSpan(0, textLength));
        var reasons = new List<string>();
        if (characterTruncated) reasons.Add("Characters");
        if (parsed.RowsTruncated) reasons.Add("Rows");
        if (parsed.ColumnsTruncated) reasons.Add("Columns");
        return new AttachmentCsvPreviewResponse(
            metadata,
            PreviewMode.Csv.ToString(),
            parsed.Rows,
            reasons.Count > 0,
            reasons,
            options.PreviewCsvMaxRows,
            options.PreviewCsvMaxColumns,
            options.PreviewCsvMaxCharacters);
    }

    private CsvParseResult ParseCsv(ReadOnlySpan<char> text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var columnsTruncated = false;
        var rowsTruncated = false;

        void FinishField()
        {
            if (row.Count < options.PreviewCsvMaxColumns) row.Add(field.ToString());
            else columnsTruncated = true;
            field.Clear();
        }
        void FinishRow()
        {
            FinishField();
            if (rows.Count < options.PreviewCsvMaxRows) rows.Add(row.ToArray());
            else rowsTruncated = true;
            row = [];
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(character);
                }
                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    break;
                case ',':
                    FinishField();
                    break;
                case '\r':
                    if (index + 1 < text.Length && text[index + 1] == '\n') index++;
                    FinishRow();
                    if (rowsTruncated) index = text.Length;
                    break;
                case '\n':
                    FinishRow();
                    if (rowsTruncated) index = text.Length;
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }
        if (field.Length > 0 || row.Count > 0 || (text.Length > 0 && text[^1] == ',')) FinishRow();
        return new CsvParseResult(rows, rowsTruncated, columnsTruncated);
    }

    private AttachmentSpreadsheetPreviewResponse ReadSpreadsheet(
        AttachmentMetadataResponse metadata,
        Stream stream,
        string? requestedSheet)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidDataException("Workbook metadata is missing.");
        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidDataException("Workbook relationships are missing.");
        var relationships = ReadWorkbookRelationships(relationshipsEntry);
        var allSheets = ReadWorkbookSheets(workbookEntry, relationships);
        if (allSheets.Count == 0) throw new InvalidDataException("Workbook has no worksheets.");
        var availableSheets = allSheets.Take(options.PreviewSpreadsheetMaxSheets).ToArray();

        var selected = string.IsNullOrEmpty(requestedSheet)
            ? availableSheets.FirstOrDefault(sheet => sheet.Visibility == "Visible") ?? availableSheets[0]
            : availableSheets.SingleOrDefault(sheet => string.Equals(sheet.Name, requestedSheet, StringComparison.Ordinal));
        if (selected is null) throw new RequestedSheetNotFoundException();
        var sheetEntry = archive.GetEntry(selected.Path)
            ?? throw new InvalidDataException("Worksheet content is missing.");
        var sharedStrings = ReadSharedStrings(archive.GetEntry("xl/sharedStrings.xml"));
        var parsed = ReadWorksheet(sheetEntry, sharedStrings);
        var reasons = new List<string>();
        if (allSheets.Count > options.PreviewSpreadsheetMaxSheets) reasons.Add("Sheets");
        if (parsed.RowsTruncated) reasons.Add("Rows");
        if (parsed.ColumnsTruncated) reasons.Add("Columns");
        return new AttachmentSpreadsheetPreviewResponse(
            metadata,
            PreviewMode.Spreadsheet.ToString(),
            allSheets.Take(options.PreviewSpreadsheetMaxSheets)
                .Select(sheet => new AttachmentSpreadsheetSheetResponse(sheet.Name, sheet.Visibility))
                .ToArray(),
            selected.Name,
            parsed.Rows,
            reasons.Count > 0,
            reasons,
            options.PreviewSpreadsheetMaxSheets,
            options.PreviewSpreadsheetMaxRows,
            options.PreviewSpreadsheetMaxColumns);
    }

    private static Dictionary<string, string> ReadWorkbookRelationships(ZipArchiveEntry entry)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, AttachmentFilePolicy.SafeXmlSettings());
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship") continue;
            if (string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)) continue;
            var id = reader.GetAttribute("Id");
            var target = reader.GetAttribute("Target");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target)) continue;
            var normalized = NormalizePackagePath("xl", target);
            if (normalized is not null) result[id] = normalized;
        }
        return result;
    }

    private static List<WorkbookSheet> ReadWorkbookSheets(
        ZipArchiveEntry entry,
        IReadOnlyDictionary<string, string> relationships)
    {
        var result = new List<WorkbookSheet>();
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, AttachmentFilePolicy.SafeXmlSettings());
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "sheet") continue;
            var name = reader.GetAttribute("name");
            var relationshipId = reader.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            if (string.IsNullOrEmpty(name)
                || name.EnumerateRunes().Count() > 255
                || string.IsNullOrEmpty(relationshipId)
                || !relationships.TryGetValue(relationshipId, out var path)) continue;
            var state = reader.GetAttribute("state") switch
            {
                "hidden" => "Hidden",
                "veryHidden" => "VeryHidden",
                _ => "Visible",
            };
            result.Add(new WorkbookSheet(name, state, path));
        }
        return result;
    }

    private IReadOnlyList<string> ReadSharedStrings(ZipArchiveEntry? entry)
    {
        if (entry is null) return [];
        var strings = new List<string>();
        var totalCharacters = 0;
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, AttachmentFilePolicy.SafeXmlSettings());
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "si") continue;
            using var subtree = reader.ReadSubtree();
            var value = new StringBuilder();
            while (subtree.Read())
            {
                if (subtree.NodeType == XmlNodeType.Element && subtree.LocalName == "t")
                {
                    value.Append(subtree.ReadElementContentAsString());
                }
            }
            totalCharacters = checked(totalCharacters + value.Length);
            if (totalCharacters > options.PreviewSpreadsheetMaxSharedStringCharacters)
            {
                throw new PreviewLimitException();
            }
            strings.Add(value.ToString());
        }
        return strings;
    }

    private WorksheetParseResult ReadWorksheet(
        ZipArchiveEntry entry,
        IReadOnlyList<string> sharedStrings)
    {
        var rows = new List<AttachmentSpreadsheetRowResponse>();
        var rowsTruncated = false;
        var columnsTruncated = false;
        var displayCharacters = 0;
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, AttachmentFilePolicy.SafeXmlSettings());
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row") continue;
            var rowNumber = int.TryParse(reader.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRow)
                ? parsedRow
                : rows.Count + 1;
            if (rows.Count >= options.PreviewSpreadsheetMaxRows)
            {
                rowsTruncated = true;
                break;
            }

            using var rowSubtree = reader.ReadSubtree();
            var values = new SortedDictionary<int, string>();
            while (rowSubtree.Read())
            {
                if (rowSubtree.NodeType != XmlNodeType.Element || rowSubtree.LocalName != "c") continue;
                var column = ParseColumnIndex(rowSubtree.GetAttribute("r"));
                if (column <= 0) continue;
                var type = rowSubtree.GetAttribute("t");
                using var cellSubtree = rowSubtree.ReadSubtree();
                string? rawValue = null;
                var inlineValue = new StringBuilder();
                while (cellSubtree.Read())
                {
                    if (cellSubtree.NodeType != XmlNodeType.Element) continue;
                    if (cellSubtree.LocalName == "v") rawValue = cellSubtree.ReadElementContentAsString();
                    else if (cellSubtree.LocalName == "t") inlineValue.Append(cellSubtree.ReadElementContentAsString());
                    // Formula elements are deliberately ignored; only an existing cached value is read.
                }
                var value = type switch
                {
                    "s" when int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                        && index >= 0 && index < sharedStrings.Count => sharedStrings[index],
                    "inlineStr" => inlineValue.ToString(),
                    "b" => rawValue == "1" ? "TRUE" : rawValue == "0" ? "FALSE" : string.Empty,
                    _ => rawValue ?? inlineValue.ToString(),
                };
                displayCharacters = checked(displayCharacters + value.Length);
                if (displayCharacters > options.PreviewSpreadsheetMaxSharedStringCharacters)
                {
                    throw new PreviewLimitException();
                }
                if (column <= options.PreviewSpreadsheetMaxColumns) values[column] = value;
                else columnsTruncated = true;
            }
            var width = values.Count == 0 ? 0 : values.Keys.Max();
            var cells = new string[width];
            foreach (var pair in values) cells[pair.Key - 1] = pair.Value;
            rows.Add(new AttachmentSpreadsheetRowResponse(rowNumber, cells));
        }
        return new WorksheetParseResult(rows, rowsTruncated, columnsTruncated);
    }

    private static int ParseColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return 0;
        var result = 0;
        foreach (var character in reference)
        {
            if (character is >= 'A' and <= 'Z') result = checked(result * 26 + character - 'A' + 1);
            else if (character is >= 'a' and <= 'z') result = checked(result * 26 + character - 'a' + 1);
            else break;
        }
        return result;
    }

    private static string? NormalizePackagePath(string baseDirectory, string target)
    {
        var parts = new List<string>();
        var combined = target.StartsWith('/') ? target[1..] : $"{baseDirectory}/{target}";
        foreach (var part in combined.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (parts.Count == 0) return null;
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }
        return string.Join('/', parts);
    }

    private sealed record CsvParseResult(
        IReadOnlyList<IReadOnlyList<string>> Rows,
        bool RowsTruncated,
        bool ColumnsTruncated);
    private sealed record WorkbookSheet(string Name, string Visibility, string Path);
    private sealed record WorksheetParseResult(
        IReadOnlyList<AttachmentSpreadsheetRowResponse> Rows,
        bool RowsTruncated,
        bool ColumnsTruncated);
    private sealed class PreviewLimitException : Exception { }
    private sealed class RequestedSheetNotFoundException : Exception { }
}
