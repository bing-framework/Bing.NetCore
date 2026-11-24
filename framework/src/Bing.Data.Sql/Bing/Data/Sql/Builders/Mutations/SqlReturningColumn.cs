using System.ComponentModel;
using System.Text;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Mutation Returning 的结构化返回列。
/// </summary>
public sealed class SqlReturningColumn
{
    // 直接列保存 string，计算列保存已复制的 string[]，普通列无需多占一个引用字段。
    private readonly object _value;

    /// <summary>
    /// 初始化一个 <see cref="SqlReturningColumn"/> 类型的实例。
    /// </summary>
    /// <param name="column">物理列名。</param>
    /// <param name="qualifier">可选目标表别名。</param>
    /// <param name="alias">可选结果列别名。</param>
    public SqlReturningColumn(string column, string qualifier = null, string alias = null)
    {
        _value = string.IsNullOrWhiteSpace(column)
            ? throw new ArgumentException("Returning 列名不能为空。", nameof(column))
            : column;
        Qualifier = qualifier;
        Alias = alias;
    }

    /// <summary>
    /// 获取直接投影的物理列名；计算投影返回 null，应通过 AppendTo 渲染。
    /// </summary>
    public string Column => _value as string;

    /// <summary>
    /// 获取可选目标表别名。
    /// </summary>
    public string Qualifier { get; }

    /// <summary>
    /// 获取可选结果列别名。
    /// </summary>
    public string Alias { get; }

    private SqlReturningColumn(string[] parts, string qualifier, string alias)
    {
        _value = parts;
        Qualifier = qualifier;
        Alias = alias;
    }

    /// <summary>供生成代码创建不可变的列运算投影，不作为独立的业务表达式 API。</summary>
    /// <param name="parts">固定运算符文本与物理列名交替排列，首尾为文本。</param>
    /// <param name="qualifier">可选目标表别名。</param>
    /// <param name="alias">可选结果列别名。</param>
    /// <returns>已复制输入片段的不可变返回列。</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static SqlReturningColumn CreateComputed(IReadOnlyList<string> parts,
        string qualifier = null, string alias = null) => CreateArithmetic(parts, qualifier, alias);

    /// <summary>供生成代码创建含数值负号或四则运算的投影，同时标识运行库支持一元运算。</summary>
    /// <param name="parts">运算符文本与物理列名交替排列，首尾为文本。</param>
    /// <param name="qualifier">可选目标表别名。</param>
    /// <param name="alias">可选结果列别名。</param>
    /// <returns>已复制输入片段的不可变返回列。</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static SqlReturningColumn CreateArithmetic(IReadOnlyList<string> parts,
        string qualifier = null, string alias = null)
    {
        if (parts == null) throw new ArgumentNullException(nameof(parts));
        if (parts.Count < 3 || parts.Count % 2 == 0)
            throw new ArgumentException("计算投影必须交替包含运算符文本和列名。", nameof(parts));
        var copy = new string[parts.Count];
        var expectsOperand = true;
        var depth = 0;
        var operators = 0;
        for (var i = 0; i < copy.Length; i++)
        {
            var value = parts[i];
            if (i % 2 != 0)
            {
                if (!expectsOperand || string.IsNullOrWhiteSpace(value)) Invalid();
                expectsOperand = false;
            }
            else
            {
                if (value == null) Invalid();
                for (var j = 0; j < value.Length; j++)
                {
                    var character = value[j];
                    if (character == ' ') continue;
                    if (character == '(' && expectsOperand) depth++;
                    else if (character == ')' && !expectsOperand && depth > 0) depth--;
                    else if (character is '+' or '-' or '*' or '/')
                    {
                        // 负号可以出现在操作数之前；连续 -- 是 SQL 注释，不能作为算术文本接受。
                        if (expectsOperand && character != '-' ||
                            character == '-' && j > 0 && value[j - 1] == '-') Invalid();
                        operators++;
                        expectsOperand = true;
                    }
                    else Invalid();
                }
            }
            copy[i] = value;
        }
        if (expectsOperand || depth != 0 || operators == 0) Invalid();
        return new SqlReturningColumn(copy, qualifier, alias);

        static void Invalid() => throw new ArgumentException("计算投影仅支持列名、数值负号和完整的四则运算。", nameof(parts));
    }

    /// <summary>按当前方言渲染直接列或计算列，供默认及自定义 Returning 子句复用。</summary>
    /// <param name="builder">目标 SQL 缓冲区。</param>
    /// <param name="dialect">实际执行方言。</param>
    /// <param name="qualifier">Provider 决定的限定符；未提供时使用列自身的限定符。</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AppendTo(StringBuilder builder, IDialect dialect, string qualifier = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (dialect == null) throw new ArgumentNullException(nameof(dialect));
        qualifier ??= Qualifier;
        var prefix = string.IsNullOrWhiteSpace(qualifier) ? null : dialect.SafeName(qualifier);
        void AppendColumn(string column)
        {
            if (prefix != null) builder.Append(prefix).Append('.');
            builder.Append(dialect.SafeName(column));
        }
        if (_value is string column) AppendColumn(column);
        else
        {
            var parts = (string[])_value;
            for (var i = 0; i < parts.Length; i++)
                if (i % 2 == 0) builder.Append(parts[i]);
                else AppendColumn(parts[i]);
        }
        if (string.IsNullOrWhiteSpace(Alias) == false)
            builder.Append(" As ").Append(dialect.SafeName(Alias));
    }
}
