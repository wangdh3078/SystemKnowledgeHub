using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;

namespace SystemKnowledgeHub.Api.Features.Attachments.Application;

public sealed partial class AttachmentStorage(
    AttachmentOptions options,
    ILogger<AttachmentStorage> logger)
{
    private const int BufferSize = 81_920;

    public async Task<StagedAttachment> Stage(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        EnsureStorageDirectories();
        var stagingPath = Path.Combine(options.StorageRoot, "staging", $"{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}.tmp");
        long size = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using var target = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
            var buffer = new byte[BufferSize];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                size = checked(size + read);
                if (size > maximumBytes)
                {
                    throw new AttachmentPayloadTooLargeException();
                }
                hash.AppendData(buffer.AsSpan(0, read));
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await target.FlushAsync(cancellationToken);
            if (size == 0)
            {
                throw new AttachmentEmptyPayloadException();
            }
            return new StagedAttachment(stagingPath, size, hash.GetHashAndReset());
        }
        catch
        {
            DeleteFileIfPresent(stagingPath);
            throw;
        }
    }

    public string Commit(StagedAttachment staged)
    {
        EnsureStorageDirectories();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var objectName = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            var shard = objectName[..2];
            var directory = Path.Combine(options.StorageRoot, "objects", shard);
            Directory.CreateDirectory(directory);
            EnsureNotReparsePoint(directory);
            var storageKey = $"objects/{shard}/{objectName}.bin";
            var targetPath = ResolveStorageKey(storageKey);
            try
            {
                File.Move(staged.StagingPath, targetPath);
                return storageKey;
            }
            catch (IOException) when (File.Exists(targetPath))
            {
                // A cryptographically random collision is harmless; retry with another key.
            }
        }
        throw new AttachmentStorageUnavailableException("Unable to allocate attachment storage.");
    }

    public async Task<bool> Verify(
        Attachment attachment,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = OpenRead(attachment.StorageKey);
            if (stream.Length != attachment.SizeBytes) return false;
            var actualHash = await SHA256.HashDataAsync(stream, cancellationToken);
            return CryptographicOperations.FixedTimeEquals(actualHash, attachment.Sha256);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or AttachmentStorageUnavailableException)
        {
            return false;
        }
    }

    public FileStream OpenRead(string storageKey)
    {
        var path = ResolveStorageKey(storageKey);
        if (!File.Exists(path))
        {
            throw new AttachmentStorageUnavailableException("Stored attachment is unavailable.");
        }
        EnsureNotReparsePoint(path);
        try
        {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AttachmentStorageUnavailableException("Stored attachment is unavailable.", exception);
        }
    }

    public void DeleteCommitted(string storageKey)
    {
        var path = ResolveStorageKey(storageKey);
        try
        {
            if (File.Exists(path))
            {
                EnsureNotReparsePoint(path);
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AttachmentStorageUnavailableException("Stored attachment could not be deleted.", exception);
        }
    }

    public void DeleteStaging(StagedAttachment staged) => DeleteFileIfPresent(staged.StagingPath);

    private void EnsureStorageDirectories()
    {
        try
        {
            Directory.CreateDirectory(options.StorageRoot);
            EnsureNotReparsePoint(options.StorageRoot);
            foreach (var child in new[] { "objects", "staging" })
            {
                var directory = Path.Combine(options.StorageRoot, child);
                Directory.CreateDirectory(directory);
                EnsureNotReparsePoint(directory);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AttachmentStorageUnavailableException("Attachment storage is unavailable.", exception);
        }
    }

    private string ResolveStorageKey(string storageKey)
    {
        if (!StorageKeyPattern().IsMatch(storageKey))
        {
            throw new AttachmentStorageUnavailableException("Stored attachment key is invalid.");
        }
        var relative = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(options.StorageRoot, relative));
        var relativeToRoot = Path.GetRelativePath(options.StorageRoot, path);
        if (Path.IsPathRooted(relativeToRoot)
            || relativeToRoot == ".."
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new AttachmentStorageUnavailableException("Stored attachment key is invalid.");
        }
        return path;
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new AttachmentStorageUnavailableException("Attachment storage cannot use reparse points.");
        }
    }

    private void DeleteFileIfPresent(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Attachment staging compensation could not remove a task-owned temporary file.");
        }
    }

    [GeneratedRegex("^objects/[0-9a-f]{2}/[0-9a-f]{32}\\.bin$", RegexOptions.CultureInvariant)]
    private static partial Regex StorageKeyPattern();
}

public sealed record StagedAttachment(string StagingPath, long SizeBytes, byte[] Sha256);

public sealed class AttachmentPayloadTooLargeException : Exception { }
public sealed class AttachmentEmptyPayloadException : Exception { }

public sealed class AttachmentStorageUnavailableException : Exception
{
    public AttachmentStorageUnavailableException(string message) : base(message) { }
    public AttachmentStorageUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
