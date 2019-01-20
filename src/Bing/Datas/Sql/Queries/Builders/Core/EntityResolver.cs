using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Bing.Datas.Matedatas;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    /// <summary>
    /// 实体解析器
    /// </summary>
    public class EntityResolver : IEntityResolver
    {
        /// <summary>
        /// 实体元数据
        /// </summary>
        private readonly IEntityMatedata _matedata;

        /// <summary>
        /// 初始化一个<see cref="EntityResolver"/>类型的实例
        /// </summary>
        /// <param name="matedata">实体元数据</param>
        public EntityResolver(IEntityMatedata matedata = null)
        {
            _matedata = matedata;
        }

        /// <summary>
        /// 获取表
        /// </summary>
        /// <param name="entity">实体类型</param>
        /// <returns></returns>
        public string GetTable(Type entity)
        {
            if (_matedata == null)
            {
                return entity.Name;
            }

            var result = _matedata.GetTable(entity);
            return string.IsNullOrWhiteSpace(result) ? entity.Namespace : result;
        }

        /// <summary>
        /// 获取架构
        /// </summary>
        /// <param name="entity">实体类型</param>
        /// <returns></returns>
        public string GetSchema(Type entity)
        {
            return _matedata?.GetSchema(entity);
        }

        /// <summary>
        /// 获取列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">列名表达式</param>
        /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
        /// <returns></returns>
        public string GetColumns<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias)
        {
            var names = Lambda.GetLastNames(columns);
            if (_matedata == null)
            {
                return names.Join();
            }

            return GetColumns<TEntity>(names, propertyAsAlias);
        }

        /// <summary>
        /// 获取列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="names">列名集合</param>
        /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
        /// <returns></returns>
        private string GetColumns<TEntity>(List<string> names, bool propertyAsAlias)
        {
            if (propertyAsAlias == false)
            {
                return names.Select(name => _matedata.GetColumn(typeof(TEntity), name)).Join();
            }

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
        /// <returns></returns>
        public string GetColumn<TEntity>(Expression<Func<TEntity, object>> column)
        {
            return GetExpressionColumn<TEntity>(column);
        }


        /// <summary>
        /// 获取表达式列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <returns></returns>
        private string GetExpressionColumn<TEntity>(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }

            switch (expression.NodeType)
            {
                case ExpressionType.Lambda:
                    return GetExpressionColumn<TEntity>(((LambdaExpression) expression).Body);
                case ExpressionType.Convert:
                case ExpressionType.MemberAccess:
                    return GetSingleColumn<TEntity>(expression);
                case ExpressionType.ListInit:
                    var isDictionary = typeof(Dictionary<object, string>).GetGenericTypeDefinition()
                        .IsAssignableFrom(expression.Type.GetGenericTypeDefinition());
                    return isDictionary ? GetDictionaryColumns<TEntity>((ListInitExpression) expression) : null;
            }
            return null;
        }

        /// <summary>
        /// 获取单列
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <returns></returns>
        private string GetSingleColumn<TEntity>(Expression expression)
        {
            var name = Lambda.GetLastName(expression);
            if (_matedata == null)
            {
                return name;
            }

            return _matedata.GetColumn(typeof(TEntity), name);
        }

        /// <summary>
        /// 获取字典多列
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列表表达式</param>
        /// <returns></returns>
        private string GetDictionaryColumns<TEntity>(ListInitExpression expression)
        {
            var dictionary = GetDictionaryByListInitExpression(expression);
            return _matedata == null ? GetColumns(dictionary) : GetColumnsByMatedata<TEntity>(dictionary);
        }

        /// <summary>
        /// 获取字典
        /// </summary>
        /// <param name="expression">列表表达式</param>
        /// <returns></returns>
        private IDictionary<object, string> GetDictionaryByListInitExpression(ListInitExpression expression)
        {
            var result = new Dictionary<object, string>();
            foreach (var elementInit in expression.Initializers)
            {
                var keyValue = GetKeyValue(elementInit.Arguments);
                if (keyValue == null)
                {
                    continue;
                }

                var item = keyValue.SafeValue();
                result.Add(item.Key, item.Value);
            }

            return result;
        }

        /// <summary>
        /// 获取键值对
        /// </summary>
        /// <param name="arguments">参数表达式</param>
        /// <returns></returns>
        private KeyValuePair<object, string>? GetKeyValue(IEnumerable<Expression> arguments)
        {
            if (arguments == null)
            {
                return null;
            }

            var list = arguments.ToList();
            if (list.Count < 2)
            {
                return null;
            }

            return new KeyValuePair<object, string>(Lambda.GetName(list[0]), Lambda.GetValue(list[1]).SafeString());
        }

        /// <summary>
        /// 通过元数据解析创建列
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <returns></returns>
        private string GetColumnsByMatedata<TEntity>(IDictionary<object, string> dictionary)
        {
            string result = null;
            foreach (var item in dictionary)
            {
                result += $"{_matedata.GetColumn(typeof(TEntity), item.Key.SafeString())} As {item.Value},";
            }

            return result?.TrimEnd(',');
        }

        /// <summary>
        /// 通过字典创建列
        /// </summary>
        /// <param name="dictionary">字典</param>
        /// <returns></returns>
        private string GetColumns(IDictionary<object, string> dictionary)
        {
            string result = null;
            foreach (var item in dictionary)
            {
                result += $"{item.Key} As {item.Value},";
            }

            return result?.TrimEnd(',');
        }
        
        /// <summary>
        /// 获取列名
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <param name="entity">实体类型</param>
        /// <param name="right">是否取右侧操作数</param>
        /// <returns></returns>
        public string GetColumn(Expression expression, Type entity, bool right = false)
        {
            var column = Lambda.GetLastName(expression, right);
            return _matedata == null
                ? column
                : _matedata.GetColumn(entity, column);
        }

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <param name="right">是否取右侧操作数</param>
        /// <returns></returns>
        public Type GetType(Expression expression, bool right = false)
        {
            var memberExpression = Lambda.GetMemberExpression(expression, right);
            return memberExpression?.Expression?.Type;
        }
    }
}
