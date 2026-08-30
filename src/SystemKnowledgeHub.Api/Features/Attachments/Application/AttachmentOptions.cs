using SystemKnowledgeHub.Api.Features.Attachments.Domain;

namespace SystemKnowledgeHub.Api.Features.Attachments.Application;

public sealed class AttachmentOptions
{
    public const long DefaultMaxImageBytes = 10L * 1024 * 1024;
    public const long DefaultMaxFileBytes = 50L * 1024 * 1024;
    public const int DefaultMaxStoredAttachmentsPerDocument = 100;
    public const int DefaultMemoryBufferThresholdBytes = 64 * 1024;
    public const long DefaultUploadRequestOverheadBytes = 2L * 1024 * 1024;
    public const int DefaultPreviewTextMaxBytes = 256 * 1024;
    public const int DefaultPreviewCsvMaxRows = 200;
    public const int DefaultPreviewCsvMaxColumns = 50;
    public const int DefaultPreviewCsvMaxCharacters = 256 * 1024;
    public const long DefaultPreviewSpreadsheetMaxWorkbookBytes = 10L * 1024 * 1024;
    public const int DefaultPreviewSpreadsheetMaxSheets = 20;
    public const int DefaultPreviewSpreadsheetMaxRows = 200;
    public const int DefaultPreviewSpreadsheetMaxColumns = 50;
    public const int DefaultPreviewSpreadsheetMaxSharedStringCharacters = 1024 * 1024;

    private const long AbsoluteMaximumUploadBytes = 200L * 1024 * 1024;
    private const int AbsoluteMaximumTextPreviewBytes = 2 * 1024 * 1024;
    private const int AbsoluteMaximumPreviewRows = 2_000;
    private const int AbsoluteMaximumPreviewColumns = 200;
    private const int AbsoluteMaximumPreviewCharacters = 4 * 1024 * 1024;
    private const int AbsoluteMaximumAttachmentsPerDocument = 1_000;
    private const int AbsoluteMaximumMemoryBufferThresholdBytes = 4 * 1024 * 1024;
    private const long AbsoluteMaximumUploadRequestOverheadBytes = 16L * 1024 * 1024;

    public required string StorageRoot { get; init; }
    public IReadOnlyList<string> AllowedImageExtensions { get; init; } = AttachmentFilePolicy.SupportedImageExtensions;
    public IReadOnlyList<string> AllowedFileExtensions { get; init; } = AttachmentFilePolicy.SupportedFileExtensions;
    public long MaxImageBytes { get; init; } = DefaultMaxImageBytes;
    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;
    public int MaxStoredAttachmentsPerDocument { get; init; } = DefaultMaxStoredAttachmentsPerDocument;
    public int MemoryBufferThresholdBytes { get; init; } = DefaultMemoryBufferThresholdBytes;
    public long UploadRequestOverheadBytes { get; init; } = DefaultUploadRequestOverheadBytes;
    public int PreviewTextMaxBytes { get; init; } = DefaultPreviewTextMaxBytes;
    public int PreviewCsvMaxRows { get; init; } = DefaultPreviewCsvMaxRows;
    public int PreviewCsvMaxColumns { get; init; } = DefaultPreviewCsvMaxColumns;
    public int PreviewCsvMaxCharacters { get; init; } = DefaultPreviewCsvMaxCharacters;
    public long PreviewSpreadsheetMaxWorkbookBytes { get; init; } = DefaultPreviewSpreadsheetMaxWorkbookBytes;
    public int PreviewSpreadsheetMaxSheets { get; init; } = DefaultPreviewSpreadsheetMaxSheets;
    public int PreviewSpreadsheetMaxRows { get; init; } = DefaultPreviewSpreadsheetMaxRows;
    public int PreviewSpreadsheetMaxColumns { get; init; } = DefaultPreviewSpreadsheetMaxColumns;
    public int PreviewSpreadsheetMaxSharedStringCharacters { get; init; } = DefaultPreviewSpreadsheetMaxSharedStringCharacters;

    public long MaximumRequestBodyBytes => checked(
        Math.Max(MaxImageBytes, MaxFileBytes) + UploadRequestOverheadBytes);

    public static bool TryCreate(
        IConfiguration configuration,
        IHostEnvironment environment,
        out AttachmentOptions? options,
        out string? error)
    {
        options = null;
        error = null;
        var configuredRoot = configuration["Attachments:StorageRoot"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            if (!environment.IsDevelopment())
            {
                error = "Attachments:StorageRoot is required outside Development and must identify isolated persistent storage.";
                return false;
            }

            configuredRoot = Path.Combine("App_Data", "attachments");
        }

        string resolvedRoot;
        try
        {
            resolvedRoot = Path.GetFullPath(
                Path.IsPathRooted(configuredRoot)
                    ? configuredRoot
                    : Path.Combine(environment.ContentRootPath, configuredRoot));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "Attachments:StorageRoot is not a valid filesystem path.";
            return false;
        }

        if (!environment.IsDevelopment())
        {
            if (!Path.IsPathRooted(configuredRoot))
            {
                error = "Attachments:StorageRoot must be an absolute persistent path outside the application deployment directory.";
                return false;
            }
            if (IsPathWithinDirectory(resolvedRoot, environment.ContentRootPath))
            {
                error = "Attachments:StorageRoot must be outside the application deployment directory.";
                return false;
            }
        }

        var rootPath = Path.GetPathRoot(resolvedRoot)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = resolvedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(rootPath, normalizedRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            error = "Attachments:StorageRoot cannot be a filesystem root.";
            return false;
        }

        if (!AttachmentFilePolicy.TryNormalizeConfiguredExtensions(
                configuration["Attachments:AllowedImageExtensions"]
                    ?? string.Join(',', AttachmentFilePolicy.SupportedImageExtensions),
                AttachmentKind.Image,
                out var allowedImageExtensions,
                out error)
            || !AttachmentFilePolicy.TryNormalizeConfiguredExtensions(
                configuration["Attachments:AllowedFileExtensions"]
                    ?? string.Join(',', AttachmentFilePolicy.SupportedFileExtensions),
                AttachmentKind.File,
                out var allowedFileExtensions,
                out error))
        {
            return false;
        }

        if (!TryReadLong(configuration, "Attachments:MaxImageBytes", DefaultMaxImageBytes, out var maxImageBytes)
            || !TryReadLong(configuration, "Attachments:MaxFileBytes", DefaultMaxFileBytes, out var maxFileBytes)
            || !TryReadInt(configuration, "Attachments:MaxStoredAttachmentsPerDocument", DefaultMaxStoredAttachmentsPerDocument, out var maxStored)
            || !TryReadInt(configuration, "Attachments:MemoryBufferThresholdBytes", DefaultMemoryBufferThresholdBytes, out var memoryBufferThreshold)
            || !TryReadLong(configuration, "Attachments:UploadRequestOverheadBytes", DefaultUploadRequestOverheadBytes, out var uploadRequestOverhead)
            || !TryReadInt(configuration, "Attachments:PreviewTextMaxBytes", DefaultPreviewTextMaxBytes, out var textBytes)
            || !TryReadInt(configuration, "Attachments:PreviewCsvMaxRows", DefaultPreviewCsvMaxRows, out var csvRows)
            || !TryReadInt(configuration, "Attachments:PreviewCsvMaxColumns", DefaultPreviewCsvMaxColumns, out var csvColumns)
            || !TryReadInt(configuration, "Attachments:PreviewCsvMaxCharacters", DefaultPreviewCsvMaxCharacters, out var csvCharacters)
            || !TryReadLong(configuration, "Attachments:PreviewSpreadsheetMaxWorkbookBytes", DefaultPreviewSpreadsheetMaxWorkbookBytes, out var workbookBytes)
            || !TryReadInt(configuration, "Attachments:PreviewSpreadsheetMaxSheets", DefaultPreviewSpreadsheetMaxSheets, out var spreadsheetSheets)
            || !TryReadInt(configuration, "Attachments:PreviewSpreadsheetMaxRows", DefaultPreviewSpreadsheetMaxRows, out var spreadsheetRows)
            || !TryReadInt(configuration, "Attachments:PreviewSpreadsheetMaxColumns", DefaultPreviewSpreadsheetMaxColumns, out var spreadsheetColumns)
            || !TryReadInt(configuration, "Attachments:PreviewSpreadsheetMaxSharedStringCharacters", DefaultPreviewSpreadsheetMaxSharedStringCharacters, out var sharedStringCharacters))
        {
            error = "Attachments size/count/preview limits must be valid integers.";
            return false;
        }

        if (maxImageBytes is <= 0 or > AbsoluteMaximumUploadBytes
            || maxFileBytes is <= 0 or > AbsoluteMaximumUploadBytes
            || maxStored is <= 0 or > AbsoluteMaximumAttachmentsPerDocument
            || memoryBufferThreshold is <= 0 or > AbsoluteMaximumMemoryBufferThresholdBytes
            || uploadRequestOverhead is <= 0 or > AbsoluteMaximumUploadRequestOverheadBytes
            || textBytes is <= 0 or > AbsoluteMaximumTextPreviewBytes
            || csvRows is <= 0 or > AbsoluteMaximumPreviewRows
            || csvColumns is <= 0 or > AbsoluteMaximumPreviewColumns
            || csvCharacters is <= 0 or > AbsoluteMaximumPreviewCharacters
            || workbookBytes is <= 0 or > AbsoluteMaximumUploadBytes
            || spreadsheetSheets is <= 0 or > 100
            || spreadsheetRows is <= 0 or > AbsoluteMaximumPreviewRows
            || spreadsheetColumns is <= 0 or > AbsoluteMaximumPreviewColumns
            || sharedStringCharacters is <= 0 or > AbsoluteMaximumPreviewCharacters)
        {
            error = "Attachments size/count/preview limits must be positive and within the documented safety ceilings.";
            return false;
        }

        options = new AttachmentOptions
        {
            StorageRoot = resolvedRoot,
            AllowedImageExtensions = allowedImageExtensions,
            AllowedFileExtensions = allowedFileExtensions,
            MaxImageBytes = maxImageBytes,
            MaxFileBytes = maxFileBytes,
            MaxStoredAttachmentsPerDocument = maxStored,
            MemoryBufferThresholdBytes = memoryBufferThreshold,
            UploadRequestOverheadBytes = uploadRequestOverhead,
            PreviewTextMaxBytes = textBytes,
            PreviewCsvMaxRows = csvRows,
            PreviewCsvMaxColumns = csvColumns,
            PreviewCsvMaxCharacters = csvCharacters,
            PreviewSpreadsheetMaxWorkbookBytes = workbookBytes,
            PreviewSpreadsheetMaxSheets = spreadsheetSheets,
            PreviewSpreadsheetMaxRows = spreadsheetRows,
            PreviewSpreadsheetMaxColumns = spreadsheetColumns,
            PreviewSpreadsheetMaxSharedStringCharacters = sharedStringCharacters,
        };
        return true;
    }

    private static bool TryReadInt(IConfiguration configuration, string key, int fallback, out int value)
    {
        var configured = configuration[key];
        if (configured is null)
        {
            value = fallback;
            return true;
        }
        return int.TryParse(configured, out value);
    }

    private static bool TryReadLong(IConfiguration configuration, string key, long fallback, out long value)
    {
        var configured = configuration[key];
        if (configured is null)
        {
            value = fallback;
            return true;
        }
        return long.TryParse(configured, out value);
    }

    private static bool IsPathWithinDirectory(string path, string directory)
    {
        var relativePath = Path.GetRelativePath(Path.GetFullPath(directory), Path.GetFullPath(path));
        return !Path.IsPathRooted(relativePath)
            && (relativePath == "."
                || (!relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && relativePath != ".."));
    }
}
