using System;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.Biz.FileManagement;

namespace Bing.Admin.Service.FileStorage;

/// <summary>文件生命周期审计使用的独立连接；每次返回未打开的新连接。</summary>
public sealed class AdminFileAuditDatabase
{
    /// <summary>审计数据库语法。</summary>
    public AuditLogDatabase Database { get; }
    /// <summary>新建独立连接的工厂。</summary>
    public Func<DbConnection> CreateConnection { get; }
    /// <summary>指定审计连接与方言。</summary>
    public AdminFileAuditDatabase(AuditLogDatabase database, Func<DbConnection> createConnection)
    { Database = database; CreateConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection)); }
}

/// <summary>将持久生命周期事件写入现有审计表，按事件 ID 幂等；保存失败交回 Outbox 重试。</summary>
public sealed class AdminFileLifecycleAuditHandler : IFileLifecycleEventHandler
{
    private readonly AdminFileAuditDatabase _database;
    private readonly IFileManagementStore _files;
    /// <inheritdoc />
    public string Name => "admin-file-audit-v1";
    /// <summary>使用独立审计连接及持久文件元数据创建处理器。</summary>
    public AdminFileLifecycleAuditHandler(AdminFileAuditDatabase database, IFileManagementStore files)
    { _database = database; _files = files; }

    /// <inheritdoc />
    public async Task HandleAsync(FileLifecycleEvent message, CancellationToken ct)
    {
        var entry = await _files.FindAsync(message.FileId, ct);
        if (entry == null || entry.File.ScopeKey != message.ScopeKey || message.Id == Guid.Empty || message.FileVersion < 1 ||
            message.Kind is not ("FileReady" or "FileDeleted"))
            throw new InvalidOperationException("文件事件与持久文件范围不匹配。");
        var tenant = entry.File.TenantId ?? "";
        var id = "file-event-" + message.Id.ToString("N");
        var correlation = message.FileId.ToString("N");
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(ct);
        if (await ExistsAsync(connection, id, tenant, correlation, message.Kind, ct)) return;
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            var audit = new AuditLogInfo
            {
                ApplicationName = "Bing.Admin.Files", TenantId = tenant,
                CorrelationId = correlation, HttpMethod = message.Kind,
                ExecutionTime = DateTimeOffset.FromUnixTimeMilliseconds(message.CreatedAt).UtcDateTime
            };
            audit.Actions.Add(new AuditLogActionInfo
            {
                ServiceName = "File/" + correlation, MethodName = message.Kind + "/v" + message.FileVersion,
                ExecutionTime = audit.ExecutionTime
            });
            // 不使用后台进程的当前身份冒充原业务操作者；事件版本来自持久消息。
            await new DbAuditingStore(_database.Database).SaveAsync(transaction, id, audit, ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbException)
        {
            // 并发重复或提交响应丢失：只在新连接确认目标审计已存在时视为成功。
            await using var verify = _database.CreateConnection();
            await verify.OpenAsync(ct);
            if (!await ExistsAsync(verify, id, tenant, correlation, message.Kind, ct)) throw;
        }
    }

    private async Task<bool> ExistsAsync(DbConnection connection, string id, string tenant, string correlation, string kind, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ApplicationName, CorrelationId, HttpMethod FROM bing_audit_log WHERE AuditId = @id AND TenantId = @tenant";
        Add(command, "@id", id);
        Add(command, "@tenant", _database.Database == AuditLogDatabase.MySql ? Encoding.UTF8.GetBytes(tenant) : tenant);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return false;
        if (reader.GetString(0) != "Bing.Admin.Files" || reader.GetString(1) != correlation || reader.GetString(2) != kind)
            throw new InvalidOperationException("事件审计标识冲突。");
        return true;
    }

    private static void Add(DbCommand command, string name, object value)
    { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
}
