using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;

namespace SystemKnowledgeHub.Api.Features.Attachments.Application;

public sealed class AttachmentFilePolicy
{
    private const int MaximumPackageEntries = 10_000;
    private const long MaximumPackageUncompressedBytes = 500L * 1024 * 1024;
    private const long MaximumContentTypesBytes = 2L * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly IReadOnlyDictionary<string, AttachmentTypeDescriptor> Types =
        new Dictionary<string, AttachmentTypeDescriptor>(StringComparer.Ordinal)
        {
            [".png"] = new(AttachmentKind.Image, "image/png", PreviewMode.Image, Recognition.Png),
            [".jpg"] = new(AttachmentKind.Image, "image/jpeg", PreviewMode.Image, Recognition.Jpeg),
            [".jpeg"] = new(AttachmentKind.Image, "image/jpeg", PreviewMode.Image, Recognition.Jpeg),
            [".gif"] = new(AttachmentKind.Image, "image/gif", PreviewMode.Image, Recognition.Gif),
            [".webp"] = new(AttachmentKind.Image, "image/webp", PreviewMode.Image, Recognition.Webp),
            [".pdf"] = new(AttachmentKind.File, "application/pdf", PreviewMode.Pdf, Recognition.Pdf),
            [".docx"] = new(AttachmentKind.File, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", PreviewMode.None, Recognition.Docx),
            [".xlsx"] = new(AttachmentKind.File, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", PreviewMode.Spreadsheet, Recognition.Xlsx),
            [".pptx"] = new(AttachmentKind.File, "application/vnd.openxmlformats-officedocument.presentationml.presentation", PreviewMode.None, Recognition.Pptx),
            [".txt"] = new(AttachmentKind.File, "text/plain", PreviewMode.Text, Recognition.Utf8Text),
            [".log"] = new(AttachmentKind.File, "text/plain", PreviewMode.Text, Recognition.Utf8Text),
            [".sql"] = new(AttachmentKind.File, "text/plain", PreviewMode.Text, Recognition.Utf8Text),
            [".md"] = new(AttachmentKind.File, "text/markdown", PreviewMode.Markdown, Recognition.Utf8Text),
            [".csv"] = new(AttachmentKind.File, "text/csv", PreviewMode.Csv, Recognition.Utf8Text),
            [".json"] = new(AttachmentKind.File, "application/json", PreviewMode.Text, Recognition.Utf8Text),
            [".xml"] = new(AttachmentKind.File, "application/xml", PreviewMode.Text, Recognition.Utf8Text),
            [".zip"] = new(AttachmentKind.File, "application/zip", PreviewMode.None, Recognition.Zip),
        };

    public AttachmentUploadDescriptor ValidateRequest(string? suppliedFileName, string? declaredContentType)
    {
        string normalizedName;
        try
        {
            normalizedName = (suppliedFileName ?? string.Empty).Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException exception)
        {
            throw new AttachmentTypeRejectedException("文件名包含无效的 Unicode 字符。", exception);
        }
        ValidateFileName(normalizedName);
        var extension = Path.GetExtension(normalizedName).ToLowerInvariant();
        if (!Types.TryGetValue(extension, out var descriptor))
        {
            throw new AttachmentTypeRejectedException("文件扩展名不在允许列表中。");
        }

        var declared = declaredContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(declared)
            && declared != "application/octet-stream"
            && !string.Equals(declared, descriptor.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new AttachmentTypeRejectedException("浏览器声明的内容类型与文件类型不一致。");
        }

        return new AttachmentUploadDescriptor(
            normalizedName,
            extension,
            descriptor.Kind,
            descriptor.ContentType,
            descriptor.PreviewMode,
            descriptor.Recognition);
    }

    public async Task ValidateContent(
        StagedAttachment staged,
        AttachmentUploadDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                staged.StagingPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            switch (descriptor.Recognition)
            {
                case Recognition.Png:
                    await ValidatePng(stream, cancellationToken);
                    break;
                case Recognition.Jpeg:
                    await ValidateJpeg(stream, cancellationToken);
                    break;
                case Recognition.Gif:
                    await ValidatePrefix(stream, [.. "GIF87a"u8], [.. "GIF89a"u8], cancellationToken);
                    break;
                case Recognition.Webp:
                    await ValidateWebp(stream, cancellationToken);
                    break;
                case Recognition.Pdf:
                    await ValidatePrefix(stream, [.. "%PDF-"u8], null, cancellationToken);
                    break;
                case Recognition.Utf8Text:
                    await ValidateUtf8Text(stream, cancellationToken);
                    break;
                case Recognition.Zip:
                    ValidateZip(stream, null);
                    break;
                case Recognition.Docx:
                case Recognition.Xlsx:
                case Recognition.Pptx:
                    ValidateZip(stream, descriptor.Recognition);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported attachment recognition policy.");
            }
        }
        catch (AttachmentTypeRejectedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or DecoderFallbackException or IOException)
        {
            throw new AttachmentTypeRejectedException("文件内容与允许的类型或安全结构不匹配。", exception);
        }
    }

    public static PreviewMode GetPreviewMode(Attachment attachment) =>
        Types.TryGetValue(attachment.Extension, out var descriptor)
            && descriptor.Kind == attachment.Kind
            && string.Equals(descriptor.ContentType, attachment.ContentType, StringComparison.Ordinal)
                ? descriptor.PreviewMode
                : PreviewMode.None;

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName is "." or ".."
            || fileName.EnumerateRunes().Count() > 255
            || fileName.EndsWith(' ')
            || fileName.EndsWith('.')
            || fileName.IndexOfAny(['/', '\\', ':', '\0', '\r', '\n']) >= 0
            || fileName.Any(char.IsControl)
            || Path.IsPathRooted(fileName))
        {
            throw new AttachmentTypeRejectedException("文件名不安全或不符合长度规则。");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            throw new AttachmentTypeRejectedException("文件名必须包含允许的终端扩展名。");
        }
        var baseName = fileName[..^extension.Length];
        var deviceName = baseName.Split('.', 2)[0];
        if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (deviceName.Length == 4
                && (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && deviceName[3] is >= '1' and <= '9'))
        {
            throw new AttachmentTypeRejectedException("文件名使用了系统保留名称。");
        }
    }

    private static async Task ValidatePng(Stream stream, CancellationToken cancellationToken)
    {
        var header = await ReadHeader(stream, 24, cancellationToken);
        if (!IsValidPngHeader(header))
        {
            throw new AttachmentTypeRejectedException("PNG 文件头无效。");
        }
    }

    private static async Task ValidateJpeg(Stream stream, CancellationToken cancellationToken)
    {
        var header = await ReadHeader(stream, 3, cancellationToken);
        if (header[0] != 0xff || header[1] != 0xd8 || header[2] != 0xff)
        {
            throw new AttachmentTypeRejectedException("JPEG 文件头无效。");
        }
        stream.Seek(-2, SeekOrigin.End);
        var trailer = await ReadHeader(stream, 2, cancellationToken);
        if (trailer[0] != 0xff || trailer[1] != 0xd9)
        {
            throw new AttachmentTypeRejectedException("JPEG 文件尾无效。");
        }
    }

    private static async Task ValidateWebp(Stream stream, CancellationToken cancellationToken)
    {
        var header = await ReadHeader(stream, 12, cancellationToken);
        if (!IsValidWebpHeader(header))
        {
            throw new AttachmentTypeRejectedException("WEBP 文件头无效。");
        }
    }

    private static async Task ValidatePrefix(
        Stream stream,
        byte[] first,
        byte[]? alternative,
        CancellationToken cancellationToken)
    {
        var header = await ReadHeader(stream, first.Length, cancellationToken);
        if (!header.SequenceEqual(first) && (alternative is null || !header.SequenceEqual(alternative)))
        {
            throw new AttachmentTypeRejectedException("文件签名无效。");
        }
    }

    private static bool IsValidPngHeader(byte[] header)
    {
        var span = header.AsSpan();
        return span[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(span.Slice(8, 4)) == 13
            && span.Slice(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(span.Slice(16, 4)) > 0
            && BinaryPrimitives.ReadUInt32BigEndian(span.Slice(20, 4)) > 0;
    }

    private static bool IsValidWebpHeader(byte[] header)
    {
        var span = header.AsSpan();
        return span[..4].SequenceEqual("RIFF"u8)
            && span.Slice(8, 4).SequenceEqual("WEBP"u8);
    }

    private static async Task ValidateUtf8Text(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, StrictUtf8, detectEncodingFromByteOrderMarks: false, 4096, leaveOpen: true);
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (buffer.AsSpan(0, read).Contains('\0'))
            {
                throw new AttachmentTypeRejectedException("文本文件不能包含 NUL 字符。");
            }
        }
    }

    private static void ValidateZip(Stream stream, Recognition? packageType)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumPackageEntries)
        {
            throw new AttachmentTypeRejectedException("ZIP/OOXML 条目数量不符合安全限制。");
        }

        long totalLength = 0;
        foreach (var entry in archive.Entries)
        {
            totalLength = checked(totalLength + entry.Length);
            if (totalLength > MaximumPackageUncompressedBytes)
            {
                throw new AttachmentTypeRejectedException("ZIP/OOXML 解压后声明大小超过安全限制。");
            }
            if (entry.Length > 0 && (entry.CompressedLength == 0 || entry.Length > entry.CompressedLength * 100L))
            {
                throw new AttachmentTypeRejectedException("ZIP/OOXML 压缩比例超过安全限制。");
            }
        }

        if (packageType is null) return;
        var contentTypesEntry = archive.GetEntry("[Content_Types].xml");
        if (contentTypesEntry is null || contentTypesEntry.Length is <= 0 or > MaximumContentTypesBytes)
        {
            throw new AttachmentTypeRejectedException("OOXML 内容类型清单缺失或过大。");
        }
        if (archive.Entries.Any(entry => entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new AttachmentTypeRejectedException("不允许包含宏的 Office 文件。");
        }

        var requiredContentType = packageType switch
        {
            Recognition.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            Recognition.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
            Recognition.Pptx => "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
            _ => throw new InvalidOperationException("Unsupported OOXML package type."),
        };
        var foundRequired = false;
        using var contentTypesStream = contentTypesEntry.Open();
        using var reader = XmlReader.Create(contentTypesStream, SafeXmlSettings());
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;
            var contentType = reader.GetAttribute("ContentType");
            if (string.IsNullOrEmpty(contentType)) continue;
            if (contentType.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("vbaProject", StringComparison.OrdinalIgnoreCase))
            {
                throw new AttachmentTypeRejectedException("不允许包含宏的 Office 文件。");
            }
            foundRequired |= string.Equals(contentType, requiredContentType, StringComparison.Ordinal);
        }
        if (!foundRequired)
        {
            throw new AttachmentTypeRejectedException("OOXML 主文档类型与扩展名不匹配。");
        }
    }

    internal static XmlReaderSettings SafeXmlSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        MaxCharactersFromEntities = 0,
        MaxCharactersInDocument = 8L * 1024 * 1024,
    };

    private static async Task<byte[]> ReadHeader(Stream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken);
            if (read == 0) throw new AttachmentTypeRejectedException("文件内容过短。");
            offset += read;
        }
        return buffer;
    }

    public enum Recognition
    {
        Png,
        Jpeg,
        Gif,
        Webp,
        Pdf,
        Utf8Text,
        Zip,
        Docx,
        Xlsx,
        Pptx,
    }

    private sealed record AttachmentTypeDescriptor(
        AttachmentKind Kind,
        string ContentType,
        PreviewMode PreviewMode,
        Recognition Recognition);
}

public sealed record AttachmentUploadDescriptor(
    string OriginalFileName,
    string Extension,
    AttachmentKind Kind,
    string ContentType,
    PreviewMode PreviewMode,
    AttachmentFilePolicy.Recognition Recognition);

public enum PreviewMode
{
    Image,
    Pdf,
    Text,
    Markdown,
    Csv,
    Spreadsheet,
    None,
}

public sealed class AttachmentTypeRejectedException : Exception
{
    public AttachmentTypeRejectedException(string message) : base(message) { }
    public AttachmentTypeRejectedException(string message, Exception innerException) : base(message, innerException) { }
}
