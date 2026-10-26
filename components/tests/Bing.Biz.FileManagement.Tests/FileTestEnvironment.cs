using Bing.Biz.FileManagement.FreeSql;
using System.Security.Claims;
using System.Security.Cryptography;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.FileStorage;
using Bing.FileStorage.FileSystem;
using Bing.MultiTenancy;
using Bing.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class FileTestEnvironment : IAsyncDisposable
{
    private readonly bool _freeSql, _minio, _minioDefault;
    public FileTestEnvironment(bool freeSql = false, bool minio = false, bool minioDefault = true) { _freeSql = freeSql; _minio = minio; _minioDefault = minioDefault; }
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "bing-file-management-tests", Guid.NewGuid().ToString("N"));
    public ServiceProvider Provider { get; private set; } = null!;
    public TestIdentity Identity { get; } = new();
    public TestBusinessAuthorizer Authorizer { get; } = new();
    public Action<FileManagementOptions>? ConfigureManagement { get; set; }
    public Action<IServiceCollection>? CustomizeServices { get; set; }
    public IServiceCollection Configure(IServiceCollection services)
    {
        Directory.CreateDirectory(Root);
        services.AddLogging();
        services.AddSingleton<ICurrentUser>(Identity); services.AddSingleton<ICurrentTenant>(Identity);
        services.AddSingleton<IFileAccessAuthorizer>(Authorizer);
        services.AddBingFileStorage(o => o.DefaultStore = _minio && _minioDefault ? "minio-v1" : "local-v1");
        if (_minio) services.AddBingMinioStorage("minio-v1", o =>
        {
            o.Endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT")!;
            o.AccessKey = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ACCESS_KEY")!;
            o.SecretKey = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_SECRET_KEY")!;
            o.StorageNamespace = "management-tests";
        });
        services.AddBingFileSystemStorage("local-v1", o => o.RootPath = Path.Combine(Root, "local"));
        services.AddBingFileSystemStorage("local-v2", o => o.RootPath = Path.Combine(Root, "other"));
        services.AddBingFileManagement(o => { o.Containers["public"] = new() { Visibility = FileVisibility.Public }; o.PartSize = 5 * 1024 * 1024; foreach(var policy in o.Containers.Values) policy.MaxFileLength = 1024L * 1024 * 1024; o.CleanupInterval = TimeSpan.FromSeconds(1); o.ReplicaRetention = TimeSpan.Zero; ConfigureManagement?.Invoke(o); });
        if (_freeSql)
        {
            services.AddSingleton<IFreeSql>(_ => new global::FreeSql.FreeSqlBuilder().UseConnectionString(global::FreeSql.DataType.Sqlite, "Data Source=" + Path.Combine(Root, "files.db")).UseAutoSyncStructure(false).Build());
            services.AddBingFileManagementFreeSql(sp => sp.GetRequiredService<IFreeSql>());
        }
        else services.AddBingFileManagementEntityFrameworkCore(o => o.UseSqlite("Data Source=" + Path.Combine(Root, "files.db")));
        CustomizeServices?.Invoke(services);
        return services;
    }
    public async Task InitializeAsync()
    {
        if (_minio)
        {
            // 专用环境的前置条件由本测试显式创建，不依赖另一个测试项目的执行顺序。
            var endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT")!;
            using var client = new Amazon.S3.AmazonS3Client(
                new Amazon.Runtime.BasicAWSCredentials(Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ACCESS_KEY"), Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_SECRET_KEY")),
                new Amazon.S3.AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1", UseHttp = new Uri(endpoint).Scheme == "http" });
            try { await client.GetBucketLocationAsync(new Amazon.S3.Model.GetBucketLocationRequest { BucketName = "attachments" }); }
            catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
            {
                try { await client.PutBucketAsync(new Amazon.S3.Model.PutBucketRequest { BucketName = "attachments" }); }
                catch (Amazon.S3.AmazonS3Exception race) when (race.ErrorCode == "BucketAlreadyOwnedByYou") { }
            }
        }
        Provider = Configure(new ServiceCollection()).BuildServiceProvider();
        await InitializeDatabaseAsync(Provider);
    }
    public async Task RestartAsync()
    {
        await Provider.DisposeAsync();
        Provider = Configure(new ServiceCollection()).BuildServiceProvider();
    }
    public static async Task InitializeDatabaseAsync(IServiceProvider provider)
    {
        if (provider.GetService<IFreeSql>() is { } orm) { FileManagementFreeSqlSchema.SyncStructure(orm); return; }
        await using var db = await provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }
    public T Get<T>() where T : notnull => Provider.GetRequiredService<T>();
    public async Task RunAsync(int rounds = 3)
    { for (var i = 0; i < rounds; i++) await Get<FileOperationProcessor>().RunBatchAsync(); }
    public async Task<UploadSessionView> UploadAsync(byte[] bytes, string container = "attachments")
    {
        var service = Get<FileManagementService>();
        var created = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "测试文件.txt", Length = bytes.Length, ContentType = "text/plain", Container = container, LastModified = 100 });
        await RunAsync();
        var session = await service.GetSessionAsync(created.Id);
        for (int number = 1, offset = 0; offset < bytes.Length; number++)
        {
            var part = bytes.Skip(offset).Take(session.PartSize).ToArray();
            await service.CreatePartRequestAsync(session.Id, new() { Number = number, Sha256 = Convert.ToBase64String(SHA256.HashData(part)) });
            await service.UploadPartAsync(session.Id, number, new MemoryStream(part));
            offset += part.Length;
        }
        await service.CompleteUploadAsync(session.Id); await RunAsync();
        session = await service.GetSessionAsync(session.Id);
        Assert.Equal(FileState.Ready, session.State);
        return session;
    }
    public async ValueTask DisposeAsync()
    {
        if (Provider != null) await Provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        System.Data.SQLite.SQLiteConnection.ClearAllPools();
        // Windows 上池关闭与文件系统过滤驱动可能短暂持有句柄；仅对共享/锁冲突有限重试。
        for (var attempt = 0; Directory.Exists(Root); attempt++)
        {
            try { Directory.Delete(Root, true); break; }
            catch (IOException ex) when (attempt < 5 && (ex.HResult & 0xffff) is 32 or 33)
            { await Task.Delay(100 * (attempt + 1)); }
        }
    }
}
public sealed class TestIdentity : ICurrentUser, ICurrentTenant
{
    public string UserId { get; set; } = "user-a";
    public string? Tenant { get; set; } = "tenant-a";
    public bool IsAuthenticated { get; set; } = true;
    public HashSet<string> Permissions { get; } = new() { "files.upload", "files.publish" };
    public string UserName => UserId; public string PhoneNumber => ""; public bool PhoneNumberVerified => false;
    public string Email => ""; public bool EmailVerified => false; public string TenantId => Tenant!; public string[] Roles => Array.Empty<string>();
    public Claim FindClaim(string type) => FindClaims(type).FirstOrDefault()!;
    public Claim[] FindClaims(string type) => GetAllClaims().Where(c => c.Type == type).ToArray();
    public Claim[] GetAllClaims() => Permissions.Select(p => new Claim("permission", p)).ToArray();
    public bool IsInRole(string role) => false;
    public bool IsAvailable => Tenant != null; public string? Id => Tenant; public string? Name => Tenant;
    public IDisposable Change(string id, string? name = null) => throw new NotSupportedException();
}
public sealed class TestBusinessAuthorizer : IFileAccessAuthorizer
{
    public bool Granted { get; set; }
    public Task<bool> AuthorizeAsync(string businessType, string businessId, FileBusinessAccess access, CancellationToken ct = default)
        => Task.FromResult(Granted && businessType == "orders");
}
