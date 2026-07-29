namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 组件兼容访问器。
/// </summary>
/// <remarks>
/// 新代码应按职责依赖 <see cref="ISqlCommonPartAccessor"/> 或
/// <see cref="ISqlQueryClauseAccessor"/>。保留此接口以兼容已有 Provider 与调用方。
/// </remarks>
public interface ISqlPartAccessor : ISqlCommonPartAccessor, ISqlQueryClauseAccessor
{
}
