using System.Text;
using System.Text.RegularExpressions;
using Bing.Data.Sql.Builders.Extensions;
using Bing.Data.Sql.Metadata;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 表示可包含数据库名、前缀、名称和别名的 SQL 项。
/// </summary>
public class SqlItem
{
    #region 字段

    /// <summary>
    /// 保存 SQL 项名称文本。
    /// </summary>
    private string _name;

    /// <summary>
    /// 前缀
    /// </summary>
    private string _prefix;

    /// <summary>
    /// 别名
    /// </summary>
    private string _alias;

    #endregion

    #region 属性

    /// <summary>
    /// 是否使用原始值
    /// </summary>
    public bool IsRaw { get; }

    /// <summary>
    /// 前缀，范例：t.a As b，值为 t
    /// </summary>
    public string Prefix => _prefix.SafeString();

    /// <summary>
    /// 获取 SQL 项名称；原始项保留原始文本，结构化项读取时规范化空值。
    /// </summary>
    public string Name => IsRaw ? _name : _name.SafeString();

    /// <summary>
    /// 别名，范例：t.a As b，值为 b
    /// </summary>
    public string Alias => _alias.SafeString();

    /// <summary>
    /// 数据库名称
    /// </summary>
    public string DatabaseName { get; private set; }

    #endregion

    #region 工厂方法

    /// <summary>
    /// 解析包含别名或限定段的结构化 SQL 项。
    /// </summary>
    /// <param name="name">名称。</param>
    /// <param name="prefix">前缀。</param>
    /// <param name="alias">别名。</param>
    /// <returns>已解析的 SQL 项。</returns>
    public static SqlItem Parse(string name, string prefix = null, string alias = null) => new(name, prefix, alias);

    /// <summary>
    /// 创建不拆分句点的原子 SQL 标识符项。
    /// </summary>
    /// <param name="name">名称。</param>
    /// <param name="prefix">前缀。</param>
    /// <param name="alias">别名。</param>
    /// <returns>原子 SQL 项。</returns>
    public static SqlItem Atomic(string name, string prefix = null, string alias = null) =>
        new(name, prefix, alias, isSplit: false);

    /// <summary>
    /// 创建不进行标识符解析的原始 SQL 项。
    /// </summary>
    /// <param name="sql">原始 SQL。</param>
    /// <param name="alias">别名。</param>
    /// <returns>原始 SQL 项。</returns>
    public static SqlItem Raw(string sql, string alias = null) => new(sql, alias: alias, raw: true);

    /// <summary>
    /// 创建保留调用方名称文本的未解析 SQL 项。
    /// </summary>
    /// <param name="name">名称。</param>
    /// <param name="prefix">前缀。</param>
    /// <param name="alias">别名。</param>
    /// <returns>未解析 SQL 项。</returns>
    public static SqlItem Unresolved(string name, string prefix = null, string alias = null) =>
        new(name, prefix, alias, isSplit: false, isResolve: false);

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个 <see cref="SqlItem"/> 类型的实例。
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="prefix">前缀</param>
    /// <param name="alias">别名</param>
    /// <param name="raw">是否使用原始值</param>
    /// <param name="isSplit">是否用句点分割名称</param>
    /// <param name="isResolve">是否解析名称</param>
    internal SqlItem(string name, string prefix = null, string alias = null, bool raw = false, bool isSplit = true,
        bool isResolve = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        _prefix = prefix;
        _alias = alias;
        IsRaw = raw;
        if (raw)
        {
            _name = name;
            return;
        }
        Resolve(name, isSplit, isResolve);
    }

    /// <summary>
    /// 设置别名，返回前缀和名称
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="isSplit">是否用句点分割名称</param>
    /// <param name="isResolve">是否解析名称</param>
    private void Resolve(string name, bool isSplit, bool isResolve)
    {
        name = name.Trim();
        if (isResolve == false)
        {
            _name = name;
            return;
        }
        var pattern = @"\s+[aA][sS]\s+";
        name = Regex.Replace(name, pattern, " ");
        if (name.Contains("."))
        {
            pattern = @"\s+.\s+";
            name = Regex.Replace(name, pattern, ".");
        }
        var list = name.Split(' ').Where(t => t.IsEmpty() == false).ToList();
        if (list.Count == 0)
            return;
        if (list.Count == 2)
            _alias = list[1].Trim();
        if (isSplit)
        {
            SplitName(list[0]);
            return;
        }
        _name = name;
    }

    /// <summary>
    /// 分割名称
    /// </summary>
    /// <param name="name">名称</param>
    private void SplitName(string name)
    {
        var result = new NameItem(name);
        if (string.IsNullOrWhiteSpace(result.Name) == false)
            _name = result.Name;
        if (string.IsNullOrWhiteSpace(result.Prefix) == false)
            _prefix = result.Prefix;
        if (string.IsNullOrWhiteSpace(result.DatabaseName) == false)
            DatabaseName = result.DatabaseName;
    }

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns>当前 SQL 项的独立副本。</returns>
    public virtual SqlItem Clone()
    {
        var result = new SqlItem(Name, Prefix, Alias, IsRaw, false, false)
        {
            DatabaseName = DatabaseName
        };
        return result;
    }

    #endregion

    #region ToSql(获取Sql)

    /// <summary>
    /// 获取Sql
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <returns>按指定 SQL 方言渲染的 SQL；名称为空时返回 <see langword="null"/>。</returns>
    public virtual string ToSql(IDialect dialect = null)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return null;
        if (IsRaw)
            return Name;
        var column = GetColumn(dialect);
        var columnAlias = GetSafeName(dialect, Alias);
        return dialect.GetColumn(column, columnAlias);
    }

    /// <summary>
    /// 获取列
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <returns>按指定 SQL 方言转义的数据库名、前缀和名称组合。</returns>
    protected string GetColumn(IDialect dialect)
    {
        var result = new StringBuilder();
        var database = DatabaseName;
        if (string.IsNullOrWhiteSpace(database) == false)
            result.Append($"{GetSafeName(dialect, database)}.");
        if (string.IsNullOrWhiteSpace(Prefix) == false)
            result.Append($"{GetSafeName(dialect, Prefix)}.");
        result.Append(GetSafeName(dialect, Name));
        return result.ToString();
    }

    /// <summary>
    /// 获取名称
    /// </summary>
    /// <returns>由前缀和名称组成的未转义文本。</returns>
    protected string GetName() => string.IsNullOrWhiteSpace(Prefix) ? Name : $"{Prefix}.{Name}";

    /// <summary>
    /// 获取安全名称
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="name">名称</param>
    /// <returns>按指定 SQL 方言转义的名称。</returns>
    protected string GetSafeName(IDialect dialect, string name) => dialect.GetSafeName(name);

    #endregion
}