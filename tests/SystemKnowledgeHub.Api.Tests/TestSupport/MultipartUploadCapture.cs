using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public sealed record MultipartUploadObservation(
    long? RequestContentLength,
    long FormFileLength,
    byte[] First24Bytes,
    byte[] Sha256);

public sealed class MultipartUploadCapture
{
    private readonly ConcurrentDictionary<string, MultipartUploadObservation> _observations = new();

    public void Record(string fileName, MultipartUploadObservation observation) =>
        _observations[fileName] = observation;

    public MultipartUploadObservation GetRequired(string fileName) =>
        _observations.TryGetValue(fileName, out var observation)
            ? observation
            : throw new InvalidOperationException($"No multipart observation was captured for {fileName}.");
}

public sealed class MultipartUploadCaptureStartupFilter(MultipartUploadCapture capture) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => application =>
    {
        application.Use(async (context, nextMiddleware) =>
        {
            await nextMiddleware();

            if (!HttpMethods.IsPost(context.Request.Method)
                || context.Request.Path.Value?.EndsWith("/attachments", StringComparison.OrdinalIgnoreCase) != true
                || !context.Request.HasFormContentType)
            {
                return;
            }

            IFormCollection form;
            try
            {
                form = await context.Request.ReadFormAsync(context.RequestAborted);
            }
            catch (Exception exception) when (exception is BadHttpRequestException or InvalidDataException or IOException)
            {
                return;
            }

            var file = form.Files.SingleOrDefault(item =>
                string.Equals(item.Name, "file", StringComparison.Ordinal));
            if (file is null)
            {
                return;
            }

            await using var stream = file.OpenReadStream();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var first24 = new byte[Math.Min(24, checked((int)Math.Min(file.Length, 24)))];
            var first24Offset = 0;
            var buffer = new byte[64 * 1024];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, context.RequestAborted);
                if (read == 0)
                {
                    break;
                }

                if (first24Offset < first24.Length)
                {
                    var prefixRead = Math.Min(read, first24.Length - first24Offset);
                    buffer.AsSpan(0, prefixRead).CopyTo(first24.AsSpan(first24Offset));
                    first24Offset += prefixRead;
                }
                hash.AppendData(buffer, 0, read);
            }

            capture.Record(
                file.FileName,
                new MultipartUploadObservation(
                    context.Request.ContentLength,
                    file.Length,
                    first24,
                    hash.GetHashAndReset()));
        });

        next(application);
    };
}
