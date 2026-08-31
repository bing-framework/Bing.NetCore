using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// MySql Sql 生成器
/// </summary>
public class MySqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="MySqlBuilder"/>类型的实例
    /// </summary>
    /// <param name="services">SQL Builder 共享服务。</param>
    /// <param name="parameterManager">当前 Builder 的参数管理器。</param>
    public MySqlBuilder(SqlBuilderServices services = null, IParameterManager parameterManager = null)
        : base(MySqlSqlProvider.Instance, services ?? SqlBuilderServices.CreateDefault(), parameterManager)
    {
    }

    /// <inheritdoc />
    /// <returns>使用指定参数管理器创建的 MySQL Builder。</returns>
    protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
        new MySqlBuilder(Services, parameterManager);

    /// <inheritdoc />
    /// <remarks>
    /// MySQL 8 的递归公用表表达式使用 <c>With Recursive</c> 语法；该关键字同样可用于普通 CTE。
    /// </remarks>
    /// <returns>MySQL CTE 使用的关键字文本。</returns>
    protected override string GetCteKeyWord() => "With Recursive";

}
