using System.Text;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 表项
/// </summary>
public class TableItem
{
    /// <summary>
    /// Sql方言
    /// </summary>
    private readonly IDialect _dialect;

    /// <summary>
    /// 初始化一个<see cref="TableItem"/>类型的实例
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="name">表名</param>
    public TableItem(IDialect dialect, string name)
    {
        _dialect = dialect;
        Resolve(name);
    }

    /// <summary>
    /// 解析
    /// </summary>
    private void Resolve(string name)
    {
        var item = new NameItemV1(name);
        Schema = item.Prefix;
        Name = item.Name;
        TableAlias = item.Alias;
    }

    /// <summary>
    /// 架构名
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 表别名
    /// </summary>
    public string TableAlias { get; set; }

    /// <summary>
    /// 验证是否有效
    /// </summary>
    public bool Validate() => !string.IsNullOrWhiteSpace(Name);

    /// <summary>
    /// 获取结果
    /// </summary>
    public string ToResult()
    {
        var builder = new StringBuilder();
        AppendTo(builder);
        return builder.ToString();
    }

    /// <summary>
    /// 添加到字符串生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    public virtual void AppendTo(StringBuilder builder)
    {
        AppendSchema(builder);
        AppendTable(builder);
        AppendTableAlias(builder);
    }

    /// <summary>
    /// 添加架构名
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    private void AppendSchema(StringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(Schema))
            return;
        builder.AppendFormat("{0}.", _dialect.GetSafeName(Schema));
    }

    /// <summary>
    /// 添加表名
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    private void AppendTable(StringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return;
        builder.AppendFormat(_dialect.GetSafeName(Name));
    }

    /// <summary>
    /// 添加表别名
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    private void AppendTableAlias(StringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(TableAlias))
            return;
        builder.AppendFormat(" As {0}", _dialect.GetSafeName(TableAlias));
    }
}
