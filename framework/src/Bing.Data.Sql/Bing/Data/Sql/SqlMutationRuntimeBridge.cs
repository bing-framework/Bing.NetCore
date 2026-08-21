using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 写入运行时协作入口。
/// </summary>
internal static class SqlMutationRuntimeBridge
{
    /// <summary>
    /// 判断批量 Update 是否必须使用结构化逐实体执行以应用数据边界。
    /// </summary>
    /// <param name="builder">已配置目标表的 Update Builder。</param>
    /// <param name="target">结构化更新目标。</param>
    /// <returns>存在启用的数据边界时返回 <see langword="true"/>。</returns>
    public static bool RequiresStructuredUpdate(ISqlUpdateBuilder builder, SqlTableReference target)
    {
        if (builder is not SqlUpdateBuilder updateBuilder)
            throw new NotSupportedException("当前 Update Builder 不支持运行时数据边界探测。");
        return SqlMutationDataBoundary.RequiresStructuredUpdate(updateBuilder.MutationContext, target);
    }
}