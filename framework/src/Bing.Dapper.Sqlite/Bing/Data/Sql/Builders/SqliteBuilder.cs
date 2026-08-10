using System.Text;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sqlite Sql生成器
/// </summary>
public class SqliteBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="SqliteBuilder"/>类型的实例
    /// </summary>
    /// <param name="services">SQL Builder 共享服务。</param>
    /// <param name="parameterManager">当前 Builder 的参数管理器。</param>
    public SqliteBuilder(SqlBuilderServices services = null, IParameterManager parameterManager = null)
        : base(SqliteSqlProvider.Instance, services ?? SqlBuilderServices.CreateDefault(), parameterManager)
    {
    }

    /// <inheritdoc />
    protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
        new SqliteBuilder(Services, parameterManager);

    /// <inheritdoc />
    /// <remarks>
    /// SQLite 的递归公用表表达式使用 <c>With Recursive</c> 语法；该关键字同样可用于普通 CTE。
    /// </remarks>
    protected override string GetCteKeyWord() => "With Recursive";

    /// <summary>
    /// 创建 SQLite 联合查询 SQL。
    /// </summary>
    /// <param name="result">承载最终 SQL 的字符串构建器。</param>
    /// <remarks>
    /// SQLite 不支持将复合 SELECT 的每个操作数包裹在括号中，因此保留基类的参数合并流程但省略操作数括号。
    /// </remarks>
    protected override void CreateSqlByUnion(StringBuilder result)
    {
        AppendSelect(result);
        AppendFrom(result);
        AppendClause(result, JoinClause);
        AppendClause(result, WhereClause);
        AppendClause(result, GroupByClause);
        foreach (var operation in UnionItems)
        {
            AppendSql(result, operation.Name);
            AppendSql(result, RenderSubquery(operation.Builder));
        }
        AppendClause(result, OrderByClause);
        AppendLimit(result);
    }
}
