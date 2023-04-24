using Bing.Text;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 名称项，用于拆分列名或表名
/// </summary>
public class NameItemV1
{
    /// <summary>
    /// 初始化一个<see cref="NameItemV1"/>类型的实例
    /// </summary>
    /// <param name="name">名称</param>
    public NameItemV1(string name) => Resolve(name);

    /// <summary>
    /// 解析
    /// </summary>
    /// <param name="name">名称</param>
    private void Resolve(string name)
    {
        name = Filter(name);
        var complex = IsComplex(name);
        var asItem = SplitItem.On(false, name, " ");
        Alias = asItem.Right;
        var dotItem = SplitItem.On(complex, asItem.Left, ".");
        if (string.IsNullOrWhiteSpace(dotItem.Right))
        {
            Name = dotItem.Left;
            return;
        }
        Prefix = dotItem.Left;
        Name = dotItem.Right;
    }

    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="name">名称</param>
    private string Filter(string name)
    {
        name = FilterAs(name);
        return FilterDotSpace(name);
    }

    /// <summary>
    /// 过滤as关键字，替换为空格
    /// </summary>
    /// <param name="name">名称</param>
    private string FilterAs(string name) => name.Replace(" as ", " ", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 过滤.前后空格
    /// </summary>
    /// <param name="name">名称</param>
    private string FilterDotSpace(string name) => name.Replace(@" .", ".").Replace(@". ", ".");

    /// <summary>
    /// 是否复杂名称
    /// </summary>
    /// <param name="name">名称</param>
    private bool IsComplex(string name) => name.Contains("[") || name.Contains("`") || name.Contains("\"");

    /// <summary>
    /// 前缀。范例：a.b As c，值为 a
    /// </summary>
    public string Prefix { get; private set; }

    /// <summary>
    /// 名称。范例：a.b As c，值为 b
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 别名。范例：a.b As c，值为 c
    /// </summary>
    public string Alias { get; private set; }
}
