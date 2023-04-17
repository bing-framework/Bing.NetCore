using System.Data;
using System.Text;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Database;
using Bing.Data.Sql.Diagnostics;
using Bing.DependencyInjection;
using Bing.Extensions;
using Bing.Helpers;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象基类
/// </summary>
public abstract partial class SqlQueryBase : ISqlQuery, IClauseAccessor, IUnionAccessor, ICteAccessor, IDbConnectionManager, IDbTransactionManager
{
    #region 字段

    /// <summary>
    /// Sql生成器
    /// </summary>
    private ISqlBuilder _sqlBuilder;

    /// <summary>
    /// 数据库连接
    /// </summary>
    private IDbConnection _connection;

    /// <summary>
    /// 事务
    /// </summary>
    private IDbTransaction _transaction;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="SqlQueryBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="sqlBuilder">Sql生成器</param>
    /// <param name="database">数据库</param>
    /// <param name="sqlOptions">Sql配置</param>
    protected SqlQueryBase(IServiceProvider serviceProvider, IDatabase database, SqlOptions sqlOptions = null)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Database = database;
        SqlOptions = sqlOptions ?? GetOptions();
        Logger = CreateLogger();
        ContextId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    private SqlOptions GetOptions()
    {
        try
        {
            var options = ServiceLocator.Instance.GetService<IOptionsSnapshot<SqlOptions>>();
            return options == null ? new SqlOptions() : options.Value;
        }
        catch
        {
            return new SqlOptions();
        }
    }

    /// <summary>
    /// 创建日志
    /// </summary>
    private ILogger CreateLogger()
    {
        var loggerFactory = ServiceProvider.GetService<ILoggerFactory>();
        if (loggerFactory == null)
            return NullLogger.Instance;
        return loggerFactory.CreateLogger(GetType());
    }

    #endregion

    #region 属性

    /// <summary>
    /// 上下文标识
    /// </summary>
    public string ContextId { get; private set; }

    /// <inheritdoc />
    public ISqlBuilder SqlBuilder => _sqlBuilder ??= CreateSqlBuilder();

    /// <summary>
    /// 服务提供程序
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 日志操作
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// 数据库
    /// </summary>
    protected IDatabase Database { get; private set; }

    /// <summary>
    /// Sql配置
    /// </summary>
    protected SqlOptions SqlOptions { get; set; }

    /// <summary>
    /// Sql生成器
    /// </summary>
    protected ISqlBuilder Builder { get; }

    /// <summary>
    /// Select子句
    /// </summary>
    public ISelectClause SelectClause => ((IClauseAccessor)Builder).SelectClause;

    /// <summary>
    /// From子句
    /// </summary>
    public IFromClause FromClause => ((IClauseAccessor)Builder).FromClause;

    /// <summary>
    /// Join子句
    /// </summary>
    public IJoinClause JoinClause => ((IClauseAccessor)Builder).JoinClause;

    /// <summary>
    /// Where子句
    /// </summary>
    public IWhereClause WhereClause => ((IClauseAccessor)Builder).WhereClause;

    /// <summary>
    /// GroupBy子句
    /// </summary>
    public IGroupByClause GroupByClause => ((IClauseAccessor)Builder).GroupByClause;

    /// <summary>
    /// OrderBy子句
    /// </summary>
    public IOrderByClause OrderByClause => ((IClauseAccessor)Builder).OrderByClause;

    /// <summary>
    /// 参数列表
    /// </summary>
    protected IReadOnlyDictionary<string, object> Params => Builder.GetParams();

    /// <summary>
    /// 是否包含联合操作
    /// </summary>
    public bool IsUnion => ((IUnionAccessor)Builder).IsUnion;

    /// <summary>
    /// 联合操作项集合
    /// </summary>
    public List<BuilderItem> UnionItems => ((IUnionAccessor)Builder).UnionItems;

    /// <summary>
    /// 公用表表达式CTE集合
    /// </summary>
    public List<BuilderItem> CteItems => ((ICteAccessor)Builder).CteItems;

    #endregion

    #region 工厂方法

    /// <summary>
    /// 创建Sql生成器
    /// </summary>
    protected abstract ISqlBuilder CreateSqlBuilder();

    #endregion

    #region SetConnection(设置数据库连接)

    /// <summary>
    /// 设置数据库连接
    /// </summary>
    /// <param name="connection">数据库连接</param>
    public void SetConnection(IDbConnection connection)
    {
        if (connection == null)
            return;
        _connection = connection;
    }

    #endregion

    #region GetConnection(获取数据库连接)

    /// <summary>
    /// 获取数据库连接
    /// </summary>
    public IDbConnection GetConnection()
    {
        if (_connection != null)
            return _connection;
        _connection = Database.GetConnection();
        if (_connection == null)
            throw new InvalidOperationException("数据库连接不能为空");
        return _connection;
    }

    #endregion

    #region Config(配置)

    /// <summary>
    /// 配置
    /// </summary>
    /// <param name="configAction">配置操作</param>
    public void Config(Action<SqlOptions> configAction) => configAction?.Invoke(SqlOptions);

    #endregion

    /// <summary>
    /// 在执行之后清空Sql和参数
    /// </summary>
    protected void ClearAfterExecution()
    {
        if (SqlOptions.IsClearAfterExecution == false)
            return;
        Builder.Clear();
    }

    /// <summary>
    /// 获取调试Sql语句
    /// </summary>
    public string GetDebugSql() => Builder.ToDebugSql();

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    protected string GetSql() => Builder.ToSql();

    /// <summary>
    /// 获取Sql生成器
    /// </summary>
    public ISqlBuilder GetBuilder() => Builder;

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="connection">数据库连接</param>
    public virtual object ToScalar(IDbConnection connection = null) =>
        Query((con, sql, sqlParams) => con.ExecuteScalar(sql, sqlParams), connection);

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="connection">数据库连接</param>
    public virtual async Task<object> ToScalarAsync(IDbConnection connection = null) =>
        await QueryAsync(async (con, sql, sqlParams) => await con.ExecuteScalarAsync(sql, sqlParams),
            connection);

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="connection">数据库连接</param>
    public virtual TResult To<TResult>(IDbConnection connection = null) =>
        Query((con, sql, sqlParams) => con.QueryFirstOrDefault<TResult>(sql, sqlParams), connection);

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="connection">数据库连接</param>
    public virtual async Task<TResult> ToAsync<TResult>(IDbConnection connection = null) =>
        await QueryAsync(async (con, sql, sqlParams) => await con.QueryFirstOrDefaultAsync<TResult>(sql, sqlParams), connection);

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="connection">数据库连接</param>
    public virtual List<TResult> ToList<TResult>(IDbConnection connection = null) =>
        Query((con, sql, sqlParams) => con.Query<TResult>(sql, sqlParams).ToList(), connection);

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="connection">数据库连接</param>
    public virtual async Task<List<TResult>> ToListAsync<TResult>(IDbConnection connection = null) =>
        await QueryAsync(async (con, sql, sqlParams) => (await con.QueryAsync<TResult>(sql, sqlParams)).ToList(), connection);

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="connection">数据库连接</param>
    public virtual async Task<List<TResult>> ToListAsync<TResult>(string sql, IDbConnection connection = null) =>
        (await GetConnection().QueryAsync<TResult>(sql, Params)).ToList();

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public virtual PagerList<TResult> ToPagerList<TResult>(IPager parameter = null, IDbConnection connection = null) =>
        PagerQuery(() => ToList<TResult>(connection), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public virtual async Task<PagerList<TResult>> ToPagerListAsync<TResult>(IPager parameter = null, IDbConnection connection = null) =>
        await PagerQueryAsync(async () => await ToListAsync<TResult>(connection), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="page">页数</param>
    /// <param name="pageSize">每页显示行数</param>
    /// <param name="connection">数据库连接</param>
    public virtual PagerList<TResult> ToPagerList<TResult>(int page, int pageSize, IDbConnection connection = null) =>
        ToPagerList<TResult>(new Pager(page, pageSize), connection);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public virtual PagerList<TResult> PagerQuery<TResult>(Func<List<TResult>> func, IPager parameter, IDbConnection connection = null)
    {
        parameter = GetPage(parameter);
        if (parameter.TotalCount == 0)
            parameter.TotalCount = GetCount(connection);
        SetPager(parameter);
        return new PagerList<TResult>(parameter, func());
    }

    /// <summary>
    /// 获取行数
    /// </summary>
    /// <param name="connection">数据库连接</param>
    protected int GetCount(IDbConnection connection)
    {
        DiagnosticsMessage message = null;
        try
        {
            var builder = GetCountBuilder();
            var sql = builder.ToSql();
            var conn = GetConnection();
            message = ExecuteBefore(sql, Params, conn);

            WriteTraceLog(sql, builder.GetParams(), builder.ToDebugSql());
            var result = conn.ExecuteScalar(sql, builder.GetParams());

            ExecuteAfter(message);
            return Conv.ToInt(result);
        }
        catch (Exception e)
        {
            ExecuteError(message, e);
            throw;
        }
    }

    /// <summary>
    /// 设置分页参数
    /// </summary>
    /// <param name="parameter">分页参数</param>
    private void SetPager(IPager parameter)
    {
        Builder.OrderBy(parameter.Order);
        Builder.Page(parameter);
    }

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public virtual async Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<Task<List<TResult>>> func, IPager parameter, IDbConnection connection = null)
    {
        parameter = GetPage(parameter);
        if (parameter.TotalCount == 0)
            parameter.TotalCount = await GetCountAsync(connection);
        SetPager(parameter);
        return new PagerList<TResult>(parameter, await func());
    }

    /// <summary>
    /// 获取行数
    /// </summary>
    protected async Task<int> GetCountAsync(IDbConnection connection)
    {
        DiagnosticsMessage message = null;
        try
        {
            var builder = GetCountBuilder();
            var sql = builder.ToSql();
            var conn = GetConnection();
            message = ExecuteBefore(sql, Params, conn);

            WriteTraceLog(sql, builder.GetParams(), builder.ToDebugSql());
            var result = await conn.ExecuteScalarAsync(sql, builder.GetParams());

            ExecuteAfter(message);
            return Conv.ToInt(result);
        }
        catch (Exception e)
        {
            ExecuteError(message, e);
            throw;
        }

    }

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="page">页数</param>
    /// <param name="pageSize">每页显示行数</param>
    /// <param name="connection">数据库连接</param>
    public virtual async Task<PagerList<TResult>> ToPagerListAsync<TResult>(int page, int pageSize, IDbConnection connection = null) =>
        await ToPagerListAsync<TResult>(new Pager(page, pageSize), connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="page">页数</param>
    /// <param name="pageSize">每页显示行数</param>
    /// <param name="connection">数据库连接</param>
    public virtual async Task<PagerList<TResult>> ToPagerListAsync<TResult>(string sql, int page, int pageSize, IDbConnection connection = null)
    {
        var result = await ToListAsync<TResult>(sql, connection);
        return new PagerList<TResult>(new Pager(page, pageSize), result);
    }

    /// <summary>
    /// 查询
    /// </summary>
    /// <typeparam name="TResult">实体类型</typeparam>
    /// <param name="func">查询操作</param>
    /// <param name="connection">数据库连接</param>
    public TResult Query<TResult>(Func<IDbConnection, string, IReadOnlyDictionary<string, object>, TResult> func, IDbConnection connection = null)
    {
        DiagnosticsMessage message = null;
        try
        {
            var sql = GetSql();
            var conn = GetConnection();
            message = ExecuteBefore(sql, Params, conn);

            WriteTraceLog(sql, Params, GetDebugSql());
            var result = func(conn, sql, Params);
            ClearAfterExecution();

            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            ExecuteError(message, e);
            throw;
        }
    }

    /// <summary>
    /// 查询
    /// </summary>
    /// <typeparam name="TResult">实体类型</typeparam>
    /// <param name="func">查询操作</param>
    /// <param name="connection">数据库连接</param>
    public async Task<TResult> QueryAsync<TResult>(Func<IDbConnection, string, IReadOnlyDictionary<string, object>, Task<TResult>> func, IDbConnection connection = null)
    {
        DiagnosticsMessage message = null;
        try
        {
            var sql = GetSql();
            var conn = GetConnection();
            message = ExecuteBefore(sql, Params, conn);

            WriteTraceLog(sql, Params, GetDebugSql());
            var result = await func(conn, sql, Params);
            ClearAfterExecution();

            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            ExecuteError(message, e);
            throw;
        }

    }

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="debugSql">调试Sql语句</param>
    protected virtual void WriteTraceLog(string sql, IReadOnlyDictionary<string, object> parameters, string debugSql)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false)
            return;
        var message = new StringBuilder();
        foreach (var param in parameters)
            message.AppendLine($"    {param.Key} : {GetParamLiterals(param.Value)} : {param.Value?.GetType()},");
        var result = message.ToString().RemoveEnd($",{Common.Line}");
        Logger.LogTrace("原始Sql:\r\n{Sql}\r\n调试Sql:\r\n{DebugSql}\r\nSql参数:\r\n{SqlParam}\r\n", sql, debugSql, result);
    }

    /// <summary>
    /// 获取参数字面值
    /// </summary>
    /// <param name="value">参数值</param>
    private static string GetParamLiterals(object value)
    {
        if (value == null)
            return "''";
        switch (value.GetType().Name.ToLower())
        {
            case "boolean":
                return Conv.ToBool(value) ? "1" : "0";
            case "int16":
            case "int32":
            case "int64":
            case "single":
            case "double":
            case "decimal":
                return value.SafeString();
            default:
                return $"'{value}'";
        }
    }

    /// <summary>
    /// 获取分页参数
    /// </summary>
    /// <param name="parameter">分页参数</param>
    protected IPager GetPage(IPager parameter)
    {
        if (parameter != null)
            return parameter;
        return Builder.Pager;
    }

    /// <summary>
    /// 获取行数Sql生成器
    /// </summary>
    protected ISqlBuilder GetCountBuilder()
    {
        var builder = Builder.Clone();
        ClearCountBuilder(builder);
        if (IsUnion)
            return GetCountBuilderByUnion(builder);
        if (IsGroup(builder))
            return GetCountBuilderByGroup(builder);
        return GetCountBuilder(builder);
    }

    /// <summary>
    /// 清空行数Sql生成器
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    private void ClearCountBuilder(ISqlBuilder builder)
    {
        builder.ClearOrderBy();
        builder.ClearPageParams();
    }

    /// <summary>
    /// 获取行数Sql生成器 - 联合
    /// </summary>
    /// <param name="countBuilder">行数Sql生成器</param>
    private ISqlBuilder GetCountBuilderByUnion(ISqlBuilder countBuilder) => countBuilder.New().Count().From(countBuilder, "t");

    /// <summary>
    /// 是否分组
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    private bool IsGroup(ISqlBuilder builder)
    {
        if (builder is IClauseAccessor accessor)
            return accessor.GroupByClause.IsGroup;
        return false;
    }

    /// <summary>
    /// 获取行数Sql生成器 - 分组
    /// </summary>
    /// <param name="countBuilder">行数Sql生成器</param>
    private ISqlBuilder GetCountBuilderByGroup(ISqlBuilder countBuilder)
    {
        countBuilder.ClearSelect();
        return countBuilder.New().Count().From(countBuilder.AppendSelect("1 As c"), "t");
    }

    /// <summary>
    /// 获取行数Sql生成器
    /// </summary>
    /// <param name="countBuilder">行数Sql生成器</param>
    private ISqlBuilder GetCountBuilder(ISqlBuilder countBuilder)
    {
        countBuilder.ClearSelect();
        return countBuilder.Count();
    }

    #region Dispose(释放资源)

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_connection != null)
            _connection.Dispose();
        _transaction = null;
    }

    #endregion
}
