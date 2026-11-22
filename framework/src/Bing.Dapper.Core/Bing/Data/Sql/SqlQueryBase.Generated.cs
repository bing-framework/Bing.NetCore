using System.Data.Common;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

public abstract partial class SqlQueryBase : ISqlGeneratedPlanExecutor, ISqlGeneratedSinglePlanExecutor,
    ISqlGeneratedTerminalPlanExecutor
{
    List<TResult> ISqlGeneratedPlanExecutor.ToGeneratedList<TResult>(SqlQueryPlan plan,
        Func<DbDataReader, TResult> read, int? timeout) => InternalQueryPlan(plan, (connection, sql, parameters, transaction) =>
    {
        return SqlStaticCommandRunner.Execute(connection, sql,
            new GeneratedParameters(parameters, plan.GeneratedParameterBinder), transaction, timeout, (command, errors) =>
        {
            var reader = command.ExecuteReader();
            return ExecuteWithReaderCleanup(reader, errors, current =>
            {
                var rows = new List<TResult>();
                while (current.Read()) rows.Add(read(current));
                return rows;
            });
        }, plan.CommandType);
    });

    Task<List<TResult>> ISqlGeneratedPlanExecutor.ToGeneratedListAsync<TResult>(SqlQueryPlan plan,
        Func<DbDataReader, TResult> read, int? timeout, CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(plan, async (connection, sql, parameters, transaction, token) =>
    {
        return await SqlStaticCommandRunner.ExecuteAsync(connection, sql,
            new GeneratedParameters(parameters, plan.GeneratedParameterBinder), transaction, timeout, async (command, operationToken, errors) =>
        {
            using var registration = operationToken.Register(command.Cancel);
            var reader = await command.ExecuteReaderAsync(operationToken).ConfigureAwait(false);
            return await ExecuteWithReaderCleanupAsync(reader, errors, async (current, readToken) =>
            {
                var rows = new List<TResult>();
                while (await current.ReadAsync(readToken).ConfigureAwait(false)) rows.Add(read(current));
                return rows;
            }, operationToken).ConfigureAwait(false);
        }, token, plan.CommandType).ConfigureAwait(false);
    }, cancellationToken);

    TResult ISqlGeneratedSinglePlanExecutor.ToGeneratedSingle<TResult>(SqlQueryPlan plan,
        Func<DbDataReader, TResult> read, int? timeout) => InternalQueryPlan(plan, (connection, sql, parameters, transaction) =>
    {
        return SqlStaticCommandRunner.Execute(connection, sql,
            new GeneratedParameters(parameters, plan.GeneratedParameterBinder), transaction, timeout, (command, errors) =>
        {
            var reader = command.ExecuteReader();
            return ExecuteWithReaderCleanup(reader, errors, current =>
            {
                if (!current.Read()) return default;
                var result = read(current);
                if (current.Read())
                {
                    // 与普通 SingleOrDefault 保持一致：先尝试读取第二行，再报告基数错误。
                    read(current);
                    throw new InvalidOperationException("Sequence contains more than one element");
                }
                return result;
            });
        }, plan.CommandType);
    });

    Task<TResult> ISqlGeneratedSinglePlanExecutor.ToGeneratedSingleAsync<TResult>(SqlQueryPlan plan,
        Func<DbDataReader, TResult> read, int? timeout, CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(plan, async (connection, sql, parameters, transaction, token) =>
        {
            return await SqlStaticCommandRunner.ExecuteAsync(connection, sql,
                new GeneratedParameters(parameters, plan.GeneratedParameterBinder), transaction, timeout,
                async (command, operationToken, errors) =>
                {
                    using var registration = operationToken.Register(command.Cancel);
                    var reader = await command.ExecuteReaderAsync(operationToken).ConfigureAwait(false);
                    return await ExecuteWithReaderCleanupAsync(reader, errors, async (current, readToken) =>
                    {
                        if (!await current.ReadAsync(readToken).ConfigureAwait(false)) return default;
                        var result = read(current);
                        if (await current.ReadAsync(readToken).ConfigureAwait(false))
                        {
                            // 与同步路径相同，第二行的读取异常优先于基数错误。
                            read(current);
                            throw new InvalidOperationException("Sequence contains more than one element");
                        }
                        return result;
                    }, operationToken).ConfigureAwait(false);
                }, token, plan.CommandType).ConfigureAwait(false);
        }, cancellationToken);

    TResult ISqlGeneratedTerminalPlanExecutor.ToGeneratedTerminal<TResult>(SqlQueryPlan plan,
        Func<DbDataReader, TResult> read, SqlGeneratedTerminal terminal, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) =>
            SqlStaticCommandRunner.Execute(connection, sql,
                new GeneratedParameters(parameters, plan.GeneratedParameterBinder), transaction, timeout, (command, errors) =>
            {
                var reader = command.ExecuteReader();
                return ExecuteWithReaderCleanup(reader, errors, current =>
                    ReadGeneratedTerminal(current, read, terminal));
            }, plan.CommandType));

    Task<TResult> ISqlGeneratedTerminalPlanExecutor.ToGeneratedTerminalAsync<TResult>(SqlQueryPlan plan,
        Func<DbDataReader, TResult> read, SqlGeneratedTerminal terminal, int? timeout,
        CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(plan, async (connection, sql, parameters, transaction, token) =>
            await SqlStaticCommandRunner.ExecuteAsync(connection, sql,
                new GeneratedParameters(parameters, plan.GeneratedParameterBinder), transaction, timeout,
                async (command, operationToken, errors) =>
                {
                    using var registration = operationToken.Register(command.Cancel);
                    var reader = await command.ExecuteReaderAsync(operationToken).ConfigureAwait(false);
                    return await ExecuteWithReaderCleanupAsync(reader, errors,
                        (current, readToken) => ReadGeneratedTerminalAsync(current, read, terminal, readToken),
                        operationToken).ConfigureAwait(false);
                }, token, plan.CommandType).ConfigureAwait(false), cancellationToken);

    /// <summary>在读取器上执行生成式基数终结。</summary>
    private static TResult ReadGeneratedTerminal<TResult>(DbDataReader reader,
        Func<DbDataReader, TResult> read, SqlGeneratedTerminal terminal)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        if (read == null) throw new ArgumentNullException(nameof(read));
        if (reader.Read() == false)
        {
            if (terminal is SqlGeneratedTerminal.First or SqlGeneratedTerminal.Single)
                throw new InvalidOperationException("Sequence contains no elements");
            return default;
        }
        var result = read(reader);
        if (terminal is SqlGeneratedTerminal.Single or SqlGeneratedTerminal.SingleOrDefault)
        {
            if (reader.Read())
            {
                read(reader);
                throw new InvalidOperationException("Sequence contains more than one element");
            }
        }
        return result;
    }

    /// <summary>异步在读取器上执行生成式基数终结。</summary>
    private static async Task<TResult> ReadGeneratedTerminalAsync<TResult>(DbDataReader reader,
        Func<DbDataReader, TResult> read, SqlGeneratedTerminal terminal, CancellationToken cancellationToken)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        if (read == null) throw new ArgumentNullException(nameof(read));
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) == false)
        {
            if (terminal is SqlGeneratedTerminal.First or SqlGeneratedTerminal.Single)
                throw new InvalidOperationException("Sequence contains no elements");
            return default;
        }
        var result = read(reader);
        if (terminal is SqlGeneratedTerminal.Single or SqlGeneratedTerminal.SingleOrDefault)
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                read(reader);
                throw new InvalidOperationException("Sequence contains more than one element");
            }
        }
        return result;
    }

    /// <summary>只适配生成参数绑定；命令和连接生命周期由共享执行器负责。</summary>
    private sealed class GeneratedParameters : ISqlStaticParameterSource
    {
        private readonly IEnumerable<SqlParam> _values;
        private readonly Action<DbCommand, IEnumerable<SqlParam>> _bind;
        internal GeneratedParameters(object parameters, Action<DbCommand, IEnumerable<SqlParam>> bind)
        {
            _values = parameters as IEnumerable<SqlParam> ?? throw new NotSupportedException("生成式执行要求结构化参数快照。");
            _bind = bind ?? throw new NotSupportedException("缺少生成参数绑定器。");
        }
        public void BindTo(IDbCommand command) => _bind((DbCommand)command, _values);
    }
}
