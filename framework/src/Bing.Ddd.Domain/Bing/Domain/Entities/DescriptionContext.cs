using System.Linq.Expressions;
using System.Text;
using Bing.Expressions;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Domain.Entities;

/// <summary>
/// 在内存中按添加顺序收集并格式化对象描述片段。
/// </summary>
public sealed class DescriptionContext
{
    /// <summary>
    /// 保存当前描述片段的可变字符串缓冲区。
    /// </summary>
    private readonly StringBuilder _stringBuilder;

    /// <summary>
    /// 初始化空的描述上下文。
    /// </summary>
    public DescriptionContext() => _stringBuilder = new StringBuilder();

    /// <summary>
    /// 添加原始描述片段。
    /// </summary>
    /// <param name="description">要追加的描述；空白文本会被忽略。</param>
    public void Add(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;
        _stringBuilder.Append(description);
    }

    /// <summary>
    /// 添加名称和值组成的描述片段。
    /// </summary>
    /// <typeparam name="TValue">属性值类型。</typeparam>
    /// <param name="name">显示名称；空白名称会被忽略。</param>
    /// <param name="value">属性值；空值、默认值或空白字符串会被忽略。</param>
    /// <remarks>有效值以 <c>名称:值,</c> 的形式写入缓冲区，最终输出会移除结尾逗号。</remarks>
    public void Add<TValue>(string name, TValue value)
    {
        if (string.IsNullOrWhiteSpace(name) || value == null || value.Equals(default(TValue)) || string.IsNullOrWhiteSpace(value.ToString()))
            return;
        _stringBuilder.AppendFormat("{0}:{1},", name.Trim(), value);
    }

    /// <summary>
    /// 根据属性表达式的显示名称或描述添加对应描述片段。
    /// </summary>
    /// <typeparam name="T">属性所属对象类型。</typeparam>
    /// <typeparam name="TProperty">属性值类型。</typeparam>
    /// <param name="expression">用于定位属性元数据的成员访问表达式，例如 <c>t => t.Name</c>。</param>
    public void Add<T, TProperty>(Expression<Func<T, TProperty>> expression)
    {
        var member = Lambdas.GetMember(expression);
        var description = Reflection.Reflections.GetDisplayNameOrDescription(member);
        var value = member.GetPropertyValue(this);
        if (Reflection.Reflections.IsBool(member))
            value = Conv.ToBool(value).Description();
        Add(description, value);
    }

    /// <summary>
    /// 清除当前已收集的全部描述片段。
    /// </summary>
    public void FlushCache() => _stringBuilder.Clear();

    /// <summary>
    /// 获取当前格式化后的描述文本。
    /// </summary>
    /// <returns>无描述时返回空字符串；否则返回移除末尾逗号后的描述文本。</returns>
    public string Output()
    {
        if (_stringBuilder.Length == 0)
            return string.Empty;
        return _stringBuilder.ToString().Trim().TrimEnd(',');
    }

    /// <summary>
    /// 返回当前描述文本。
    /// </summary>
    /// <returns><see cref="Output"/> 生成的描述文本。</returns>
    public override string ToString() => Output();
}
