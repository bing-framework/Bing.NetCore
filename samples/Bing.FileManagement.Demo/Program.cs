using Bing.Biz.FileManagement.FreeSql;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.FileStorage;
using Bing.FileStorage.FileSystem;
using Bing.FileStorage.AliyunDrive;
using Bing.FileStorage.Pan115;
using Bing.MultiTenancy;
using Bing.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
// 示例身份只用于回环地址下的开发验证，禁止把这个宿主当作生产认证服务。
builder.WebHost.UseUrls("http://127.0.0.1:" + (builder.Configuration["FileDemo:Port"] ?? "5188"));
var data = Path.GetFullPath(builder.Configuration["FileDemo:DataRoot"] ?? Path.Combine(AppContext.BaseDirectory, "demo-data"));
Directory.CreateDirectory(data);
var backend = builder.Configuration["FileDemo:Provider"] ?? "local";
if (backend is not ("local" or "minio" or "qiniu" or "oss")) throw new InvalidOperationException("FileDemo:Provider 仅支持 local、minio、qiniu 或 oss；网盘通过 RemoteDrives 连接。");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DemoIdentity>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<DemoIdentity>());
builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<DemoIdentity>());
builder.Services.AddScoped<IFileAccessAuthorizer, DemoBusinessAuthorizer>();
builder.Services.AddAuthentication("demo").AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>("demo", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddControllers();
var defaultStore = backend switch { "minio" => "minio-v1", "qiniu" => "qiniu-v1", "oss" => "oss-v1", _ => "local-v1" };
builder.Services.AddBingFileStorage(o => o.DefaultStore = defaultStore);
builder.Services.AddBingFileSystemStorage("local-v1", o => o.RootPath = Path.Combine(data, "objects"));
if (backend == "minio")
{
    var endpoint = builder.Configuration["FileDemo:Minio:Endpoint"] ?? "http://127.0.0.1:19000";
    if (!new Uri(endpoint).IsLoopback) throw new InvalidOperationException("示例只连接本机隔离 MinIO。");
    var access = builder.Configuration["FileDemo:Minio:AccessKey"] ?? throw new InvalidOperationException("设置 FileDemo__Minio__AccessKey。");
    var secret = builder.Configuration["FileDemo:Minio:SecretKey"] ?? throw new InvalidOperationException("设置 FileDemo__Minio__SecretKey。");
    builder.Services.AddBingMinioStorage("minio-v1", o => { o.Endpoint = endpoint; o.AccessKey = access; o.SecretKey = secret; o.StorageNamespace = "file-demo"; });
    using var client = new AmazonS3Client(new BasicAWSCredentials(access, secret), new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
    foreach (var bucket in new[] { "attachments", "public" })
    {
        try { await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }); }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists") { }
        // 固定 MinIO 社区版使用服务级 MINIO_API_CORS_ALLOW_ORIGIN；不调用不支持的 Bucket CORS API。
    }
}
else if (backend == "qiniu")
{
    builder.Services.AddBingQiniuStorage("qiniu-v1", o => builder.Configuration.GetSection("FileDemo:Qiniu").Bind(o));
}
else if (backend == "oss") builder.Services.AddBingAliyunOssStorage("oss-v1", o => builder.Configuration.GetSection("FileDemo:Oss").Bind(o));
foreach (var template in builder.Configuration.GetSection("FileDemo:RemoteDrives").GetChildren())
    if (template["Provider"] == "AliyunDrive") builder.Services.AddBingAliyunDriveStorage(template.Key, o => template.Bind(o));
    else if (template["Provider"] == "Pan115") builder.Services.AddBingPan115Storage(template.Key, o => template.Bind(o));
    else throw new InvalidOperationException("未知网盘 Provider。");
builder.Services.AddBingFileManagement(o => { o.CleanupInterval = TimeSpan.FromSeconds(1); o.Containers["public"] = new() { Visibility = FileVisibility.Public }; });
builder.Services.AddSingleton<IFileLifecycleEventHandler>(new DemoFileEventHandler(Path.Combine(data, "events")));
var freeSql = builder.Configuration["FileDemo:Orm"] == "FreeSql";
if (freeSql)
{
    builder.Services.AddSingleton<IFreeSql>(_ => new FreeSql.FreeSqlBuilder().UseConnectionString(FreeSql.DataType.Sqlite, "Data Source=" + Path.Combine(data, "files.db")).UseAutoSyncStructure(false).Build());
    builder.Services.AddBingFileManagementFreeSql(sp => sp.GetRequiredService<IFreeSql>());
}
else builder.Services.AddBingFileManagementEntityFrameworkCore(o => o.UseSqlite("Data Source=" + Path.Combine(data, "files.db")));
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(data, "keys")));
builder.Services.AddBingFileManagementWeb(configureConsole: console => console.Enabled = true);
var app = builder.Build();
if (freeSql) FileManagementFreeSqlSchema.SyncStructure(app.Services.GetRequiredService<IFreeSql>());
else await using (var db = await app.Services.GetRequiredService<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync()) await db.Database.EnsureCreatedAsync();
app.UseDefaultFiles(); app.UseStaticFiles();
app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/demo/config", () => new { provider = backend, orm = freeSql ? "FreeSql / SQLite" : "EF Core / SQLite" });
app.MapControllers();
app.MapHealthChecks("/health/ready");
await app.RunAsync();

public sealed class FilesController : FileManagementControllerBase
{
    public FilesController(FileManagementService files, IFileManagementStore store, IFileStorageFactory storage, FileManagementOptions options, IDataProtectionProvider protection)
        : base(files, store, storage, options, protection) { }
}
public sealed class DemoAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DemoAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock) { }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var manager = Request.Headers.Authorization == "Bearer demo-manage-token";
        if (!manager && Request.Headers.Authorization != "Bearer demo-upload-token") return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "demo-user"), new("tenant", "demo-tenant"),
            new("permission", "files.upload"), new("permission", "files.publish") };
        if (manager) { claims.Add(new Claim("permission", "files.manage")); claims.Add(new Claim("permission", "files.connections.manage")); }
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
public sealed class DemoIdentity : ICurrentUser, ICurrentTenant
{
    private readonly IHttpContextAccessor _context;
    public DemoIdentity(IHttpContextAccessor context) { _context = context; }
    private ClaimsPrincipal Principal => _context.HttpContext?.User ?? new ClaimsPrincipal();
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public string UserId => Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public string UserName => UserId; public string PhoneNumber => ""; public bool PhoneNumberVerified => false;
    public string Email => ""; public bool EmailVerified => false; public string TenantId => Id!;
    public string[] Roles => Array.Empty<string>(); public Claim FindClaim(string type) => Principal.FindFirst(type)!;
    public Claim[] FindClaims(string type) => Principal.FindAll(type).ToArray(); public Claim[] GetAllClaims() => Principal.Claims.ToArray();
    public bool IsInRole(string role) => Principal.IsInRole(role);
    public bool IsAvailable => Id != null; public string? Id => Principal.FindFirstValue("tenant"); public string? Name => Id;
    public IDisposable Change(string id, string? name = null) => throw new NotSupportedException();
}
public sealed class DemoBusinessAuthorizer : IFileAccessAuthorizer
{
    private readonly ICurrentUser _user;
    public DemoBusinessAuthorizer(ICurrentUser user) { _user = user; }
    public Task<bool> AuthorizeAsync(string businessType, string businessId, FileBusinessAccess access, CancellationToken ct = default)
        => Task.FromResult(_user.IsAuthenticated && businessType == "demo" && businessId == "1");
}

/// <summary>可观察的幂等事件消费示例：每个事件原子发布一个 UTF-8 JSON 文件。</summary>
public sealed class DemoFileEventHandler : IFileLifecycleEventHandler
{
    private readonly string _directory;
    public DemoFileEventHandler(string directory) { _directory = directory; Directory.CreateDirectory(directory); }
    public string Name => "demo-json-receipts";
    public async Task HandleAsync(FileLifecycleEvent message, CancellationToken ct)
    {
        var destination = Path.Combine(_directory, message.Id.ToString("N") + ".json");
        if (File.Exists(destination)) return;
        var temporary = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, System.Text.Json.JsonSerializer.Serialize(message), new System.Text.UTF8Encoding(false), ct);
            ct.ThrowIfCancellationRequested();
            try { File.Move(temporary, destination); }
            catch (IOException) when (File.Exists(destination)) { }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
