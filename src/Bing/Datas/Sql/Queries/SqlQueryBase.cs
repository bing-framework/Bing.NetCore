using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Bing.Datas.Sql.Builders;
using Bing.Datas.Sql.Builders.Core;
using Bing.Datas.Sql.Configs;
using Bing.Domains.Repositories;
using Bing.Helpers;
using Microsoft.Extensions.Options;

namespace Bing.Datas.Sql.Queries
{
    /// <summary>
    /// Sql查询对象基类
    /// </summary>
    public abstract class SqlQueryBase : ISqlQuery, IClauseAccessor, IUnionAccessor
    {
        #region 属性

        /// <summary>
        /// 数据库
        /// </summary>
        protected IDatabase Database { get; }

        /// <summary>
        /// Sql查询配置
        /// </summary>
        protected SqlQueryOptions SqlQueryOptions { get; set; }

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
        public List<UnionItem> UnionItems => ((IUnionAccessor)Builder).UnionItems;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="SqlQueryBase"/>类型的实例
        /// </summary>
        /// <param name="sqlBuilder">Sql生成器</param>
        /// <param name="database">数据库</param>
        /// <param name="sqlQueryOptions">Sql查询配置</param>
        protected SqlQueryBase(ISqlBuilder sqlBuilder, IDatabase database = null, SqlQueryOptions sqlQueryOptions = null)
        {
            Builder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
            Database = database;
            SqlQueryOptions = sqlQueryOptions;
        }

        #endregion


        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public abstract ISqlQuery Clone();

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="configAction">配置操作</param>
        public void Config(Action<SqlQueryOptions> configAction)
        {
            SqlQueryOptions = new SqlQueryOptions();
            configAction?.Invoke(SqlQueryOptions);
        }

        /// <summary>
        /// 清空并初始化
        /// </summary>
        public ISqlQuery Clear()
        {
            Builder.Clear();
            return this;
        }

        /// <summary>
        /// 在执行之后清空Sql和参数
        /// </summary>
        protected void ClearAfterExecution()
        {
            if (SqlQueryOptions == null)
            {
                SqlQueryOptions = GetConfig();
            }

            if (SqlQueryOptions.IsClearAfterExecution == false)
            {
                return;
            }
            Clear();
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        private SqlQueryOptions GetConfig()
        {
            try
            {
                var options = Ioc.Create<IOptionsSnapshot<SqlQueryOptions>>();
                return options.Value;
            }
            catch
            {
                return new SqlQueryOptions();
            }
        }

        /// <summary>
        /// 获取调试Sql语句
        /// </summary>
        /// <returns></returns>
        public string GetDebugSql()
        {
            return Builder.ToDebugSql();
        }

        /// <summary>
        /// 获取Sql语句
        /// </summary>
        /// <returns></returns>
        protected string GetSql()
        {
            return Builder.ToSql();
        }

        /// <summary>
        /// 获取Sql生成器
        /// </summary>
        /// <returns></returns>
        public ISqlBuilder GetBuilder()
        {
            return Builder;
        }

        /// <summary>
        /// 获取单值
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract object ToScalar(IDbConnection connection = null);

        /// <summary>
        /// 获取单值
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract Task<object> ToScalarAsync(IDbConnection connection = null);

        /// <summary>
        /// 获取单个实体
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract TResult To<TResult>(IDbConnection connection = null);

        /// <summary>
        /// 获取单个实体
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract Task<TResult> ToAsync<TResult>(IDbConnection connection = null);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract List<TResult> ToList<TResult>(IDbConnection connection = null);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract Task<List<TResult>> ToListAsync<TResult>(IDbConnection connection = null);

        /// <summary>
        /// 获取分页列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="parameter">分页参数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract PagerList<TResult> ToPagerList<TResult>(IPager parameter = null,
            IDbConnection connection = null);

        /// <summary>
        /// 获取分页列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="parameter">分页参数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract Task<PagerList<TResult>> ToPagerListAsync<TResult>(IPager parameter = null,
            IDbConnection connection = null);

        /// <summary>
        /// 获取分页列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="page">页数</param>
        /// <param name="pageSize">每页显示行数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract PagerList<TResult> ToPagerList<TResult>(int page, int pageSize, IDbConnection connection = null);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="func">获取列表操作</param>
        /// <param name="parameter">分页参数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract PagerList<TResult> PagerQuery<TResult>(Func<List<TResult>> func, IPager parameter,
            IDbConnection connection = null);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="func">获取列表操作</param>
        /// <param name="parameter">分页参数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<Task<List<TResult>>> func,
            IPager parameter, IDbConnection connection = null);

        /// <summary>
        /// 获取分页列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="page">页数</param>
        /// <param name="pageSize">每页显示行数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public abstract Task<PagerList<TResult>> ToPagerListAsync<TResult>(int page, int pageSize, IDbConnection connection = null);

        /// <summary>
        /// 查询
        /// </summary>
        /// <typeparam name="TResult">实体类型</typeparam>
        /// <param name="func">查询操作</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public TResult Query<TResult>(Func<IDbConnection, string, IReadOnlyDictionary<string, object>, TResult> func, IDbConnection connection = null)
        {
            var sql = GetSql();
            WriteTraceLog(sql, Params, GetDebugSql());
            var result = func(GetConnection(connection), sql, Params);
            ClearAfterExecution();
            return result;
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <typeparam name="TResult">实体类型</typeparam>
        /// <param name="func">查询操作</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        public async Task<TResult> QueryAsync<TResult>(Func<IDbConnection, string, IReadOnlyDictionary<string, object>, Task<TResult>> func, IDbConnection connection = null)
        {
            var sql = GetSql();
            WriteTraceLog(sql, Params, GetDebugSql());
            var result = await func(GetConnection(connection), sql, Params);
            ClearAfterExecution();
            return result;
        }        

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        protected IDbConnection GetConnection(IDbConnection connection)
        {
            if (connection != null)
            {
                return connection;
            }
            connection = Database?.GetConnection();
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            return connection;
        }        

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="parameters">参数</param>
        /// <param name="debugSql">调试Sql语句</param>
        protected abstract void WriteTraceLog(string sql, IReadOnlyDictionary<string, object> parameters, string debugSql);

        /// <summary>
        /// 获取分页参数
        /// </summary>
        /// <param name="parameter">分页参数</param>
        /// <returns></returns>
        protected IPager GetPage(IPager parameter)
        {
            if (parameter != null)
            {
                return parameter;
            }

            return Builder.Pager;
        }

        /// <summary>
        /// 获取行数Sql生成器
        /// </summary>
        /// <returns></returns>
        protected ISqlBuilder GetCountBuilder()
        {
            var builder = Builder.Clone();
            ClearCountBuilder(builder);
            if (IsUnion)
            {
                return GetCountBuilderByUnion(builder);
            }
            if (IsGroup(builder))
            {
                return GetCountBuilderByGroup(builder);
            }

            return GetCountBuilder(builder);
        }

        /// <summary>
        /// 清空行数Sql生成器
        /// </summary>
        /// <param name="builder"></param>
        private void ClearCountBuilder(ISqlBuilder builder)
        {
            builder.ClearOrderBy();
            builder.ClearPageParams();
        }

        /// <summary>
        /// 获取行数Sql生成器 - 联合
        /// </summary>
        /// <param name="countBuilder">行数Sql生成器</param>
        /// <returns></returns>
        private ISqlBuilder GetCountBuilderByUnion(ISqlBuilder countBuilder)
        {
            return countBuilder.New().Count().From(countBuilder, "t");
        }

        /// <summary>
        /// 是否分组
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <returns></returns>
        private bool IsGroup(ISqlBuilder builder)
        {
            if (builder is IClauseAccessor accessor)
            {
                return accessor.GroupByClause.IsGroup;
            }

            return false;
        }

        /// <summary>
        /// 获取行数Sql生成器 - 分组
        /// </summary>
        /// <param name="countBuilder"></param>
        /// <returns></returns>
        private ISqlBuilder GetCountBuilderByGroup(ISqlBuilder countBuilder)
        {
            countBuilder.ClearSelect();
            return countBuilder.New().Count().From(countBuilder.AppendSelect("1 As c"), "t");
        }

        /// <summary>
        /// 获取行数Sql生成器
        /// </summary>
        /// <param name="countBuilder">Sql生成器</param>
        /// <returns></returns>
        private ISqlBuilder GetCountBuilder(ISqlBuilder countBuilder)
        {
            countBuilder.ClearSelect();
            return countBuilder.Count();
        }
    }
}
