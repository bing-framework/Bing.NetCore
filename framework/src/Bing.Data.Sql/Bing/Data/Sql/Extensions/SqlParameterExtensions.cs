using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Params;
using Bing.Expressions;
using Bing.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Sql参数(<see cref="ISqlParameter"/>) 扩展
/// </summary>
public static class SqlParameterExtensions
{
    #region AddParam(添加Sql参数)

    /// <summary>
    /// 添加Sql参数
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    public static T AddParam<T>(this T source, string name, object value = null)
        where T : ISqlParameter
    {
        source.CheckNull(nameof(source));
        if (GetSqlBuilder(source) is ISqlCommonPartAccessor accessor &&
            string.IsNullOrWhiteSpace(accessor.ParameterManager.NormalizeName(name)) == false)
            SqlQueryOperationAccessor.MutateBuilder(source, _ => accessor.ParameterManager.Add(name, value));
        return source;
    }

    /// <summary>
    /// 添加Sql参数
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    /// <param name="property">实体属性表达式</param>
    /// <param name="value">参数值</param>
    public static T AddParam<T, TEntity>(this T source, string name, Expression<Func<TEntity, object>> property,
        object value = null)
        where T : ISqlParameter
        where TEntity : class
    {
        source.CheckNull(nameof(source));
        property.CheckNull(nameof(property));
        if (GetSqlBuilder(source) is not ISqlCommonPartAccessor accessor)
            return source;
        if (accessor.ParameterManager is IAdvancedParameterManager advancedParameterManager)
        {
            var parameter = CreateSqlParam(source, name, property, value);
            if (parameter != null &&
                string.IsNullOrWhiteSpace(accessor.ParameterManager.NormalizeName(parameter.Name)) == false)
                SqlQueryOperationAccessor.MutateBuilder(source, _ => advancedParameterManager.Add(parameter));
            return source;
        }
        if (string.IsNullOrWhiteSpace(accessor.ParameterManager.NormalizeName(name)) == false)
            SqlQueryOperationAccessor.MutateBuilder(source, _ => accessor.ParameterManager.Add(name, value));
        return source;
    }

    #endregion

    #region GetParams(获取参数列表)

    /// <summary>
    /// 获取参数列表
    /// </summary>
    /// <param name="source">源</param>
    public static IReadOnlyDictionary<string, object> GetParams(this ISqlParameter source)
    {
        source.CheckNull(nameof(source));
        if (GetSqlBuilder(source) is ISqlCommonPartAccessor accessor)
            return accessor.ParameterManager.GetParams();
        return default;
    }

    /// <summary>
    /// 获取增强参数列表
    /// </summary>
    /// <param name="source">源</param>
    public static IReadOnlyDictionary<string, SqlParam> GetSqlParams(this ISqlParameter source)
    {
        source.CheckNull(nameof(source));
        if (GetSqlBuilder(source) is not ISqlCommonPartAccessor accessor)
            return default;
        if (accessor.ParameterManager is IAdvancedParameterManager advancedParameterManager)
            return advancedParameterManager.GetSqlParams();
        var parameters = accessor.ParameterManager.GetParams().ToDictionary(
            item => item.Key,
            item => new SqlParam(item.Key, item.Value)
            {
                Source = SqlParameterSource.Basic,
                MetadataLevel = SqlParameterMetadataLevel.Weak
            },
            StringComparer.OrdinalIgnoreCase);
        return new ReadOnlyDictionary<string, SqlParam>(parameters);
    }

    #endregion

    #region GetParam(获取参数值)

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    public static T GetParam<T>(this ISqlParameter source, string name)
    {
        source.CheckNull(nameof(source));
        if (GetSqlBuilder(source) is ISqlCommonPartAccessor accessor)
            return (T)accessor.ParameterManager.GetValue(name);
        return default;
    }

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    public static object GetParam(this ISqlParameter source, string name)
    {
        source.CheckNull(nameof(source));
        if (GetSqlBuilder(source) is ISqlCommonPartAccessor accessor)
            return accessor.ParameterManager.GetValue(name);
        return default;
    }

    #endregion

    #region ClearParams(清空Sql参数)

    /// <summary>
    /// 清空Sql参数
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    public static T ClearParams<T>(this T source)
        where T : ISqlParameter
    {
        source.CheckNull(nameof(source));
        if (GetSqlBuilder(source) is ISqlCommonPartAccessor accessor && accessor.ParameterManager.Count > 0)
            SqlQueryOperationAccessor.MutateBuilder(source, _ => accessor.ParameterManager.Clear());
        return source;
    }

    /// <summary>
    /// 创建增强 Sql 参数
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    /// <param name="property">属性表达式</param>
    /// <param name="value">参数值</param>
    /// <returns>Sql 参数</returns>
    private static SqlParam CreateSqlParam<T, TEntity>(T source, string name, Expression<Func<TEntity, object>> property,
        object value)
        where T : ISqlParameter
        where TEntity : class
    {
        var propertyName = Lambdas.GetLastName(property);
        var builder = GetSqlBuilder(source);
        if (builder is SqlBuilderBase sqlBuilder)
        {
            var column = sqlBuilder.ResolveColumnMetadata(typeof(TEntity), propertyName);
            var parameter = sqlBuilder.CreateSqlParam(name, value, column, typeof(TEntity), SqlParameterSource.Manual);
            if (parameter != null)
            {
                parameter.EntityType ??= typeof(TEntity);
                parameter.PropertyName ??= propertyName;
                parameter.Source = SqlParameterSource.Manual;
                return parameter;
            }
        }
        return new SqlParam(name, value)
        {
            EntityType = typeof(TEntity),
            PropertyName = propertyName,
            Source = SqlParameterSource.Manual,
            MetadataLevel = SqlParameterMetadataLevel.Weak
        };
    }

    /// <summary>
    /// 获取 Sql 生成器
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <returns>Sql 生成器</returns>
    private static object GetSqlBuilder<T>(T source) where T : ISqlParameter
    {
        if (source is ISqlQueryBuilderAccessor accessor)
            return accessor.GetSqlBuilder();
        return (object)(source as ISqlBuilder) ?? source as ISqlCommonPartAccessor;
    }

    #endregion
}
