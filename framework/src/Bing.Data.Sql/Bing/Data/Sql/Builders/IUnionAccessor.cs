using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 联合操作访问器
/// </summary>
public interface IUnionAccessor
{
    /// <summary>
    /// 获取是否包含联合操作
    /// </summary>
    bool IsUnion { get; }

    /// <summary>
    /// 获取联合操作项集合
    /// </summary>
    List<BuilderItem> UnionItems { get; }
}
