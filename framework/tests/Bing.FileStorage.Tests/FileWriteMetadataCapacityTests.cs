using System.Security.Cryptography;
using Bing.FileStorage.FileSystem;
using Xunit;

namespace Bing.FileStorage.Tests;

public sealed class FileWriteMetadataCapacityTests : IDisposable
{
    private const int PartSize = 5 * 1024 * 1024;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bing-storage-metadata-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData(14, true)]
    [InlineData(15, true)]
    [InlineData(16, true)]
    [InlineData(14, false)]
    [InlineData(15, false)]
    [InlineData(16, false)]
    public async Task SaveAsync_PersistsMetadataAtPublicLimit(int metadataCount, bool empty)
    {
        var address = new FileAddress("attachments", $"metadata/{metadataCount}-{empty.ToString().ToLowerInvariant()}");
        var metadata = CreateMetadata(metadataCount);
        var content = empty ? Array.Empty<byte>() : new byte[] { 2, 4, 6, 8, 10 };
        var storage = CreateStorage();

        using var source = new MemoryStream(content, writable: false);
        var result = await storage.SaveAsync(
            new FileWriteRequest(address, content.Length, FileWriteMode.CreateOnly, "application/octet-stream", metadata),
            source);

        Assert.Equal((long)content.Length, result.Properties.Length);
        AssertMetadata(metadata, result.Properties.Metadata);

        using var read = new MemoryStream();
        await storage.ReadAsync(address, async (stream, cancellationToken) =>
            await stream.CopyToAsync(read, cancellationToken));
        Assert.Equal(content, read.ToArray());
    }

    [Fact]
    public async Task Multipart_CreateRecoverAndComplete_PreservesSixteenMetadataItems()
    {
        var address = new FileAddress("attachments", "metadata/multipart-recovery");
        var metadata = CreateMetadata(16);
        var content = new byte[PartSize + 3];
        RandomNumberGenerator.Fill(content);
        var storage = CreateStorage();
        var multipart = (IMultipartFileStorage)storage;
        var upload = await multipart.CreateMultipartAsync(
            new FileWriteRequest(address, content.Length, FileWriteMode.CreateOnly, "application/octet-stream", metadata),
            PartSize);

        var firstBytes = content.AsMemory(0, PartSize).ToArray();
        FilePart first;
        using (var firstSource = new MemoryStream(firstBytes, writable: false))
            first = await multipart.UploadPartAsync(upload, 1, firstSource,
                Convert.ToBase64String(SHA256.HashData(firstBytes)));

        // 恢复受信任句柄并重建后端对象，模拟进程重启后的会话恢复。
        var restoredRequest = FileWriteRequest.RestoreMultipart(address, content.Length, FileWriteMode.CreateOnly,
            "application/octet-stream", metadata);
        var restoredUpload = new MultipartUpload(upload.UploadId, restoredRequest, PartSize);
        multipart = (IMultipartFileStorage)CreateStorage();
        Assert.Equal(first.Number, Assert.Single(await multipart.ListPartsAsync(restoredUpload)).Number);

        var lastBytes = content.AsMemory(PartSize, 3).ToArray();
        FilePart last;
        using (var lastSource = new MemoryStream(lastBytes, writable: false))
            last = await multipart.UploadPartAsync(restoredUpload, 2, lastSource);
        var completed = await multipart.CompleteMultipartAsync(restoredUpload, new[] { first, last });

        Assert.Equal((long)content.Length, completed.Properties.Length);
        Assert.Equal("application/octet-stream", completed.Properties.ContentType);
        foreach (var pair in metadata)
            Assert.Equal(pair.Value, completed.Properties.Metadata[pair.Key]);

        using var read = new MemoryStream();
        await CreateStorage().ReadAsync(address, async (stream, cancellationToken) =>
            await stream.CopyToAsync(read, cancellationToken));
        Assert.Equal(content, read.ToArray());
    }

    private IFileStorage CreateStorage() => new FileSystemStorageBackend("local", _root).Bind(StorageScope.Host);

    private static Dictionary<string, string> CreateMetadata(int count) =>
        Enumerable.Range(0, count).ToDictionary(index => $"meta{index:D2}", index => $"value{index:D2}", StringComparer.Ordinal);

    private static void AssertMetadata(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var pair in expected)
            Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
