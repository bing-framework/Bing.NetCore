using System.Linq.Expressions;
using System.Text;
using Bing.Expressions;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Domain.Entities;

/// <summary>
/// 描述上下文，提供对对象属性和描述信息的管理。
/// </summary>
public sealed class DescriptionContext
{
    /// <summary>
    /// 字符串拼接器
    /// </summary>
    private readonly StringBuilder _stringBuilder;

    /// <summary>
    /// 初始化一个<see cref="DescriptionContext"/>类型的实例
    /// </summary>
    public DescriptionContext() => _stringBuilder = new StringBuilder();

    /// <summary>
    /// 添加描述信息。
    /// </summary>
    /// <param name="description">描述</param>
    public void Add(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;
        _stringBuilder.Append(description);
    }

    /// <summary>
    /// 添加带有名称的描述信息。
    /// </summary>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="name">属性名称</param>
    /// <param name="value">属性值</param>
    public void Add<TValue>(string name, TValue value)
    {
        if (string.IsNullOrWhiteSpace(name) || value == null || value.Equals(default(TValue)) || string.IsNullOrWhiteSpace(value.ToString()))
            return;
        _stringBuilder.AppendFormat("{0}:{1},", name.Trim(), value);
    }

    /// <summary>
    /// 添加基于属性表达式的描述信息。
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="expression">属性表达式，范例：t => t.Name</param>
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
    /// 刷新缓存，清除所有描述信息。
    /// </summary>
    public void FlushCache() => _stringBuilder.Clear();

    /// <summary>
    /// 获取格式化后的描述信息。
    /// </summary>
    /// <returns>格式化的描述字符串</returns>
    public string Output()
    {
        if (_stringBuilder.Length == 0)
            return string.Empty;
        return _stringBuilder.ToString().Trim().TrimEnd(',');
    }

    /// <summary>
    /// 返回描述信息的字符串表示形式。
    /// </summary>
    /// <returns>描述信息字符串</returns>
    public override string ToString() => Output();
}
