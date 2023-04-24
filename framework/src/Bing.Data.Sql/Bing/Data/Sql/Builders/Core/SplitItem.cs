using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 拆分项
/// </summary>
public class SplitItem
{
    /// <summary>
    /// 初始化一个<see cref="SplitItem"/>类型的实例
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="separator">分割符</param>
    public SplitItem(string value, string separator)
    {
        if (string.IsNullOrEmpty(value))
            return;
        value = value.Trim();
        var list = ResolveBySeparator(value, separator);
        Init(list);
    }

    /// <summary>
    /// 初始化一个<see cref="SplitItem"/>类型的实例
    /// </summary>
    /// <param name="value">值</param>
    public SplitItem(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        value = value.Trim();
        var list = ResolveByPattern(value);
        Init(list);
    }

    /// <summary>
    /// 通过分隔符进行解析
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="separator">分隔符</param>
    private List<string> ResolveBySeparator(string value, string separator) => value.Split(separator, true).ToList();

    /// <summary>
    /// 通过正则表达式进行解析
    /// </summary>
    /// <param name="value">值</param>
    private List<string> ResolveByPattern(string value)
    {
        var pattern = "^(([\\[`\"]\\S+?[\\]`\"]).)?(([\\[`\"]\\S+[\\]`\"]).)?([\\[`\"]\\S+[\\]`\"])$";
        var list = Regexs.GetValues(value, pattern, new[] { "$1", "$2", "$3", "$4", "$5" }).Select(t => t.Value).ToList();
        return list.Where(t => string.IsNullOrWhiteSpace(t) == false && t.EndsWith(".") == false).ToList();
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="list">数组</param>
    private void Init(List<string> list)
    {
        if (list.Count == 1)
        {
            Left = list[0];
            return;
        }

        if (list.Count == 2)
        {
            Left = list[0];
            Right = list[1];
        }

        if (list.Count == 3)
        {
            // 数据库名称抛弃
            Left = list[1];
            Right = list[2];
        }
    }

    /// <summary>
    /// 左拆分值
    /// </summary>
    public string Left { get; set; }

    /// <summary>
    /// 右拆分值
    /// </summary>
    public string Right { get; set; }

    /// <summary>
    /// On操作符
    /// </summary>
    /// <param name="complex">是否复杂名称</param>
    /// <param name="value">值</param>
    /// <param name="separator">分隔符</param>
    public static SplitItem On(bool complex, string value, string separator = " ") => complex ? new SplitItem(value) : new SplitItem(value, separator);
}
