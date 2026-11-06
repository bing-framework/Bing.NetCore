using System.Text;
using System.Text.Json;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.Biz.FileManagement.FreeSql;
using Bing.FileStorage;
using Bing.FileStorage.FileSystem;
using Bing.FileStorage.AliyunDrive;
using Bing.FileStorage.Pan115;
using Bing.Biz.FileManagement.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
if (args.Length == 0) throw new ArgumentException("命令：schema | import | migrate | verify | run-tasks | local-cleanup | operations | operation | retry-operation | batch-preview | batch-create | batch-status | batch-items | batch-pause | batch-resume | batch-retry；--config <appsettings.json>，执行数据变更需 --apply。");
var command = args[0];
var options = new Dictionary<string, string>(StringComparer.Ordinal);
for (var i = 1; i < args.Length; i++) { if (args[i] == "--apply") options[args[i]] = "true"; else options.Add(args[i], args[++i]); }
var apply = options.ContainsKey("--apply");
var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(options["--config"])).AddEnvironmentVariables().Build();
var section = config.GetSection("FileManagement");
var connection = config.GetConnectionString("FileManagement") ?? throw new ArgumentException("需要专用 ConnectionStrings:FileManagement。");
var isSqlite = string.Equals(section["Database"], "Sqlite", StringComparison.OrdinalIgnoreCase);
var isFreeSql = string.Equals(section["Orm"], "FreeSql", StringComparison.OrdinalIgnoreCase);
var services = new ServiceCollection(); services.AddLogging();
services.AddBingFileStorage(o =>
{
    o.DefaultStore = section["DefaultStore"] ?? "local-v1";
    foreach (var container in section.GetSection("Containers").GetChildren()) o.Containers.Add(container.Key);
});
if (command is not ("schema" or "local-cleanup"))
foreach (var store in section.GetSection("Stores").GetChildren())
{
    if (store["Provider"] == "FileSystem") services.AddBingFileSystemStorage(store.Key, o => store.Bind(o));
    else if (store["Provider"] == "Minio") services.AddBingMinioStorage(store.Key, o => store.Bind(o));
    else if (store["Provider"] == "Qiniu") services.AddBingQiniuStorage(store.Key, o => store.Bind(o));
    else if (store["Provider"] == "AliyunOss") services.AddBingAliyunOssStorage(store.Key, o => store.Bind(o));
    else throw new ArgumentException("未知存储 Provider。");
}
if (command is not ("schema" or "local-cleanup") && section.GetSection("RemoteDrives").GetChildren().Any())
{
    services.AddBingRemoteDriveCredentialProtection(section["KeyRingPath"] ?? throw new ArgumentException("需要 KeyRingPath。"), section["KeyRingApplicationName"] ?? "Bing.FileManagement");
    foreach (var template in section.GetSection("RemoteDrives").GetChildren())
        if (template["Provider"] == "AliyunDrive") services.AddBingAliyunDriveStorage(template.Key, o => template.Bind(o));
        else if (template["Provider"] == "Pan115") services.AddBingPan115Storage(template.Key, o => template.Bind(o));
        else throw new ArgumentException("未知网盘 Provider。");
}
services.AddBingFileManagement(o => section.Bind(o));
if (isFreeSql)
{
    services.AddSingleton<IFreeSql>(_ => new FreeSql.FreeSqlBuilder().UseConnectionString(isSqlite ? FreeSql.DataType.Sqlite : FreeSql.DataType.MySql, connection)
        .UseAutoSyncStructure(false).UseLazyLoading(false).Build());
    services.AddBingFileManagementFreeSql(sp => sp.GetRequiredService<IFreeSql>());
}
else services.AddBingFileManagementEntityFrameworkCore(o =>
{
    if (isSqlite) o.UseSqlite(connection);
    else o.UseMySql(connection, new MySqlServerVersion(Version.Parse(section["MySqlVersion"] ?? "8.0.36")));
});
await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var storeService = scope.ServiceProvider.GetRequiredService<IFileManagementStore>();
FileMigrationService Migration() => scope.ServiceProvider.GetRequiredService<FileMigrationService>();
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
using var deadline = new CancellationTokenSource(TimeSpan.FromHours(2));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; deadline.Cancel(); };
switch (command)
{
    case "local-cleanup":
        if (apply && options.GetValueOrDefault("--offline") != "true") throw new ArgumentException("先停止该目录的写入进程，再传 --offline true --apply。");
        var local = section.GetSection("Stores").GetSection(options["--store"]);
        if (local["Provider"] != "FileSystem") throw new ArgumentException("只支持 FileSystem 存储。");
        await WriteOutputAsync(JsonSerializer.Serialize(FileSystemStorageMaintenance.CleanupTemporaryFiles(
            local["RootPath"]!, local["Namespace"] ?? "bing", DateTimeOffset.UtcNow.AddDays(-1), apply), json));
        break;
    case "schema":
        // 只导出建表语句；永远不自动修改现有业务数据库。
        if (isFreeSql) await WriteOutputAsync(FileManagementFreeSqlSchema.GenerateScript(provider.GetRequiredService<IFreeSql>()));
        else await using (var db = await provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync(deadline.Token))
            await WriteOutputAsync(db.Database.GenerateCreateScript());
        break;
    case "import":
        var rows = JsonSerializer.Deserialize<List<ImportRow>>(await File.ReadAllTextAsync(options["--manifest"], Encoding.UTF8, deadline.Token), json)
            ?? throw new ArgumentException("旧数据清单为空。");
        var report = new List<LegacyFileImport>();
        foreach (var row in rows)
        {
            var prefix = options.GetValueOrDefault("--address-prefix", "");
            var mapped = row.Address != null && row.Address.StartsWith(prefix, StringComparison.Ordinal) ? row.Address[prefix.Length..].TrimStart('/', '\\') : "";
            var request = new LegacyImportRequest { Source = options["--source"], LegacyId = row.Id,
                Scope = row.TenantId != null ? StorageScope.ForTenant(row.TenantId) : row.HostScope ? StorageScope.Host : null,
                OwnerId = row.OwnerId ?? "", RootPath = Path.GetFullPath(options["--legacy-root"]), RelativePath = mapped,
                FileName = row.Name, ContentType = row.ContentType ?? "application/octet-stream", ExpectedLength = row.Size,
                StoreName = options.GetValueOrDefault("--store", provider.GetRequiredService<IFileStorageFactory>().DefaultStore),
                Container = options.GetValueOrDefault("--container", "attachments") };
            report.Add(await Migration().ImportAsync(request, apply, deadline.Token));
        }
        await WriteOutputAsync(JsonSerializer.Serialize(report, json));
        break;
    case "verify":
        var verifyEntry = await storeService.FindAsync(Guid.Parse(options["--file-id"]), deadline.Token) ?? throw new ArgumentException("文件不存在。");
        if (verifyEntry.File.Ownership == Bing.FileStorage.RemoteDrive.RemoteDriveOwnership.Personal)
            throw new ArgumentException("管理工具不能读取个人文件；请由所有者通过应用验证或复制发布。");
        var verifyBackend = provider.GetRequiredService<IFileStorageFactory>().GetStorage(verifyEntry.File.StoreName, verifyEntry.File.Scope());
        var properties = await verifyBackend.GetPropertiesAsync(verifyEntry.File.Address(), deadline.Token);
        var digest = await FileMigrationService.HashAsync(verifyBackend, verifyEntry.File.Address(), deadline.Token);
        var verified = properties?.Length == verifyEntry.File.Length && (verifyEntry.File.Sha256 == null || verifyEntry.File.Sha256 == digest);
        await WriteOutputAsync(JsonSerializer.Serialize(new { fileId = verifyEntry.File.Id, verifyEntry.File.State, verifyEntry.File.StoreName, verifyEntry.File.Length, sha256 = digest, verified }, json));
        if (!verified) Environment.ExitCode = 1;
        break;
    case "migrate":
        var id = Guid.Parse(options["--file-id"]);
        var target = options["--target-store"];
        var entry = await storeService.FindAsync(id, deadline.Token) ?? throw new ArgumentException("文件不存在。");
        if (!apply)
            await WriteOutputAsync(JsonSerializer.Serialize(new { mode = "Preview", fileId = id, entry.File.State, entry.File.Length, from = entry.File.StoreName, to = target, entry.File.Version }, json));
        else
            await WriteOutputAsync(JsonSerializer.Serialize(new { operationId = await Migration().QueueMigrationAsync(id, target, deadline.Token), mode = "Queued" }, json));
        break;
    case "run-tasks":
        if (!apply) throw new ArgumentException("执行持久任务需要 --apply。");
        var count = await scope.ServiceProvider.GetRequiredService<FileOperationProcessor>().RunBatchAsync(deadline.Token);
        await WriteOutputAsync(JsonSerializer.Serialize(new { processed = count }, json));
        break;
    case "operations":
        await WriteOutputAsync(JsonSerializer.Serialize(await scope.ServiceProvider.GetRequiredService<FileOperationAdministration>().ListAsync(new()
        {
            ScopeKey = ManagementScope().Key, Skip = IntOption("--skip", 0), Take = IntOption("--take", 50),
            State = options.TryGetValue("--state", out var operationState) ? Enum.Parse<FileOperationState>(operationState, true) : null,
            Kind = options.TryGetValue("--kind", out var operationKind) ? Enum.Parse<FileOperationKind>(operationKind, true) : null,
            FileId = options.TryGetValue("--file-id", out var operationFile) ? Guid.Parse(operationFile) : null
        }, deadline.Token), json));
        break;
    case "operation":
        var operationId = Guid.Parse(options["--operation-id"]);
        var operationScope = ManagementScope().Key;
        await WriteOutputAsync(JsonSerializer.Serialize(new
        {
            operation = await scope.ServiceProvider.GetRequiredService<FileOperationAdministration>().GetAsync(operationScope, operationId, deadline.Token),
            audit = await storeService.ListOperationAuditAsync(operationScope, operationId, deadline.Token)
        }, json));
        break;
    case "retry-operation":
        RequireApply();
        await WriteOutputAsync(JsonSerializer.Serialize(new { operationId = await scope.ServiceProvider.GetRequiredService<FileOperationAdministration>().RecoverAsync(
            ManagementScope().Key, options["--actor"], Guid.Parse(options["--operation-id"]), new()
            { RequestId = Guid.Parse(options["--request-id"]), ExpectedVersion = long.Parse(options["--version"]), Reason = options["--reason"] }, deadline.Token) }, json));
        break;
    case "batch-preview":
        var preview = await scope.ServiceProvider.GetRequiredService<FileMigrationBatchService>().PreviewAsync(ManagementScope(), BatchSelection(false),
            options.TryGetValue("--after-key", out var afterKey) ? new() { Key = afterKey, CreatedAt = long.Parse(options["--after-created"]) } : null, deadline.Token);
        await WriteOutputAsync(JsonSerializer.Serialize(new { items = preview.Select(f => new { f.Id, f.FileName, f.Length, f.Version }),
            nextPosition = preview.Count == 100 ? new FileScanPosition { CreatedAt = preview[^1].CreatedAt, Key = preview[^1].RequestKey } : null }, json));
        break;
    case "batch-create":
        RequireApply();
        await WriteOutputAsync(JsonSerializer.Serialize(await scope.ServiceProvider.GetRequiredService<FileMigrationBatchService>().CreateAsync(
            ManagementScope(), options["--actor"], BatchSelection(true), deadline.Token), json));
        break;
    case "batch-status":
    case "batch-items":
        var requestedBatch = Guid.Parse(options["--batch-id"]);
        var batchProgress = await scope.ServiceProvider.GetRequiredService<FileMigrationBatchService>().GetAsync(ManagementScope().Key, requestedBatch, deadline.Token);
        if (command == "batch-status") await WriteOutputAsync(JsonSerializer.Serialize(batchProgress, json));
        else await WriteOutputAsync(JsonSerializer.Serialize(await storeService.ListBatchItemsAsync(requestedBatch,
            options.TryGetValue("--state", out var itemState) ? Enum.Parse<FileMigrationItemState>(itemState, true) : null,
            Math.Max(0, IntOption("--skip", 0)), Math.Clamp(IntOption("--take", 50), 1, 100), deadline.Token), json));
        break;
    case "batch-pause":
    case "batch-resume":
        RequireApply();
        await scope.ServiceProvider.GetRequiredService<FileMigrationBatchService>().SetPausedAsync(ManagementScope().Key, Guid.Parse(options["--batch-id"]),
            long.Parse(options["--version"]), command == "batch-pause", deadline.Token);
        await WriteOutputAsync(JsonSerializer.Serialize(new { changed = true }, json));
        break;
    case "batch-retry":
        RequireApply();
        var queued = await scope.ServiceProvider.GetRequiredService<FileMigrationBatchService>().RetryFailedAsync(ManagementScope().Key, options["--actor"],
            Guid.Parse(options["--batch-id"]), long.Parse(options["--version"]), options["--reason"],
            scope.ServiceProvider.GetRequiredService<FileOperationAdministration>(), deadline.Token);
        await WriteOutputAsync(JsonSerializer.Serialize(new { queued }, json));
        break;
    default: throw new ArgumentException("未知命令。");
}
void RequireApply() { if (!apply) throw new ArgumentException("执行管理变更需要 --apply。"); }
int IntOption(string name, int fallback) => options.TryGetValue(name, out var value) ? int.Parse(value) : fallback;
StorageScope ManagementScope()
{
    var hasTenant = options.TryGetValue("--tenant-id", out var tenant);
    var host = options.GetValueOrDefault("--host-scope") == "true";
    if (hasTenant == host) throw new ArgumentException("必须且只能指定 --tenant-id 或 --host-scope true。");
    return host ? StorageScope.Host : StorageScope.ForTenant(tenant!);
}
CreateMigrationBatch BatchSelection(bool requireId) => new()
{
    RequestId = requireId ? Guid.Parse(options["--request-id"]) : Guid.Empty,
    SourceStore = options["--source-store"], TargetStore = options["--target-store"], Container = options.GetValueOrDefault("--container")
};
async Task WriteOutputAsync(string content)
{
    if (options.TryGetValue("--output", out var destination)) await File.WriteAllTextAsync(destination, content, new UTF8Encoding(false));
    else Console.WriteLine(content);
}
public sealed class ImportRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public long Size { get; set; }
    public string? TenantId { get; set; }
    public bool HostScope { get; set; }
    public string? OwnerId { get; set; }
    public string? ContentType { get; set; }
}
