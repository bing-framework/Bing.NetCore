using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using Oracle.ManagedDataAccess.Client;

namespace Bing.Data.Sql;

/// <summary>把 Oracle 写入 Builder 的冻结命令转换为回传命令。</summary>
public static class OracleReturningExtensions
{
    internal const string RowCountParameter = "bing_returning_rows";
    private const string MaxRowsParameter = "bing_returning_max_rows";

    /// <summary>为 Oracle INSERT VALUES、UPDATE 或 DELETE 生成单行回传命令。</summary>
    /// <param name="command">由 Oracle Builder 冻结的纯输入写入命令。</param>
    /// <param name="columns">要回传的物理列和参数类型。</param>
    /// <returns>可独立使用的 Oracle 单行回传命令。</returns>
    /// <remarks>Oracle 在同一语句内拒绝影响多行的写入。</remarks>
    public static OracleReturningCommand ReturningInto(this SqlWriteCommand command, params OracleReturningColumn[] columns) =>
        CreateReturning(command, columns, maxRows: 0);

    /// <summary>为 Oracle UPDATE 或 DELETE 生成有最大回传行数的多行命令。</summary>
    /// <param name="command">Oracle Builder 冻结的纯输入写入命令。</param>
    /// <param name="maxRows">最多允许回传的行数，同时也是每列输出绑定容量。</param>
    /// <param name="columns">要回传的整数、十进制数、单精度浮点数、字符串、二进制或 Oracle DATE 列。</param>
    /// <returns>可独立使用的 Oracle 多行回传命令。</returns>
    /// <remarks>超过上限时 Oracle 撤销本次写入并抛出 ORA-20060。返回顺序由 Oracle 决定。</remarks>
    public static OracleReturningCommand ReturningManyInto(this SqlWriteCommand command, int maxRows, params OracleReturningColumn[] columns)
    {
        if (maxRows <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRows));
        return CreateReturning(command, columns, maxRows);
    }

    private static OracleReturningCommand CreateReturning(SqlWriteCommand command, OracleReturningColumn[] columns, int maxRows)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (!string.Equals(command.ProviderKey, OracleSqlProvider.Instance.Key, StringComparison.OrdinalIgnoreCase) ||
            command.HasReturning || command.OperationKind is not (SqlOperationKind.InsertValues or SqlOperationKind.Update or SqlOperationKind.Delete))
            throw new NotSupportedException("ReturningInto 仅支持 Oracle INSERT VALUES、UPDATE 和 DELETE 命令。");
        if (maxRows > 0 && (command.OperationKind == SqlOperationKind.InsertValues || command.ValidateAffectedRows))
            throw new NotSupportedException("ReturningManyInto 只支持未要求影响一行的 Oracle UPDATE 和 DELETE 命令。");
        if (columns == null || columns.Length == 0)
            throw new ArgumentException("ReturningInto 必须指定至少一个输出列。", nameof(columns));

        var parameters = new List<SqlParam>(command.CreateParameters());
        if (parameters.Any(parameter => parameter.Direction is not (null or ParameterDirection.Input)))
            throw new NotSupportedException("ReturningInto 要求原命令仅包含输入参数。");

        var names = new HashSet<string>(parameters.Select(parameter => parameter.Name.TrimStart(':')), StringComparer.OrdinalIgnoreCase);
        if (!names.Add(RowCountParameter))
            throw new ArgumentException($"输入参数不能使用 ReturningInto 行数参数名 '{RowCountParameter}'。", nameof(command));
        if (maxRows > 0 && !names.Add(MaxRowsParameter))
            throw new ArgumentException($"输入参数不能使用 ReturningManyInto 容量参数名 '{MaxRowsParameter}'。", nameof(command));
        var bulkOutputs = maxRows > 0 ? new (string Name, OracleDbType Type, int? Size)[columns.Length] : null;
        var identifiers = new string[columns.Length];
        var targets = new string[columns.Length];
        var declarations = bulkOutputs == null ? null : new StringBuilder("Declare ");
        var assignments = bulkOutputs == null ? null : new StringBuilder();
        for (var i = 0; i < columns.Length; i++)
        {
            var column = columns[i] ?? throw new ArgumentException("ReturningInto 输出列不能为空。", nameof(columns));
            if (!IsBindName(column.Name) || !names.Add(column.Name))
                throw new ArgumentException($"ReturningInto 输出参数名 '{column.Name}' 无效或重复。", nameof(columns));
            identifiers[i] = OracleDialect.Instance.SafeName(column.Column);
            targets[i] = ":" + column.Name;
            parameters.Add(new SqlParam(column.Name, null, column.DbType, ParameterDirection.Output, column.Size));
            if (bulkOutputs != null)
            {
                var (type, localType) = GetBulkOutputType(column);
                var local = "bing_return_col_" + i.ToString(CultureInfo.InvariantCulture);
                declarations.Append($"Type {local}_t Is Table Of {localType} Index By Pls_Integer; {local} {local}_t; ");
                assignments.Append($":{column.Name} := {local}; ");
                targets[i] = local;
                bulkOutputs[i] = (column.Name, type, column.Size);
            }
        }
        parameters.Add(new SqlParam(RowCountParameter, null, DbType.Int32, ParameterDirection.Output));

        var into = maxRows > 0 ? "Bulk Collect Into" : "Into";
        var sql = $"{declarations}Begin {command.Sql.TrimEnd()} Returning {string.Join(", ", identifiers)} {into} {string.Join(", ", targets)}; " +
                  $":{RowCountParameter} := SQL%ROWCOUNT; ";
        if (maxRows > 0)
        {
            parameters.Add(new SqlParam(MaxRowsParameter, maxRows, DbType.Int32, ParameterDirection.Input));
            // 先检查局部集合的行数，再复制到容量固定的客户端绑定，避免 Oracle 提前抛出 ORA-06513。
            sql += $"If :{RowCountParameter} > :{MaxRowsParameter} Then Raise_Application_Error(-20060, 'ReturningManyInto row limit exceeded'); End If; " + assignments;
        }
        return new OracleReturningCommand(new SqlWriteCommand(sql + "End;", parameters, OracleSqlProvider.Instance,
            command.OperationKind, hasReturning: false, validateAffectedRows: command.ValidateAffectedRows), maxRows, bulkOutputs);
    }

    private static (OracleDbType Type, string LocalType) GetBulkOutputType(OracleReturningColumn column)
    {
        var size = column.Size?.ToString(CultureInfo.InvariantCulture);
        return column.DbType switch
        {
            DbType.Byte or DbType.Int16 or DbType.Int32 or DbType.Int64 or DbType.Decimal => (OracleDbType.Decimal, "Number"),
            DbType.String => (OracleDbType.NVarchar2, $"NVarchar2({size})"),
            DbType.AnsiString => (OracleDbType.Varchar2, $"Varchar2({size} Char)"),
            DbType.StringFixedLength => (OracleDbType.NChar, $"NChar({size})"),
            DbType.AnsiStringFixedLength => (OracleDbType.Char, $"Char({size} Char)"),
            DbType.Single => (OracleDbType.BinaryFloat, "Binary_Float"),
            DbType.Binary => (OracleDbType.Raw, $"Raw({size})"),
            DbType.Date => (OracleDbType.Date, "Date"),
            _ => throw new NotSupportedException($"ReturningManyInto 不支持输出参数类型 '{column.DbType}'。")
        };
    }

    /// <summary>执行回传命令，并用委托逐行投影。</summary>
    /// <typeparam name="TResult">投影结果类型。</typeparam>
    /// <param name="executor">Oracle 写执行器。</param>
    /// <param name="command">回传命令。</param>
    /// <param name="project">从输出参数创建结果的委托。</param>
    /// <param name="timeout">超时时间，单位为秒。</param>
    /// <returns>按 Oracle 输出顺序排列的投影结果。</returns>
    public static List<TResult> ExecuteReturning<TResult>(this ISqlExecutor executor, OracleReturningCommand command,
        Func<ISqlOutputParameterAccessor, TResult> project, int? timeout = null) =>
        GetOracleExecutor(executor).ExecuteReturningInto(command, project, timeout);

    /// <summary>异步执行回传命令，并用委托逐行投影。</summary>
    /// <typeparam name="TResult">投影结果类型。</typeparam>
    /// <param name="executor">Oracle 写执行器。</param>
    /// <param name="command">回传命令。</param>
    /// <param name="project">从输出参数创建结果的委托。</param>
    /// <param name="timeout">超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按 Oracle 输出顺序排列的投影结果。</returns>
    public static Task<List<TResult>> ExecuteReturningAsync<TResult>(this ISqlExecutor executor, OracleReturningCommand command,
        Func<ISqlOutputParameterAccessor, TResult> project, int? timeout = null, CancellationToken cancellationToken = default) =>
        GetOracleExecutor(executor).ExecuteReturningIntoAsync(command, project, timeout, cancellationToken);

    private static OracleSqlExecutorBase GetOracleExecutor(ISqlExecutor executor) =>
        executor as OracleSqlExecutorBase ?? throw new NotSupportedException("ReturningInto 要求 Oracle 写执行器。");

    /// <summary>参数名需要能直接出现在 Oracle 占位符中。</summary>
    private static bool IsBindName(string name) => name.Length > 0 &&
        IsAsciiLetter(name[0]) && name.All(value => IsAsciiLetter(value) || value is '_' or >= '0' and <= '9');

    private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
