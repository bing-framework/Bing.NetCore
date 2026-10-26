using System.Security.Cryptography;
using Bing.FileStorage;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class MinioUploadTheoryAttribute : TheoryAttribute
{
    public MinioUploadTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT")))
            Skip = "需要隔离 BING_FILE_STORAGE_MINIO_ENDPOINT。";
    }
}
public sealed class MinioUploadTests
{
    [MinioUploadTheory]
    [InlineData(false, 0)]
    [InlineData(false, 37)]
    [InlineData(true, 0)]
    [InlineData(true, 37)]
    public async Task PersistedSession_EmptyOrDirectUpload_CompletesWithoutRetry(bool freeSql, int length)
    {
        await using var env = new FileTestEnvironment(freeSql, minio: true); await env.InitializeAsync();
        var files = env.Get<FileManagementService>();
        var session = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "minio.bin", Length = length });
        await env.Get<FileOperationProcessor>().RunBatchAsync();
        Assert.Equal(FileState.Uploading, (await files.GetSessionAsync(session.Id)).State);
        var store = env.Get<IFileManagementStore>();
        if (length > 0)
        {
            Assert.Contains("bing-upload-token", (await store.FindSessionAsync(session.Id))!.Session!.BackendMetadataJson);
            var bytes = Enumerable.Repeat((byte)7, length).ToArray();
            var signed = await files.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) });
            using var http = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Put, signed!.Url) { Content = new ByteArrayContent(bytes) };
            request.Headers.TryAddWithoutValidation("x-amz-checksum-sha256", signed.Headers["x-amz-checksum-sha256"]);
            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        await files.CompleteUploadAsync(session.Id);
        // 新的处理器从数据库重建上传句柄，一次执行就成功，不能靠第二次任务重试掩盖元数据丢失。
        await env.Get<FileOperationProcessor>().RunBatchAsync();
        Assert.Equal(FileState.Ready, (await files.GetSessionAsync(session.Id)).State);
        Assert.Equal(FileState.Ready, (await files.CompleteUploadAsync(session.Id)).State);
        var entry = (await store.FindAsync(session.FileId))!;
        var storage = env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope());
        Assert.Equal(length, (await storage.GetPropertiesAsync(entry.File.Address()))!.Length);
        await files.DeleteAsync(session.FileId); await env.RunAsync();
        Assert.Null(await storage.GetPropertiesAsync(entry.File.Address()));
    }
}
