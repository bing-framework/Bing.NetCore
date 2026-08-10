using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// PostgreSql Sql生成器
/// </summary>
public class PostgreSqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="PostgreSqlBuilder"/>类型的实例
    /// </summary>
    /// <param name="services">SQL Builder 共享服务。</param>
    /// <param name="parameterManager">当前 Builder 的参数管理器。</param>
    public PostgreSqlBuilder(SqlBuilderServices services = null, IParameterManager parameterManager = null)
        : base(PostgreSqlSqlProvider.Instance, services ?? SqlBuilderServices.CreateDefault(), parameterManager) { }

    /// <inheritdoc />
    protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
        new PostgreSqlBuilder(Services, parameterManager);

    /// <inheritdoc />
    /// <remarks>
    /// PostgreSQL 递归公用表表达式必须以 <c>With Recursive</c> 开始；该关键字同样兼容非递归 CTE。
    /// </remarks>
    protected override string GetCteKeyWord() => "With Recursive";
}
