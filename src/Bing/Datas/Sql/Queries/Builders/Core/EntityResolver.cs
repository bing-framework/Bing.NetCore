using System;
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
        /// <returns></returns>
        public string GetColumns<TEntity>(Expression<Func<TEntity, object[]>> columns)
        {
            var names = Lambda.GetLastNames(columns);
            return _matedata == null
                ? names.Join()
                : names.Select(name => _matedata.GetColumn(typeof(TEntity), name)).Join();
        }

        /// <summary>
        /// 获取列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">列名表达式</param>
        /// <returns></returns>
        public string GetColumn<TEntity>(Expression<Func<TEntity, object>> column)
        {
            var name = Lambda.GetLastName(column);
            return _matedata == null
                ? name
                : _matedata.GetColumn(typeof(TEntity), name);
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
    }
}
