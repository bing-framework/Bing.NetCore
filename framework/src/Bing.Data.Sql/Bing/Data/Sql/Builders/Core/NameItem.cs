using Bing.Data.Sql.Metadata;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 表示可拆分数据库名、对象前缀和对象名称的结构化名称项。
/// </summary>
public class NameItem
{
    #region 属性

    /// <summary>
    /// 获取或设置数据库名称部分。
    /// </summary>
    public string DatabaseName { get; private set; }

    /// <summary>
    /// 保存对象名称的前缀部分。
    /// </summary>
    private string _prefix;

    /// <summary>
    /// 获取或设置对象名称的前缀；读取时空值按空字符串处理。
    /// </summary>
    public string Prefix
    {
        get => _prefix.SafeString();
        set => _prefix = value;
    }

    /// <summary>
    /// 保存对象名称部分。
    /// </summary>
    private string _name;

    /// <summary>
    /// 获取或设置对象名称；读取时空值按空字符串处理。
    /// </summary>
    public string Name
    {
        get => _name.SafeString();
        set => _name = value;
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 解析指定的数据库对象名称并初始化结构化名称项。
    /// </summary>
    /// <param name="name">包含数据库名、前缀和对象名的名称文本。</param>
    public NameItem(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var list = IsComplex(name) ? ResolveByPattern(name) : ResolveBySplit(name);
        if (list.Count == 1)
        {
            Name = list[0];
            return;
        }
        if (list.Count == 2)
        {
            Prefix = list[0];
            Name = list[1];
        }
        if (list.Count == 3)
        {
            DatabaseName = list[0];
            Prefix = list[1];
            Name = list[2];
        }
    }

    /// <summary>
    /// 判断名称是否包含需要特殊解析的引用符号。
    /// </summary>
    /// <param name="name">待判断的名称文本。</param>
    /// <returns>包含方括号、反引号或双引号时返回 <see langword="true"/>。</returns>
    private bool IsComplex(string name) => name.Contains("[") || name.Contains("`") || name.Contains("\"");

    /// <summary>
    /// 按句点拆分未引用的名称段。
    /// </summary>
    /// <param name="name">待拆分的名称文本。</param>
    /// <returns>拆分后的名称段。</returns>
    private List<string> ResolveBySplit(string name) => name.Split('.').ToList();

    /// <summary>
    /// 通过正则表达式进行解析
    /// </summary>
    /// <param name="name">名称</param>
    /// <returns>按引用符号解析出的名称段。</returns>
    private List<string> ResolveByPattern(string name)
    {
        var pattern = "^(([\\[`\"]\\S+?[\\]`\"]).)?(([\\[`\"]\\S+[\\]`\"]).)?([\\[`\"]\\S+[\\]`\"])$";
        var list = Regexs.GetValues(name, pattern, new[] { "$1", "$2", "$3", "$4", "$5" }).Select(t => t.Value).ToList();
        return list.Where(t => string.IsNullOrWhiteSpace(t) == false && t.EndsWith(".") == false).ToList();
    }

    #endregion

    #region ToSql(获取Sql)

    /// <summary>
    /// 获取Sql
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="prefix">前缀</param>
    /// <returns>按指定 SQL 方言渲染的完整对象名称。</returns>
    public string ToSql(IDialect dialect, string prefix = null)
    {
        var name = GetName(dialect, prefix);
        var database = GetDatabase(dialect);
        return string.IsNullOrWhiteSpace(database) ? name : $"{database}.{name}";
    }

    /// <summary>
    /// 获取前缀
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <returns>当前对象使用的前缀；未设置对象前缀时返回传入的前缀。</returns>
    private string GetPrefix(string prefix) => string.IsNullOrWhiteSpace(Prefix) ? prefix : Prefix;

    /// <summary>
    /// 获取前缀
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <returns>按 SQL 方言转义后的数据库名称；未设置数据库名称时返回 <see langword="null"/>。</returns>
    private string GetDatabase(IDialect dialect)
    {
        if (string.IsNullOrWhiteSpace(DatabaseName) == false)
            return dialect.SafeName(DatabaseName);
        return null;
    }

    /// <summary>
    /// 获取名称
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="prefix">前缀</param>
    /// <returns>按 SQL 方言转义后的对象名称及其前缀。</returns>
    private string GetName(IDialect dialect, string prefix)
    {
        prefix = GetPrefix(prefix);
        return string.IsNullOrWhiteSpace(prefix) ? dialect.SafeName(Name) : $"{dialect.SafeName(prefix)}.{dialect.SafeName(Name)}";
    }

    /// <summary>
    /// 获取名称
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <returns>未转义的对象名称及其前缀。</returns>
    private string GetName(string prefix)
    {
        prefix = GetPrefix(prefix);
        return string.IsNullOrWhiteSpace(prefix) ? Name : $"{prefix}.{Name}";
    }

    #endregion
}