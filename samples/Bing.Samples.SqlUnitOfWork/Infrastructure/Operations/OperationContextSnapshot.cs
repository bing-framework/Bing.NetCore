using Bing.Data;
using Bing.Data.Sql;
using Bing.Tracing;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;

/// <summary>
/// 可在线程队列或 Hangfire 中序列化传递的跟踪与数据库路由标量。
/// </summary>
/// <param name="TraceId">调用链跟踪标识。</param>
/// <param name="RootId">调用链根标识。</param>
/// <param name="ParentId">当前节点的父标识。</param>
/// <param name="ChildId">当前节点的子标识。</param>
/// <param name="DataSourceKey">已确认的数据源逻辑键。</param>
/// <param name="TenantId">租户标识。</param>
/// <param name="MappingProfile">映射配置名称。</param>
/// <param name="ReadPreference">读取偏好。</param>
public sealed record OperationContextSnapshot(
    string TraceId,
    string? RootId,
    string? ParentId,
    string? ChildId,
    string DataSourceKey,
    string? TenantId,
    string? MappingProfile,
    SqlReadPreference ReadPreference)
{
    /// <summary>
    /// 从当前执行流中复制跟踪和数据库路由标量。
    /// </summary>
    /// <param name="operationId">操作标识，用于当前没有跟踪上下文时生成稳定跟踪 ID。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <returns>不包含服务或上下文对象的可序列化快照。</returns>
    public static OperationContextSnapshot Capture(string operationId, IDatabaseContextAccessor databaseContextAccessor)
    {
        var trace = TraceIdContext.Current;
        var database = databaseContextAccessor.Current;
        return new OperationContextSnapshot(
            trace?.TraceId ?? operationId,
            trace?.RootId,
            trace?.ParentId,
            trace?.ChildId,
            database?.DbKey ?? database?.DataSource?.Key ?? "orders",
            database?.TenantId,
            database?.MappingProfile,
            database?.ReadPreference ?? SqlReadPreference.Default);
    }

    /// <summary>
    /// 应用快照并返回负责恢复父上下文的作用域。
    /// </summary>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <returns>释放时恢复之前上下文的作用域。</returns>
    public IDisposable Enter(IDatabaseContextAccessor databaseContextAccessor) =>
        new Scope(this, databaseContextAccessor);

    private sealed class Scope : IDisposable
    {
        private readonly IDatabaseContextAccessor _databaseContextAccessor;
        private readonly TraceIdContext? _previousTrace;
        private readonly DatabaseContext? _previousDatabase;
        private int _disposed;

        public Scope(OperationContextSnapshot snapshot, IDatabaseContextAccessor databaseContextAccessor)
        {
            _databaseContextAccessor = databaseContextAccessor ??
                throw new ArgumentNullException(nameof(databaseContextAccessor));
            _previousTrace = TraceIdContext.Current;
            _previousDatabase = databaseContextAccessor.Current;
            TraceIdContext.Current = new TraceIdContext(snapshot.TraceId, snapshot.RootId!, snapshot.ParentId!,
                snapshot.ChildId!);
            try
            {
                databaseContextAccessor.Current = new DatabaseContext
                {
                    DbKey = snapshot.DataSourceKey,
                    TenantId = snapshot.TenantId,
                    MappingProfile = snapshot.MappingProfile,
                    ReadPreference = snapshot.ReadPreference
                };
            }
            catch
            {
                TraceIdContext.Current = _previousTrace;
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                _databaseContextAccessor.Current = _previousDatabase;
            }
            finally
            {
                TraceIdContext.Current = _previousTrace;
            }
        }
    }
}
