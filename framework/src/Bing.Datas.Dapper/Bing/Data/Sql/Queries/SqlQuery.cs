using System.Data;
using System.Text;
using Bing.Data.Sql.Diagnostics;
using Bing.Extensions;
using Bing.Helpers;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Bing.Data.Sql.Queries;

/// <summary>
/// 基于Dapper的Sql查询对象
/// </summary>
public class SqlQuery : SqlQueryBase
{
    /// <summary>
    /// 初始化一个<see cref="SqlQuery"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="sqlBuilder">Sql生成器</param>
    /// <param name="database">数据库</param>
    public SqlQuery(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database = null) : base(serviceProvider, sqlBuilder, database)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="SqlQuery"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="sqlBuilder">Sql生成器</param>
    /// <param name="database">数据库</param>
    /// <param name="sqlQueryOptions">Sql查询配置</param>
    protected SqlQuery(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database, SqlOptions sqlQueryOptions) : base(serviceProvider, sqlBuilder, database, sqlQueryOptions) { }

    #region 属性



    #endregion

    /// <summary>
    /// 克隆
    /// </summary>
    public override ISqlQuery Clone()
    {
        var result = new SqlQuery(ServiceProvider, Builder.Clone(), Database, SqlOptions);
        result.SetConnection(Connection);
        return result;
    }

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="connection">数据库连接</param>
    public override object ToScalar(IDbConnection connection = null) =>
        Query((con, sql, sqlParams) => con.ExecuteScalar(sql, sqlParams), connection);

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="connection">数据库连接</param>
    public override async Task<object> ToScalarAsync(IDbConnection connection = null) =>
        await QueryAsync(async (con, sql, sqlParams) => await con.ExecuteScalarAsync(sql, sqlParams),
            connection);

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="connection">数据库连接</param>
    public override TResult To<TResult>(IDbConnection connection = null) =>
        Query((con, sql, sqlParams) => con.QueryFirstOrDefault<TResult>(sql, sqlParams), connection);

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="connection">数据库连接</param>
    public override async Task<TResult> ToAsync<TResult>(IDbConnection connection = null) =>
        await QueryAsync(async (con, sql, sqlParams) => await con.QueryFirstOrDefaultAsync<TResult>(sql, sqlParams), connection);

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="connection">数据库连接</param>
    public override List<TResult> ToList<TResult>(IDbConnection connection = null) =>
        Query((con, sql, sqlParams) => con.Query<TResult>(sql, sqlParams).ToList(), connection);

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="connection">数据库连接</param>
    public override async Task<List<TResult>> ToListAsync<TResult>(IDbConnection connection = null) =>
        await QueryAsync(async (con, sql, sqlParams) => (await con.QueryAsync<TResult>(sql, sqlParams)).ToList(), connection);

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="connection">数据库连接</param>
    public override async Task<List<TResult>> ToListAsync<TResult>(string sql, IDbConnection connection = null) =>
        (await GetConnection(connection).QueryAsync<TResult>(sql, Params)).ToList();

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public override PagerList<TResult> ToPagerList<TResult>(IPager parameter = null, IDbConnection connection = null) =>
        PagerQuery(() => ToList<TResult>(connection), parameter, connection);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public override PagerList<TResult> PagerQuery<TResult>(Func<List<TResult>> func, IPager parameter, IDbConnection connection = null)
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

            message = ExecuteBefore(sql, Params);

            WriteTraceLog(sql, builder.GetParams(), builder.ToDebugSql());
            var result = GetConnection(connection).ExecuteScalar(sql, builder.GetParams());

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
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="page">页数</param>
    /// <param name="pageSize">每页显示行数</param>
    /// <param name="connection">数据库连接</param>
    public override PagerList<TResult> ToPagerList<TResult>(int page, int pageSize, IDbConnection connection = null) =>
        ToPagerList<TResult>(new Pager(page, pageSize), connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public override async Task<PagerList<TResult>> ToPagerListAsync<TResult>(IPager parameter = null, IDbConnection connection = null) =>
        await PagerQueryAsync(async () => await ToListAsync<TResult>(connection), parameter, connection);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public override async Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<Task<List<TResult>>> func, IPager parameter, IDbConnection connection = null)
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
    /// <param name="connection">数据库连接</param>
    protected async Task<int> GetCountAsync(IDbConnection connection)
    {
        DiagnosticsMessage message = null;
        try
        {
            var builder = GetCountBuilder();
            var sql = builder.ToSql();

            message = ExecuteBefore(sql, Params);

            WriteTraceLog(sql, builder.GetParams(), builder.ToDebugSql());
            var result = await GetConnection(connection).ExecuteScalarAsync(sql, builder.GetParams());

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
    public override async Task<PagerList<TResult>> ToPagerListAsync<TResult>(int page, int pageSize, IDbConnection connection = null) =>
        await ToPagerListAsync<TResult>(new Pager(page, pageSize), connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="page">页数</param>
    /// <param name="pageSize">每页显示行数</param>
    /// <param name="connection">数据库连接</param>
    public override async Task<PagerList<TResult>> ToPagerListAsync<TResult>(string sql, int page, int pageSize, IDbConnection connection = null)
    {
        var result = await ToListAsync<TResult>(sql, connection);
        return new PagerList<TResult>(new Pager(page, pageSize), result);
    }

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="debugSql">调试Sql语句</param>
    protected override void WriteTraceLog(string sql, IReadOnlyDictionary<string, object> parameters, string debugSql)
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
}
