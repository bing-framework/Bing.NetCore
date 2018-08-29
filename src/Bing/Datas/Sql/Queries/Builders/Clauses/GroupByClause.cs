using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Core;
using Bing.Utils.Extensions;

namespace Bing.Datas.Sql.Queries.Builders.Clauses
{
    /// <summary>
    /// Group By子句
    /// </summary>
    public class GroupByClause:IGroupByClause
    {
        /// <summary>
        /// Sql方言
        /// </summary>
        private readonly IDialect _dialect;

        /// <summary>
        /// 实体解析器
        /// </summary>
        private readonly IEntityResolver _resolver;

        /// <summary>
        /// 实体别名注册器
        /// </summary>
        private readonly IEntityAliasRegister _register;

        /// <summary>
        /// 分组条件
        /// </summary>
        private readonly List<SqlItem> _group;

        /// <summary>
        /// 分组条件
        /// </summary>
        private string _having;

        /// <summary>
        /// 初始化一个<see cref="GroupByClause"/>类型的实例
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="resolver">实体解析器</param>
        /// <param name="register">实体别名注册器</param>
        public GroupByClause(IDialect dialect, IEntityResolver resolver, IEntityAliasRegister register)
        {
            _dialect = dialect;
            _resolver = resolver;
            _register = register;
            _group=new List<SqlItem>();
        }

        /// <summary>
        /// 分组
        /// </summary>
        /// <param name="groupBy">分组字段</param>
        /// <param name="having">分组条件</param>
        public void GroupBy(string groupBy, string having = null)
        {
            if (string.IsNullOrWhiteSpace(groupBy))
            {
                return;
            }
            _group.AddRange(groupBy.Split(',').Select(item=>new SqlItem(item)));
            _having = having;
        }

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">分组字段</param>
        /// <param name="having">分组条件</param>
        public void GroupBy<TEntity>(Expression<Func<TEntity, object>> column, string having = null)
        {
            if (column == null)
            {
                return;
            }
            _group.Add(new SqlItem(_resolver.GetColumn(column),_register.GetAlias(typeof(TEntity))));
            _having = having;
        }

        /// <summary>
        /// 添加到GroupBy子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        public void AppendSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return;
            }
            _group.Add(new SqlItem(sql, raw: true));
        }

        /// <summary>
        /// 获取Sql
        /// </summary>
        /// <returns></returns>
        public string ToSql()
        {
            if (_group.Count == 0)
            {
                return null;
            }
            var result=new StringBuilder();
            result.Append($"Group By {_group.Select(t => t.ToSql(_dialect)).Join()}");
            if (string.IsNullOrWhiteSpace(_having))
            {
                return result.ToString();
            }
            result.Append($" Having {_having}");
            return result.ToString();
        }
    }
}
