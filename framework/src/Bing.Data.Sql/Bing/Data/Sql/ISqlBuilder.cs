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
    ISqlBuilder Clone();

    /// <summary>
    /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
    /// </summary>
    string ToDebugSql();

    /// <summary>
    /// 根据已生成的Sql语句生成调试Sql语句，Sql语句中的参数被替换为参数值
    /// </summary>
    /// <param name="sql">已生成的Sql语句</param>
    string ToDebugSql(string sql);

    /// <summary>
    /// 生成Sql语句
    /// </summary>
    string ToSql();

    /// <summary>
    /// 创建Sql生成器
    /// </summary>
    ISqlBuilder New();

    /// <summary>
    /// 清空并初始化
    /// </summary>
    ISqlBuilder Clear();

    /// <summary>
    /// 清空Select子句
    /// </summary>
    ISqlBuilder ClearSelect();

    /// <summary>
    /// 清空From子句
    /// </summary>
    ISqlBuilder ClearFrom();

    /// <summary>
    /// 清空Join子句
    /// </summary>
    ISqlBuilder ClearJoin();

    /// <summary>
    /// 清空Where子句
    /// </summary>
    ISqlBuilder ClearWhere();

    /// <summary>
    /// 清空GroupBy子句
    /// </summary>
    ISqlBuilder ClearGroupBy();

    /// <summary>
    /// 清空OrderBy子句
    /// </summary>
    ISqlBuilder ClearOrderBy();

    /// <summary>
    /// 清空Sql参数
    /// </summary>
    ISqlBuilder ClearSqlParams();

    /// <summary>
    /// 清空分页参数
    /// </summary>
    ISqlBuilder ClearPageParams();

    /// <summary>
    /// 清空联合操作项
    /// </summary>
    ISqlBuilder ClearUnionBuilders();

    /// <summary>
    /// 清空公用表表达式
    /// </summary>
    ISqlBuilder ClearCte();

    /// <summary>
    /// 设置分页
    /// </summary>
    /// <param name="pager">分页参数</param>
    ISqlBuilder Page(IPager pager);

    /// <summary>
    /// 设置跳过行数
    /// </summary>
    /// <param name="count">跳过的行数</param>
    ISqlBuilder Skip(int count);

    /// <summary>
    /// 设置获取行数
    /// </summary>
    /// <param name="count">获取的行数</param>
    ISqlBuilder Take(int count);

}
