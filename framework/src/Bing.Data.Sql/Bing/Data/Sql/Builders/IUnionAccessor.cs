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
    /// 获取当前草稿的只读集合操作视图。
    /// </summary>
    IReadOnlyList<BuilderItem> UnionItems { get; }

    /// <summary>追加已准备好的集合分支；普通查询调用者使用 Union 等组合方法。</summary>
    /// <param name="item">由组合入口完成快照隔离的集合操作项。</param>
    void AddUnion(BuilderItem item);
}
