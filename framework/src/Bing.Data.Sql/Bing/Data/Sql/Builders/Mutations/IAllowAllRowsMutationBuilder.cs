namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 支持显式允许全表写操作的 Mutation Builder。
/// </summary>
public interface IAllowAllRowsMutationBuilder
{
    /// <summary>
    /// 设置是否允许全表写操作。
    /// </summary>
    /// <param name="allowAllRows">允许时为 true。</param>
    void SetAllowAllRows(bool allowAllRows);
}