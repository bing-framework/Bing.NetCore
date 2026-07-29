using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Mutations;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象基类
/// </summary>
public abstract class SqlExecutorBase : SqlQueryBase, ISqlExecutor
{
    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="SqlExecutorBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    protected SqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    #endregion

    #region Insert(插入实体)

    /// <inheritdoc />
    public virtual int Insert<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Insert(entity, options);
        return ExecuteSql(command.Sql, command.Parameters, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> InsertAsync<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Insert(entity, options);
        return ExecuteSqlAsync(command.Sql, command.Parameters, timeout);
    }

    #endregion

    #region Update(更新实体)

    /// <inheritdoc />
    public virtual int Update<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Update(entity, options);
        return ExecuteSql(command.Sql, command.Parameters, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> UpdateAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Update(entity, options);
        return ExecuteSqlAsync(command.Sql, command.Parameters, timeout);
    }

    #endregion

    #region Delete(删除实体)

    /// <inheritdoc />
    public virtual int Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Delete(entity, options);
        return ExecuteSql(command.Sql, command.Parameters, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> DeleteAsync<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Delete(entity, options);
        return ExecuteSqlAsync(command.Sql, command.Parameters, timeout);
    }

    #endregion

    #region ExecuteSql(执行Sql增删改操作)

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual int ExecuteSql(string sql, object param = null, int? timeout = null)
    {
        using var executionLease = AcquireExecutionLease();
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var dbParameters = GetDbParameters(param, sql);
            var parameterMetadata = GetSqlParameterDiagnostics(param, sql);
            message = ExecuteBefore(sql, param, connection, parameterMetadata);
            result = connection.Execute(sql, dbParameters, transaction, timeout);
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

    #endregion

    #region ExecuteSqlAsync(执行Sql增删改操作)

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual async Task<int> ExecuteSqlAsync(string sql, object param = null, int? timeout = null)
    {
        using var executionLease = AcquireExecutionLease();
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var dbParameters = GetDbParameters(param, sql);
            var parameterMetadata = GetSqlParameterDiagnostics(param, sql);
            message = ExecuteBefore(sql, param, connection, parameterMetadata);
            result = await connection.ExecuteAsync(sql, dbParameters, transaction, timeout);
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

    #endregion

    #region ExecuteProcedure(执行存储过程增删改操作)

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual int ExecuteProcedure(string procedure, object param = null, int? timeout = null)
    {
        using var executionLease = AcquireExecutionLease();
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var dbParameters = GetDbParameters(param, procedure);
            var parameterMetadata = GetSqlParameterDiagnostics(param, procedure);
            message = ExecuteBefore(procedure, param, connection, parameterMetadata);
            result = connection.Execute(procedure, dbParameters, transaction, timeout, GetProcedureCommandType());
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

    #endregion

    #region ExecuteProcedureAsync(执行存储过程增删改操作)

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual async Task<int> ExecuteProcedureAsync(string procedure, object param = null, int? timeout = null)
    {
        using var executionLease = AcquireExecutionLease();
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var dbParameters = GetDbParameters(param, procedure);
            var parameterMetadata = GetSqlParameterDiagnostics(param, procedure);
            message = ExecuteBefore(procedure, param, connection, parameterMetadata);
            result = await connection.ExecuteAsync(procedure, dbParameters, transaction, timeout, GetProcedureCommandType());
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
    /// 执行指定的SQL语句
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">Sql 参数映射</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual int ExecuteSql<TEntity>(string sql, SqlParameterMap<TEntity> param, int? timeout = null)
        where TEntity : class => ExecuteSql(sql, (object)param, timeout);

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">Sql 参数映射</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual Task<int> ExecuteSqlAsync<TEntity>(string sql, SqlParameterMap<TEntity> param, int? timeout = null)
        where TEntity : class => ExecuteSqlAsync(sql, (object)param, timeout);

    /// <summary>
    /// 创建绑定当前 Executor 数据源和映射服务的实体写入 Builder。
    /// </summary>
    /// <returns>实体写入 Builder。</returns>
    private ISqlMutationBuilder CreateMutationBuilder()
    {
        var factory = ServiceProvider.GetService<ISqlMutationBuilderFactory>();
        if (factory == null)
            throw new InvalidOperationException("未注册实体写入 SQL Builder 工厂。");
        var databaseType = GetDatabaseType();
        var provider = ServiceProvider.GetServices<ISqlProvider>()
            .FirstOrDefault(item => item.DatabaseType == databaseType);
        if (provider == null)
            throw new InvalidOperationException($"未注册数据库类型 {databaseType} 的 SQL Provider。");
        return factory.Create(provider, CreateSqlBuilderServices());
    }

    #endregion
}
