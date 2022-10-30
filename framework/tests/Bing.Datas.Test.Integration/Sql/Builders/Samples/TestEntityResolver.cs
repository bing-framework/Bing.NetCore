using System;
using System.Linq;
using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Test.Integration.Samples;
using Bing.Expressions;
using Bing.Extensions;

namespace Bing.Data.Test.Integration.Sql.Builders.Samples;

/// <summary>
/// 实体解析器
/// </summary>
public class TestEntityResolver : IEntityResolver
{
    /// <summary>
    /// 获取表
    /// </summary>
    /// <param name="entity">实体类型</param>
    public string GetTable(Type entity) => $"t_{entity.Name}";

    /// <summary>
    /// 获取架构
    /// </summary>
    /// <param name="entity">实体类型</param>
    public string GetSchema(Type entity) => "s";

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    /// <returns></returns>
    public string GetColumns<TEntity>(bool propertyAsAlias) => "*";

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="columns">列名表达式</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public string GetColumns<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias) => Lambdas.GetLastNames(columns).Select(column => $"t_{column}").Join();

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">列名表达式</param>
    public string GetColumn<TEntity>(Expression<Func<TEntity, object>> column) => $"t_{Lambdas.GetLastName(column)}";

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="entity">实体类型</param>
    /// <param name="right">是否右侧操作</param>
    public string GetColumn(Expression expression, Type entity, bool right) => $"t_{Lambdas.GetLastName(expression, right)}";

    /// <summary>
    /// 获取类型
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">是否取右侧操作数</param>
    public Type GetType(Expression expression, bool right = false) => typeof(Sample);
}