﻿using Bing.Extensions;
using Bing.Text;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 排序项
/// </summary>
public class OrderByItem
{
    /// <summary>
    /// 排序列
    /// </summary>
    public string Order { get; }

    /// <summary>
    /// 排序列，不带前缀
    /// </summary>
    public string Column { get; }

    /// <summary>
    /// 是否降序
    /// </summary>
    public bool Desc { get; }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// 是否使用原始值
    /// </summary>
    public bool Raw { get; }

    /// <summary>
    /// 前缀
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// 初始化一个<see cref="OrderByItem"/>类型的实例
    /// </summary>
    /// <param name="order">排序列</param>
    /// <param name="desc">是否降序</param>
    /// <param name="type">实体类型</param>
    /// <param name="raw">是否使用原始值</param>
    /// <param name="prefix">前缀</param>
    public OrderByItem(string order, bool desc = false, Type type = null, bool raw = false, string prefix = null)
    {
        Order = order.SafeString();
        Desc = desc;
        Type = type;
        Raw = raw;
        if (raw)
            return;
        Order = Order.TrimPhraseEnd("asc");
        if (Order.ToLower().EndsWith("desc"))
        {
            Desc = true;
            Order = Order.TrimPhraseEnd("desc");
        }

        var item = new NameItem(Order);
        Column = item.Name;
        Prefix = string.IsNullOrWhiteSpace(item.Prefix) ? prefix : item.Prefix;
    }

    /// <summary>
    /// 获取Sql
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="register">实体别名注册器</param>
    public string ToSql(IDialect dialect, IEntityAliasRegister register)
    {
        if (Raw)
            return Order;
        var name = new NameItem(Order);
        return $"{name.ToSql(dialect, GetPrefix(register))} {(Desc ? "Desc" : null)}".TrimEnd();
    }

    /// <summary>
    /// 获取前缀
    /// </summary>
    /// <param name="register">实体别名注册器</param>
    private string GetPrefix(IEntityAliasRegister register)
    {
        if (string.IsNullOrWhiteSpace(Prefix) == false)
            return Prefix;
        return register.GetAlias(Type);
    }
}
