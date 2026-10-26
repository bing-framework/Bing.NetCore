using System.Text.Json;
using Xunit;

namespace Bing.FileStorage.Qiniu.Tests;

public sealed class MetadataBudgetTests
{
    private static Dictionary<string, string> Metadata(int count) => Enumerable.Range(0, count).ToDictionary(i => "key-" + i, i => "value-" + i);

    [Theory]
    [InlineData(14, S3UploadMode.ServerProxy)] [InlineData(15, S3UploadMode.ServerProxy)] [InlineData(16, S3UploadMode.ServerProxy)]
    [InlineData(14, S3UploadMode.SignedParts)] [InlineData(15, S3UploadMode.SignedParts)] [InlineData(16, S3UploadMode.SignedParts)]
    public async Task CustomBudget_SurvivesSaveAndPersistedMultipartRestore(int count, S3UploadMode mode)
    {
        using var client = new RecordingS3Client { OmitRemoteChecksum = false };
        var storage = QiniuProviderTests.Storage(client, mode); var metadata = Metadata(count);
        foreach (var length in new[] { 0, 3 })
        {
            var address = new FileAddress("attachments", "save-" + length);
            await storage.SaveAsync(new(address, length, FileWriteMode.Overwrite, metadata: metadata), new MemoryStream(new byte[length]));
            var properties = (await storage.GetPropertiesAsync(address))!;
            Assert.Equal(count + 2, properties.Metadata.Count);
            foreach (var pair in metadata) Assert.Equal(pair.Value, properties.Metadata[pair.Key]);
        }
        var multipart = (IMultipartFileStorage)storage;
        var upload = await multipart.CreateMultipartAsync(new(new("attachments", "restore"), 3, FileWriteMode.Overwrite, metadata: metadata), 5 * 1024 * 1024);
        var persisted = JsonSerializer.Serialize(upload.Request.Metadata);
        var restored = new MultipartUpload(upload.UploadId, FileWriteRequest.RestoreMultipart(upload.Request.Address,
            upload.Request.Length, upload.Request.Mode, upload.Request.ContentType,
            JsonSerializer.Deserialize<Dictionary<string, string>>(persisted)!), upload.PartSize);
        var part = await multipart.UploadPartAsync(restored, 1, new MemoryStream(new byte[] { 1, 2, 3 }));
        Assert.Equal(3, (await multipart.CompleteMultipartAsync(restored, new[] { part })).Properties.Length);
        Assert.Equal(mode.ToString(), restored.Request.Metadata["bing-upload-mode"]);
        Assert.Equal(upload.Request.Metadata["bing-upload-token"], restored.Request.Metadata["bing-upload-token"]);
    }

    [Theory]
    [InlineData("bing-upload-token")] [InlineData("bing-upload-mode")] [InlineData("bing-upload-unknown")]
    public async Task NewRequests_RejectReservedMetadata_EvenUsingRestoreFactory(string key)
    {
        using var client = new RecordingS3Client(); var storage = QiniuProviderTests.Storage(client);
        foreach (var length in new[] { 0, 3 })
        {
            var request = FileWriteRequest.RestoreMultipart(new("attachments", "invalid"), length, FileWriteMode.Overwrite,
                null, new Dictionary<string, string> { [key] = "fake" });
            await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(request, new MemoryStream(new byte[length])));
        }
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task ExcessCustomCountOrBackendBytes_FailsBeforeSdkCall()
    {
        Assert.Throws<ArgumentException>(() => new FileWriteRequest(new("attachments", "invalid"), 0, FileWriteMode.Overwrite, metadata: Metadata(17)));
        var restored = Metadata(17); restored["bing-upload-mode"] = "ServerProxy";
        Assert.Throws<ArgumentException>(() => FileWriteRequest.RestoreMultipart(new("attachments", "invalid"), 0, FileWriteMode.Overwrite, null, restored));
        using var client = new RecordingS3Client(); var storage = QiniuProviderTests.Storage(client);
        var metadata = Metadata(4).ToDictionary(p => p.Key, _ => new string('x', 512));
        foreach (var length in new[] { 0, 3 })
            await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(new(new("attachments", "invalid"), length,
                FileWriteMode.Overwrite, metadata: metadata), new MemoryStream(new byte[length])));
        Assert.Empty(client.Calls);
    }
}
