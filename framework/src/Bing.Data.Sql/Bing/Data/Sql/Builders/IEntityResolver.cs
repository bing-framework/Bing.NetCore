using System.Linq.Expressions;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 实体解析器
/// </summary>
public interface IEntityResolver
{
    /// <summary>
    /// 获取表
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <returns>实体对应的表名；无法解析时返回 <see langword="null"/>。</returns>
    string GetTable(Type entity);

    /// <summary>
    /// 获取架构
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <returns>实体对应的架构名；未配置或无法解析时返回 <see langword="null"/>。</returns>
    string GetSchema(Type entity);

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    /// <returns>按实体映射生成的列名列表。</returns>
    string GetColumns<TEntity>(bool propertyAsAlias);

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="columns">列名表达式</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    /// <returns>按表达式和实体映射生成的列名列表。</returns>
    string GetColumns<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias);

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">列名表达式</param>
    /// <returns>表达式对应的列名；无法解析时返回 <see langword="null"/>。</returns>
    string GetColumn<TEntity>(Expression<Func<TEntity, object>> column);

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="entity">实体类型</param>
    /// <param name="right">是否取右侧操作数</param>
    /// <returns>表达式对应的列名。</returns>
    string GetColumn(Expression expression, Type entity, bool right = false);

    /// <summary>
    /// 获取类型
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">是否取右侧操作数</param>
    /// <returns>表达式操作数对应的实体类型；无法解析时返回 <see langword="null"/>。</returns>
    Type GetType(Expression expression, bool right = false);
}