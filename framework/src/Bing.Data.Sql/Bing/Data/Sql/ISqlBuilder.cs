using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql生成器
/// </summary>
/// <remarks>
/// 实例包含子句、参数、别名、分页和联合查询等可变状态，不支持并发读写。每个并发操作应使用独立的 <see cref="Clone"/> 或 <see cref="New"/> 实例；共享实例时调用方必须自行同步。
/// </remarks>
public interface ISqlBuilder : ICondition, ISqlContent, ISqlOperation, ISqlQueryClauseAccessor,
    IInsertClauseAccessor, IUpdateClauseAccessor, IDeleteClauseAccessor, IDeleteUsingClauseAccessor, ISqlMutationContextAccessor,
    IAllowAllRowsMutationBuilder
{
    /// <summary>
    /// 获取生成当前 SQL 的 Provider。
    /// </summary>
    /// <remarks>
    /// Mutation 描述会冻结该 Provider 的身份和能力档案，第三方 Builder 必须返回实际 Provider，不能返回
    /// <see langword="null"/>。
    /// </remarks>
    ISqlProvider Provider { get; }

    /// <summary>
    /// 当前 SQL 操作类型。
    /// </summary>
    SqlOperationKind OperationKind { get; }

    /// <summary>
    /// 分页参数
    /// </summary>
    IPager Pager { get; }

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns>当前 SQL 生成器的独立副本。</returns>
    ISqlBuilder Clone();

    /// <summary>
    /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
    /// </summary>
    /// <returns>将参数替换为参数值后的调试 SQL。</returns>
    string ToDebugSql();

    /// <summary>
    /// 根据已生成的Sql语句生成调试Sql语句，Sql语句中的参数被替换为参数值
    /// </summary>
    /// <param name="sql">已生成的Sql语句</param>
    /// <returns>将参数替换为参数值后的调试 SQL。</returns>
    string ToDebugSql(string sql);

    /// <summary>
    /// 生成Sql语句
    /// </summary>
    /// <returns>生成的 SQL。</returns>
    string ToSql();

    /// <summary>
    /// 创建Sql生成器
    /// </summary>
    /// <returns>已创建并初始化的 SQL 生成器。</returns>
    ISqlBuilder New();

    /// <summary>
    /// 清空并初始化
    /// </summary>
    /// <returns>清空并初始化后的当前 SQL 生成器。</returns>
    ISqlBuilder Clear();

    /// <summary>
    /// 清空Select子句
    /// </summary>
    /// <returns>清空 Select 子句后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearSelect();

    /// <summary>
    /// 清空From子句
    /// </summary>
    /// <returns>清空 From 子句后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearFrom();

    /// <summary>
    /// 清空Join子句
    /// </summary>
    /// <returns>清空 Join 子句后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearJoin();

    /// <summary>
    /// 清空Where子句
    /// </summary>
    /// <returns>清空 Where 子句后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearWhere();

    /// <summary>
    /// 清空GroupBy子句
    /// </summary>
    /// <returns>清空 GroupBy 子句后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearGroupBy();

    /// <summary>
    /// 清空OrderBy子句
    /// </summary>
    /// <returns>清空 OrderBy 子句后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearOrderBy();

    /// <summary>
    /// 清空Sql参数
    /// </summary>
    /// <returns>清空 SQL 参数后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearSqlParams();

    /// <summary>
    /// 清空分页参数
    /// </summary>
    /// <returns>清空分页参数后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearPageParams();

    /// <summary>
    /// 清空联合操作项
    /// </summary>
    /// <returns>清空联合操作项后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearUnionBuilders();

    /// <summary>
    /// 清空公用表表达式
    /// </summary>
    /// <returns>清空公用表表达式后的当前 SQL 生成器。</returns>
    ISqlBuilder ClearCte();

    /// <summary>
    /// 设置分页
    /// </summary>
    /// <param name="pager">分页参数</param>
    /// <returns>设置分页后的当前 SQL 生成器。</returns>
    ISqlBuilder Page(IPager pager);

    /// <summary>
    /// 设置跳过行数
    /// </summary>
    /// <param name="count">跳过的行数</param>
    /// <returns>设置跳过行数后的当前 SQL 生成器。</returns>
    ISqlBuilder Skip(int count);

    /// <summary>
    /// 设置获取行数
    /// </summary>
    /// <param name="count">获取的行数</param>
    /// <returns>设置获取行数后的当前 SQL 生成器。</returns>
    ISqlBuilder Take(int count);

}
