using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 公用表表达式CTE操作访问器
/// </summary>
public interface ICteAccessor
{
    /// <summary>
    /// 获取当前草稿的只读 CTE 视图。
    /// </summary>
    IReadOnlyList<BuilderItem> CteItems { get; }

    /// <summary>追加已准备好的 CTE；普通查询调用者使用 With。</summary>
    /// <param name="item">由组合入口完成快照隔离的 CTE 项。</param>
    void AddCte(BuilderItem item);
}
