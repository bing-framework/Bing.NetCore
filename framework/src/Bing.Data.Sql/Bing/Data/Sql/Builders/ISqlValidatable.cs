namespace Bing.Data.Sql.Builders;

/// <summary>
/// 支持 SQL 结构验证的组件。
/// </summary>
public interface ISqlValidatable
{
    /// <summary>
    /// 验证当前 SQL 结构。
    /// </summary>
    /// <param name="context">验证使用的 Provider、参数和执行上下文。</param>
    void Validate(SqlValidationContext context);
}