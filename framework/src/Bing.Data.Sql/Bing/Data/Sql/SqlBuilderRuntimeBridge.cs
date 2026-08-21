using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// SQL Builder 的受控运行时协作入口。
/// </summary>
internal static class SqlBuilderRuntimeBridge
{
    /// <summary>
    /// 判断 SQL 代码上下文是否包含指定参数标记。
    /// </summary>
    /// <param name="sql">待扫描的 SQL 文本。</param>
    /// <param name="parameterName">包含方言前缀的参数名称。</param>
    /// <returns>代码上下文包含该参数标记时返回 <see langword="true"/>。</returns>
    public static bool ContainsParameterToken(string sql, string parameterName) =>
        SqlBuilderBase.ContainsParameterToken(sql, parameterName);

    /// <summary>
    /// 判断 Builder 是否必须创建独立执行快照。
    /// </summary>
    /// <param name="builder">待检查的 SQL Builder。</param>
    /// <returns>动态过滤或数据边界要求快照时返回 <see langword="true"/>。</returns>
    public static bool RequiresExecutionSnapshot(ISqlBuilder builder) =>
        builder is SqlBuilderBase { RequiresRenderSnapshot: true };

    /// <summary>
    /// 创建 SQL 与参数状态一致的执行快照。
    /// </summary>
    /// <param name="builder">待冻结的 SQL Builder。</param>
    /// <returns>本次执行使用的 SQL 与 Builder 快照。</returns>
    public static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(ISqlBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (builder is SqlBuilderBase sqlBuilder)
        {
            var snapshot = sqlBuilder.CreateRenderSnapshot();
            return new SqlBuilderExecutionSnapshot(snapshot.Sql, snapshot.Builder);
        }
        var clone = builder.Clone();
        return new SqlBuilderExecutionSnapshot(clone.ToSql(), clone);
    }
}