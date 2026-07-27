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
    protected override string GetCteKeyWord() => "With Recursive";
}
