using System.Data.Common;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

public abstract partial class SqlExecutionBase
{
    private protected bool CanExecuteStatically(object parameters)
    {
        if (ExecutionMode == SqlExecutionMode.Compatible) return false;
        if (parameters == null || parameters is ISqlStaticParameterSource) return true;
        if (ExecutionMode == SqlExecutionMode.GeneratedOnly)
            throw new NotSupportedException("严格生成模式要求生成参数模型或结构化 SqlParam 参数。");
        return false;
    }

    private protected List<TResult> ReadStaticRows<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, SqlStaticResultReader<TResult> mapper,
        int maximum = int.MaxValue, CommandType commandType = CommandType.Text) =>
        SqlStaticCommandRunner.Execute(connection, sql, parameters, transaction, timeout, (command, cleanupExceptions) =>
        {
            var reader = command.ExecuteReader();
            return ExecuteWithReaderCleanup(reader, cleanupExceptions, currentReader =>
            {
                var read = mapper.Bind(currentReader);
                var rows = new List<TResult>();
                while (rows.Count < maximum && currentReader.Read()) rows.Add(read());
                return rows;
            });
        }, commandType);

    private protected Task<List<TResult>> ReadStaticRowsAsync<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, SqlStaticResultReader<TResult> mapper, CancellationToken token,
        int maximum = int.MaxValue, CommandType commandType = CommandType.Text) =>
        SqlStaticCommandRunner.ExecuteAsync(connection, sql, parameters, transaction, timeout,
            async (command, cancellation, cleanupExceptions) =>
        {
            var reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false);
            return await ExecuteWithReaderCleanupAsync(reader, cleanupExceptions, async (currentReader, readToken) =>
            {
                var read = mapper.Bind(currentReader);
                var rows = new List<TResult>();
                while (rows.Count < maximum && await currentReader.ReadAsync(readToken).ConfigureAwait(false)) rows.Add(read());
                return rows;
            }, cancellation).ConfigureAwait(false);
        }, token, commandType);

    internal static TResult ExecuteWithReaderCleanup<TResult>(DbDataReader reader,
        ICollection<Exception> cleanupExceptions, Func<DbDataReader, TResult> action)
    {
        try
        {
            return action(reader);
        }
        finally
        {
            SqlCleanupErrors.Dispose(cleanupExceptions, reader);
        }
    }

    internal static async Task<TResult> ExecuteWithReaderCleanupAsync<TResult>(DbDataReader reader,
        ICollection<Exception> cleanupExceptions,
        Func<DbDataReader, CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action(reader, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions,
                () => SqlTransactionAsyncAdapter.DisposeAsync(reader)).ConfigureAwait(false);
        }
    }

    private protected TResult ConvertScalar<TResult>(object value)
    {
        if (value == null || value is DBNull) return default;
        var converters = ServiceProvider.GetService<IFieldValueConverterSelector>() ?? new DefaultFieldValueConverterSelector();
        var converted = converters.ConvertFromProvider(value, new ColumnMappingMetadata { ClrType = typeof(TResult) }, GetDatabaseContext());
        return (TResult)SqlStaticResultReader<TResult>.ConvertValue(converted, typeof(TResult));
    }

    private protected SqlExecutionMode ExecutionMode =>
        !SqlRuntimeFeatures.ReflectionFallbackEnabled ? SqlExecutionMode.GeneratedOnly :
        ServiceProvider.GetService<SqlMetadataOptions>()?.ExecutionMode ?? SqlExecutionMode.Compatible;

    /// <summary>在取得连接前选择读取路径，数据库执行后不再回退或重发。</summary>
    private protected bool TryCreateStaticReader<TResult>(out SqlStaticResultReader<TResult> reader)
    {
        reader = null;
        if (ExecutionMode == SqlExecutionMode.Compatible) return false;
        if (SqlStaticResultReader<TResult>.IsScalar(typeof(TResult)))
        {
            reader = new SqlStaticResultReader<TResult>(null,
                ServiceProvider.GetService<IFieldValueConverterSelector>() ?? new DefaultFieldValueConverterSelector(),
                GetDatabaseContext());
            return true;
        }
        var model = ServiceProvider.GetService<IEntityModelMetadataProvider>()?.GetMetadata(typeof(TResult));
        if (model?.GeneratedFactory == null)
        {
            if (ExecutionMode == SqlExecutionMode.GeneratedOnly)
                throw new NotSupportedException($"结果类型 {typeof(TResult).FullName} 没有注册生成式结果模型。");
            return false;
        }
        var context = GetDatabaseContext();
        var mapping = EntityMappingResolver.Resolve(typeof(TResult), context);
        // 使用最终映射判断结果能力，避免把已排除成员误判为必需写入器。
        // 私有 setter 属于明确不支持的写入能力；真正的只读计算属性由 Reader 忽略。
        var canReadStatically = mapping.Model?.GeneratedFactory != null;
        if (canReadStatically)
        {
            foreach (var column in mapping.Columns.Values)
            {
                if (mapping.Model.Properties.TryGetValue(column.PropertyName, out var member) &&
                    !member.HasNonPublicSetter && (!member.CanWrite || member.GeneratedSetter != null))
                    continue;
                canReadStatically = false;
                break;
            }
        }
        if (!canReadStatically)
        {
            if (ExecutionMode == SqlExecutionMode.GeneratedOnly)
                throw new NotSupportedException($"结果类型 {typeof(TResult).FullName} 包含缺少生成写入能力的映射成员。");
            return false;
        }
        var converters = ServiceProvider.GetService<IFieldValueConverterSelector>() ?? new DefaultFieldValueConverterSelector();
        reader = new SqlStaticResultReader<TResult>(mapping, converters, context);
        return true;
    }
}
