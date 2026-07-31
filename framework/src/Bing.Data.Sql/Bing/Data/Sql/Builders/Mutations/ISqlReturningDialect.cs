using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Mutation 返回结果子句的方言约定。
/// </summary>
public interface ISqlReturningDialect
{
    /// <summary>
    /// 返回结果子句在 Mutation 语句中的位置。
    /// </summary>
    SqlReturningClausePosition Position { get; }

    /// <summary>
    /// 获取当前操作使用的返回结果关键字。
    /// </summary>
    /// <param name="executionKind">SQL 执行类型。</param>
    /// <returns>返回结果关键字。</returns>
    string GetKeyword(SqlExecutionKind executionKind);

    /// <summary>
    /// 解析当前操作的返回列限定符。
    /// </summary>
    /// <param name="executionKind">SQL 执行类型。</param>
    /// <param name="configuredQualifier">Fluent API 配置的目标表限定符。</param>
    /// <returns>方言使用的返回列限定符。</returns>
    string GetQualifier(SqlExecutionKind executionKind, string configuredQualifier);
}

/// <summary>
/// Mutation 返回结果子句位置。
/// </summary>
public enum SqlReturningClausePosition
{
    /// <summary>
    /// 位于完整 Mutation 语句末尾。
    /// </summary>
    End = 0,

    /// <summary>
    /// 位于 Mutation 数据来源或筛选子句之前。
    /// </summary>
    BeforeSource = 1
}
