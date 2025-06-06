﻿using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql生成器
/// </summary>
public interface ISqlBuilder : ICondition, ISqlContent, ISqlOperation
{
    /// <summary>
    /// 分页参数
    /// </summary>
    IPager Pager { get; }

    /// <summary>
    /// 克隆
    /// </summary>
    ISqlBuilder Clone();

    /// <summary>
    /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
    /// </summary>
    string ToDebugSql();

    /// <summary>
    /// 生成Sql语句
    /// </summary>
    string ToSql();

    /// <summary>
    /// 创建Sql生成器
    /// </summary>
    ISqlBuilder New();

    /// <summary>
    /// 清空并初始化
    /// </summary>
    ISqlBuilder Clear();

    /// <summary>
    /// 清空Select子句
    /// </summary>
    ISqlBuilder ClearSelect();

    /// <summary>
    /// 清空From子句
    /// </summary>
    ISqlBuilder ClearFrom();

    /// <summary>
    /// 清空Join子句
    /// </summary>
    ISqlBuilder ClearJoin();

    /// <summary>
    /// 清空Where子句
    /// </summary>
    ISqlBuilder ClearWhere();

    /// <summary>
    /// 清空GroupBy子句
    /// </summary>
    ISqlBuilder ClearGroupBy();

    /// <summary>
    /// 清空OrderBy子句
    /// </summary>
    ISqlBuilder ClearOrderBy();

    /// <summary>
    /// 清空Sql参数
    /// </summary>
    ISqlBuilder ClearSqlParams();

    /// <summary>
    /// 清空分页参数
    /// </summary>
    ISqlBuilder ClearPageParams();

    /// <summary>
    /// 清空联合操作项
    /// </summary>
    ISqlBuilder ClearUnionBuilders();

    /// <summary>
    /// 清空公用表表达式
    /// </summary>
    ISqlBuilder ClearCte();

    /// <summary>
    /// 设置分页
    /// </summary>
    /// <param name="pager">分页参数</param>
    ISqlBuilder Page(IPager pager);

    /// <summary>
    /// 设置跳过行数
    /// </summary>
    /// <param name="count">跳过的行数</param>
    ISqlBuilder Skip(int count);

    /// <summary>
    /// 设置获取行数
    /// </summary>
    /// <param name="count">获取的行数</param>
    ISqlBuilder Take(int count);

    /// <summary>
    /// 忽略过滤器
    /// </summary>
    /// <typeparam name="TSqlFilter">Sql过滤器类型</typeparam>
    ISqlBuilder IgnoreFilter<TSqlFilter>() where TSqlFilter : ISqlFilter;
}
