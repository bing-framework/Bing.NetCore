using Bing.Data.Sql.Diagnostics;

namespace Bing.Data.Sql;

// Sql查询对象 - 存储过程执行管线
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 在统一生命周期中执行存储过程查询
    /// </summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="procedure">存储过程名称</param>
    /// <param name="func">执行操作</param>
    /// <returns>执行结果</returns>
    protected TResult InternalProcedureQuery<TResult>(string procedure,
        Func<IDbConnection, string, object, IDbTransaction, TResult> func)
    {
        using var executionLease = AcquireExecutionLease();
        TResult result = default;
        DiagnosticsMessage message = null;
        var command = GetProcedure(procedure);
        try
        {
            if (ExecuteBefore() == false)
                return default;
            var connection = GetExecutionConnection();
            var dbParameters = GetDbParameters();
            var parameterMetadata = GetSqlParameterDiagnostics(SqlBuilder);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(command, Params, connection, parameterMetadata);
            WriteTraceLog(command, Params, command);
            result = func(connection, command, dbParameters, transaction);
            CompleteQueryTransaction();
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }

    /// <summary>
    /// 在统一生命周期中异步执行存储过程查询
    /// </summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="procedure">存储过程名称</param>
    /// <param name="func">执行操作</param>
    /// <returns>执行结果</returns>
    protected async Task<TResult> InternalProcedureQueryAsync<TResult>(string procedure,
        Func<IDbConnection, string, object, IDbTransaction, Task<TResult>> func)
    {
        using var executionLease = AcquireExecutionLease();
        TResult result = default;
        DiagnosticsMessage message = null;
        var command = GetProcedure(procedure);
        try
        {
            if (ExecuteBefore() == false)
                return default;
            var connection = GetExecutionConnection();
            var dbParameters = GetDbParameters();
            var parameterMetadata = GetSqlParameterDiagnostics(SqlBuilder);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(command, Params, connection, parameterMetadata);
            WriteTraceLog(command, Params, command);
            result = await func(connection, command, dbParameters, transaction);
            CompleteQueryTransaction();
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }
}