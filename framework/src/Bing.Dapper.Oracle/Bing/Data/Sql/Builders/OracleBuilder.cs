using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Oracle Sql生成器
/// </summary>
public class OracleBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="OracleBuilder"/>类型的实例
    /// </summary>
    /// <param name="services">SQL Builder 共享服务。</param>
    /// <param name="parameterManager">当前 Builder 的参数管理器。</param>
    public OracleBuilder(SqlBuilderServices services = null, IParameterManager parameterManager = null)
        : base(OracleSqlProvider.Instance, services ?? SqlBuilderServices.CreateDefault(), parameterManager)
    {
    }

    /// <inheritdoc />
    /// <returns>使用指定参数管理器创建的 Oracle Builder。</returns>
    protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
        new OracleBuilder(Services, parameterManager);

}
