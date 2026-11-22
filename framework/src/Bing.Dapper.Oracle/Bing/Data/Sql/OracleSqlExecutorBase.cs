using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Sql.Builders;
using Bing.Exceptions;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle Sql执行器
/// </summary>
public abstract class OracleSqlExecutorBase : SqlExecutorBase
{
    /// <summary>
    /// 初始化一个 <see cref="OracleSqlExecutorBase"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    protected OracleSqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(OracleSqlProvider.Instance);

    // Fluent 调用顺序可能与最终 SQL 的占位符顺序不同，回传命令按名称绑定。
    internal List<TResult> ExecuteReturningInto<TResult>(OracleReturningCommand command,
        Func<ISqlOutputParameterAccessor, TResult> project, int? timeout)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        if (project == null) throw new ArgumentNullException(nameof(project));
        return ExecuteWithOutput(command.Sql, command.Parameters, timeout, storedProcedure: false,
            (rows, output) => ProjectReturningRows(rows, output, command, project), dbCommand =>
            {
                ConfigureReturning((OracleCommand)dbCommand, command);
                return dbCommand.ExecuteNonQuery();
            });
    }

    internal Task<List<TResult>> ExecuteReturningIntoAsync<TResult>(OracleReturningCommand command,
        Func<ISqlOutputParameterAccessor, TResult> project, int? timeout, CancellationToken cancellationToken)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        if (project == null) throw new ArgumentNullException(nameof(project));
        return ExecuteWithOutputAsync(command.Sql, command.Parameters, timeout, storedProcedure: false,
            (rows, output) => ProjectReturningRows(rows, output, command, project), cancellationToken, (dbCommand, token) =>
            {
                ConfigureReturning((OracleCommand)dbCommand, command);
                return dbCommand.ExecuteNonQueryAsync(token);
            });
    }

    /// <summary>每次执行在当前 OracleCommand 上独立配置输出集合。</summary>
    private static void ConfigureReturning(OracleCommand dbCommand, OracleReturningCommand command)
    {
        dbCommand.BindByName = true;
        if (command.MaxRows == 0)
            return;

        foreach (var (name, type, size) in command.BulkOutputs)
        {
            var parameter = dbCommand.Parameters[name];
            parameter.OracleDbType = type;
            parameter.CollectionType = OracleCollectionType.PLSQLAssociativeArray;
            parameter.Size = command.MaxRows;
            if (type is OracleDbType.NVarchar2 or OracleDbType.Varchar2 or OracleDbType.Raw or OracleDbType.Char or OracleDbType.NChar)
            {
                var sizes = new int[command.MaxRows];
                // 固定字符列按字符声明宽度，驱动的输出缓冲须能容纳多字节字符。
                var bufferSize = type is OracleDbType.Char or OracleDbType.NChar ? checked(size.Value * 4) : size.Value;
                Array.Fill(sizes, bufferSize);
                parameter.ArrayBindSize = sizes;
            }
        }
    }

    /// <summary>按 PL/SQL 的实际影响行数，在命令仍有效时创建独立的逐行快照。</summary>
    private static List<TResult> ProjectReturningRows<TResult>(int rows, ISqlOutputParameterAccessor output,
        OracleReturningCommand command, Func<ISqlOutputParameterAccessor, TResult> project)
    {
        // 执行钩子跳过写入时不存在输出参数。
        if (rows == 0 && output == null)
            return new List<TResult>();
        var affectedRows = output.GetValue<int>(OracleReturningExtensions.RowCountParameter);
        if (command.MaxRows > 0)
            return ProjectBulkReturningRows(affectedRows, output, command, project);
        if (affectedRows == 1)
            return new List<TResult>(1) { project(SqlOutputParameterSnapshot.Create(output)) };
        if (command.Command.ValidateAffectedRows)
            throw new ConcurrencyException($"实体 Mutation 预期影响 1 行，实际影响 {affectedRows} 行。");
        return new List<TResult>();
    }

    private static List<TResult> ProjectBulkReturningRows<TResult>(int affectedRows, ISqlOutputParameterAccessor output,
        OracleReturningCommand command, Func<ISqlOutputParameterAccessor, TResult> project)
    {
        var results = new List<TResult>(affectedRows);
        if (affectedRows == 0)
            return results;

        var columns = command.BulkOutputs;
        var arrays = new Array[columns.Count];
        for (var column = 0; column < columns.Count; column++)
            arrays[column] = (Array)output.GetValue(columns[column].Name);

        var values = new KeyValuePair<string, object>[columns.Count];
        for (var row = 0; row < affectedRows; row++)
        {
            for (var column = 0; column < columns.Count; column++)
                values[column] = new KeyValuePair<string, object>(columns[column].Name,
                    ToClrValue(arrays[column].GetValue(row), columns[column].Type));
            results.Add(project(new SqlOutputParameterSnapshot(values)));
        }
        return results;
    }

    private static object ToClrValue(object value, OracleDbType type) => value switch
    {
        OracleDecimal number when number.IsNull => null,
        // BINARY_FLOAT 的驱动输出是 OracleDecimal，转 Single 可保留无穷和 NaN。
        OracleDecimal number when type == OracleDbType.BinaryFloat => number.ToSingle(),
        OracleDecimal number => number.Value,
        OracleString text => text.IsNull ? null : text.Value,
        OracleBinary bytes => bytes.IsNull ? null : bytes.Value,
        OracleDate date => date.IsNull ? null : date.Value,
        _ => value
    };
}
