﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using Bing.Data.Sql.Metadata;
using Bing.Expressions;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 实体解析器
/// </summary>
public class EntityResolver : IEntityResolver
{
    /// <summary>
    /// 实体元数据
    /// </summary>
    private readonly IEntityMetadata _matedata;

    /// <summary>
    /// 初始化一个<see cref="EntityResolver"/>类型的实例
    /// </summary>
    /// <param name="matedata">实体元数据</param>
    public EntityResolver(IEntityMetadata matedata = null) => _matedata = matedata;

    /// <summary>
    /// 获取表
    /// </summary>
    /// <param name="entity">实体类型</param>
    public string GetTable(Type entity)
    {
        if (_matedata == null)
            return entity.Name;
        var result = _matedata.GetTable(entity);
        return string.IsNullOrWhiteSpace(result) ? entity.Name : result;
    }

    /// <summary>
    /// 获取架构
    /// </summary>
    /// <param name="entity">实体类型</param>
    public string GetSchema(Type entity) => _matedata?.GetSchema(entity);

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public string GetColumns<TEntity>(bool propertyAsAlias)
    {
        var type = typeof(TEntity);
        var names = GetProperties(type).Select(t => t.Name).ToList();
        return GetColumns<TEntity>(names, propertyAsAlias);
    }

    /// <summary>
    /// 获取属性列表
    /// </summary>
    /// <param name="type">类型</param>
    private List<PropertyInfo> GetProperties(Type type)
    {
        var result = new List<PropertyInfo>();
        var properties = type.GetProperties();
        foreach (var property in properties)
        {
            var notMapped = property.GetCustomAttribute<NotMappedAttribute>();
            if (notMapped != null)
                continue;
            result.Add(property);
        }
        return result;
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="columns">列名表达式</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public string GetColumns<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias)
    {
        var names = Lambdas.GetLastNames(columns);
        return _matedata == null ? names.Join() : GetColumns<TEntity>(names, propertyAsAlias);
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="names">列名集合</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    private string GetColumns<TEntity>(List<string> names, bool propertyAsAlias)
    {
        if (propertyAsAlias == false)
            return names.Select(name => _matedata.GetColumn(typeof(TEntity), name)).Join();
        return names.Select(name =>
        {
            var column = _matedata.GetColumn(typeof(TEntity), name);
            return column == name ? column : $"{column} As {name}";
        }).Join();
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">列名表达式</param>
    public string GetColumn<TEntity>(Expression<Func<TEntity, object>> column) => GetExpressionColumn<TEntity>(column);

    /// <summary>
    /// 获取表达式列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    private string GetExpressionColumn<TEntity>(Expression expression)
    {
        if (expression == null)
            return null;
        switch (expression.NodeType)
        {
            case ExpressionType.Lambda:
                return GetExpressionColumn<TEntity>(((LambdaExpression)expression).Body);

            case ExpressionType.Convert:
            case ExpressionType.MemberAccess:
                return GetSingleColumn<TEntity>(expression);

            case ExpressionType.ListInit:
                var isDictionary = typeof(Dictionary<object, string>).GetGenericTypeDefinition()
                    .IsAssignableFrom(expression.Type.GetGenericTypeDefinition());
                return isDictionary ? GetDictionaryColumns<TEntity>((ListInitExpression)expression) : null;
        }
        return null;
    }

    /// <summary>
    /// 获取单列
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    private string GetSingleColumn<TEntity>(Expression expression)
    {
        var name = Lambdas.GetLastName(expression);
        if (_matedata == null)
            return name;
        return _matedata.GetColumn(typeof(TEntity), name);
    }

    /// <summary>
    /// 获取字典多列
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列表表达式</param>
    private string GetDictionaryColumns<TEntity>(ListInitExpression expression)
    {
        var dictionary = GetDictionaryByListInitExpression(expression);
        return _matedata == null ? GetColumns(dictionary) : GetColumnsByMatedata<TEntity>(dictionary);
    }

    /// <summary>
    /// 获取字典
    /// </summary>
    /// <param name="expression">列表表达式</param>
    private IDictionary<object, string> GetDictionaryByListInitExpression(ListInitExpression expression)
    {
        var result = new Dictionary<object, string>();
        foreach (var elementInit in expression.Initializers)
        {
            var keyValue = GetKeyValue(elementInit.Arguments);
            if (keyValue == null)
                continue;
            var item = keyValue.SafeValue();
            result.Add(item.Key, item.Value);
        }

        return result;
    }

    /// <summary>
    /// 获取键值对
    /// </summary>
    /// <param name="arguments">参数表达式</param>
    private KeyValuePair<object, string>? GetKeyValue(IEnumerable<Expression> arguments)
    {
        if (arguments == null)
            return null;
        var list = arguments.ToList();
        if (list.Count < 2)
            return null;
        return new KeyValuePair<object, string>(Lambdas.GetName(list[0]), Lambdas.GetValue(list[1]).SafeString());
    }

    /// <summary>
    /// 通过元数据解析创建列
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="dictionary">字典</param>
    private string GetColumnsByMatedata<TEntity>(IDictionary<object, string> dictionary)
    {
        string result = null;
        foreach (var item in dictionary)
            result += $"{_matedata.GetColumn(typeof(TEntity), item.Key.SafeString())} As {item.Value},";
        return result?.TrimEnd(',');
    }

    /// <summary>
    /// 通过字典创建列
    /// </summary>
    /// <param name="dictionary">字典</param>
    private string GetColumns(IDictionary<object, string> dictionary)
    {
        string result = null;
        foreach (var item in dictionary)
            result += $"{item.Key} As {item.Value},";
        return result?.TrimEnd(',');
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="entity">实体类型</param>
    /// <param name="right">是否取右侧操作数</param>
    public string GetColumn(Expression expression, Type entity, bool right = false)
    {
        var column = Lambdas.GetLastName(expression, right);
        return _matedata == null
            ? column
            : _matedata.GetColumn(entity, column);
    }

    /// <summary>
    /// 获取类型
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">是否取右侧操作数</param>
    public Type GetType(Expression expression, bool right = false)
    {
        var memberExpression = Lambdas.GetMemberExpression(expression, right);
        return memberExpression?.Expression?.Type;
    }
}