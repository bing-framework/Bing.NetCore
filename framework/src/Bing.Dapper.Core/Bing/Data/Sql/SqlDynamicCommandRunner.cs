namespace Bing.Data.Sql;

/// <summary>集中保存 JIT 兼容执行；严格宿主关闭功能后，裁剪器移除实际 Dapper 调用。</summary>
internal static class SqlDynamicCommandRunner
{
    /// <summary>沿用 Dapper 的空集合、参数命名及 Provider 列表展开规则，仅供 JIT 兼容路径调用。</summary>
    internal static void BindList(IDbCommand command, string name, object value)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
#pragma warning disable CS0618 // Dapper 公开此方法供自定义 IDynamicParameters 使用。
        SqlMapper.PackListParameters(command, name, value);
#pragma warning restore CS0618
    }

    internal static IEnumerable<T> Query<T>(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.Query<T>(sql, param, transaction, buffered, commandTimeout, commandType);
    }

    internal static T QueryFirst<T>(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryFirst<T>(sql, param, transaction, commandTimeout, commandType);
    }

    internal static Task<T> QueryFirstAsync<T>(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryFirstAsync<T>(command);
    }

    internal static T QueryFirstOrDefault<T>(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryFirstOrDefault<T>(sql, param, transaction, commandTimeout, commandType);
    }

    internal static Task<T> QueryFirstOrDefaultAsync<T>(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryFirstOrDefaultAsync<T>(command);
    }

    internal static T QuerySingle<T>(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QuerySingle<T>(sql, param, transaction, commandTimeout, commandType);
    }

    internal static Task<T> QuerySingleAsync<T>(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QuerySingleAsync<T>(command);
    }

    internal static T QuerySingleOrDefault<T>(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QuerySingleOrDefault<T>(sql, param, transaction, commandTimeout, commandType);
    }

    internal static Task<T> QuerySingleOrDefaultAsync<T>(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QuerySingleOrDefaultAsync<T>(command);
    }

    internal static Task<IEnumerable<T>> QueryAsync<T>(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryAsync<T>(command);
    }

    internal static IEnumerable<TResult> Query<TFirst, TSecond, TResult>(IDbConnection connection, string sql, Func<TFirst, TSecond, TResult> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.Query(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    internal static Task<IEnumerable<TResult>> QueryAsync<TFirst, TSecond, TResult>(IDbConnection connection, CommandDefinition command, Func<TFirst, TSecond, TResult> map, string splitOn = "Id")
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryAsync(command, map, splitOn);
    }

    internal static IEnumerable<TResult> Query<TFirst, TSecond, TThird, TResult>(IDbConnection connection, string sql, Func<TFirst, TSecond, TThird, TResult> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.Query(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    internal static Task<IEnumerable<TResult>> QueryAsync<TFirst, TSecond, TThird, TResult>(IDbConnection connection, CommandDefinition command, Func<TFirst, TSecond, TThird, TResult> map, string splitOn = "Id")
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryAsync(command, map, splitOn);
    }

    internal static IEnumerable<TResult> Query<TFirst, TSecond, TThird, TFourth, TResult>(IDbConnection connection, string sql, Func<TFirst, TSecond, TThird, TFourth, TResult> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.Query(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    internal static Task<IEnumerable<TResult>> QueryAsync<TFirst, TSecond, TThird, TFourth, TResult>(IDbConnection connection, CommandDefinition command, Func<TFirst, TSecond, TThird, TFourth, TResult> map, string splitOn = "Id")
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryAsync(command, map, splitOn);
    }

    internal static IEnumerable<TResult> Query<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(IDbConnection connection, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.Query(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    internal static Task<IEnumerable<TResult>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(IDbConnection connection, CommandDefinition command, Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, string splitOn = "Id")
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryAsync(command, map, splitOn);
    }

    internal static IEnumerable<TResult> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(IDbConnection connection, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.Query(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    internal static Task<IEnumerable<TResult>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(IDbConnection connection, CommandDefinition command, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, string splitOn = "Id")
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryAsync(command, map, splitOn);
    }

    internal static IEnumerable<TResult> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(IDbConnection connection, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.Query(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    internal static Task<IEnumerable<TResult>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(IDbConnection connection, CommandDefinition command, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, string splitOn = "Id")
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryAsync(command, map, splitOn);
    }

    internal static int Execute(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.Execute(sql, param, transaction, commandTimeout, commandType);
    }

    internal static Task<int> ExecuteAsync(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.ExecuteAsync(command);
    }

    internal static T ExecuteScalar<T>(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.ExecuteScalar<T>(sql, param, transaction, commandTimeout, commandType);
    }

    internal static object ExecuteScalar(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.ExecuteScalar(sql, param, transaction, commandTimeout, commandType);
    }

    internal static Task<T> ExecuteScalarAsync<T>(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.ExecuteScalarAsync<T>(command);
    }

    internal static Task<object> ExecuteScalarAsync(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.ExecuteScalarAsync(command);
    }

    internal static Task<IDataReader> ExecuteReaderAsync(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.ExecuteReaderAsync(command);
    }

    internal static Func<IDataReader, T> GetRowParser<T>(IDataReader reader)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return reader.GetRowParser<T>();
    }

    internal static SqlMapper.GridReader QueryMultiple(IDbConnection connection, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryMultiple(sql, param, transaction, commandTimeout, commandType);
    }

    internal static Task<SqlMapper.GridReader> QueryMultipleAsync(IDbConnection connection, CommandDefinition command)
    {
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled) throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return connection.QueryMultipleAsync(command);
    }

}
