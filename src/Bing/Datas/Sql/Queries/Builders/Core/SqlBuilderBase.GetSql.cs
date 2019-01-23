using System;
using System.Text;
using System.Text.RegularExpressions;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Filters;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // 获取Sql语句
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 获取Select语句
        /// </summary>
        /// <returns></returns>
        public virtual string GetSelect()
        {
            return SelectClause.ToSql();
        }

        /// <summary>
        /// 获取From语句
        /// </summary>
        /// <returns></returns>
        public virtual string GetFrom()
        {
            return FromClause.ToSql();
        }

        /// <summary>
        /// 获取Join语句
        /// </summary>
        /// <returns></returns>
        public virtual string GetJoin()
        {
            return JoinClause.ToSql();
        }

        /// <summary>
        /// 获取查询条件
        /// </summary>
        /// <returns></returns>
        public string GetCondition()
        {
            return WhereClause.GetCondition();
        }

        /// <summary>
        /// 获取Where语句
        /// </summary>
        /// <returns></returns>
        public virtual string GetWhere()
        {
            if (_isAddFilters == false)
            {
                AddFilters();
            }

            return WhereClause.ToSql();
        }

        /// <summary>
        /// 添加过滤器列表
        /// </summary>
        private void AddFilters()
        {
            _isAddFilters = true;
            var context = new SqlQueryContext(AliasRegister, WhereClause, EntityMatedata);
            SqlFilterCollection.Filters.ForEach(filter => filter.Filter(context));
        }

        /// <summary>
        /// 获取分组语句
        /// </summary>
        /// <returns></returns>
        public virtual string GetGroupBy()
        {
            return GroupByClause.ToSql();
        }

        /// <summary>
        /// 获取排序语句
        /// </summary>
        /// <returns></returns>
        public virtual string GetOrderBy()
        {
            return OrderByClause.ToSql();
        }

        #region ToDebugSql(生成调试Sql语句)

        /// <summary>
        /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
        /// </summary>
        /// <returns></returns>
        public virtual string ToDebugSql()
        {
            return GetDebugSql(ToSql());
        }

        /// <summary>
        /// 获取调试Sql
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        private string GetDebugSql(string sql)
        {
            var parameters = GetParams();
            foreach (var parameter in parameters)
            {
                sql = Regex.Replace(sql, $@"{parameter.Key}\b",
                    ParamLiteralsResolver.GetParamLiterals(parameter.Value));
            }

            return sql;
        }

        #endregion

        #region ToSql(生成Sql)

        /// <summary>
        /// 生成Sql语句
        /// </summary>
        /// <returns></returns>
        public virtual string ToSql()
        {
            Init();
            Validate();
            var result = new StringBuilder();
            CreateSql(result);
            return result.ToString().Trim();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void Init()
        {
            OrderByClause.OrderBy(Pager?.Order);
        }

        /// <summary>
        /// 验证
        /// </summary>
        public virtual void Validate()
        {
            FromClause.Validate();
            OrderByClause.Validate(IsLimit);
        }

        /// <summary>
        /// 创建Sql语句
        /// </summary>
        /// <param name="result"></param>
        protected virtual void CreateSql(StringBuilder result)
        {
            AppendSelect(result);
            AppendFrom(result);
            AppendSql(result, GetJoin());
            AppendSql(result, GetWhere());
            AppendSql(result, GetGroupBy());
            AppendSql(result, GetOrderBy());
            AppendLimit(result);
        }

        /// <summary>
        /// 添加Sql
        /// </summary>
        /// <param name="result">Sql拼接</param>
        /// <param name="sql">Sql语句</param>
        protected void AppendSql(StringBuilder result, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return;
            }

            result.AppendLine($"{sql} ");
        }

        /// <summary>
        /// 添加Select子句
        /// </summary>
        /// <param name="result">Sql拼接器</param>
        protected virtual void AppendSelect(StringBuilder result)
        {
            var sql = GetSelect();
            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new InvalidOperationException("必须设置Select子句");
            }

            AppendSql(result, sql);
        }

        /// <summary>
        /// 添加From子句
        /// </summary>
        /// <param name="result">Sql拼接器</param>
        protected virtual void AppendFrom(StringBuilder result)
        {
            var sql = GetFrom();
            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new InvalidOperationException("必须设置From子句");
            }

            AppendSql(result, sql);
        }

        /// <summary>
        /// 添加分页Sql
        /// </summary>
        /// <param name="result">Sql拼接器</param>
        private void AppendLimit(StringBuilder result)
        {
            if (IsLimit)
            {
                AppendSql(result, CreateLimitSql());
            }
        }

        /// <summary>
        /// 创建分页Sql
        /// </summary>
        protected abstract string CreateLimitSql();

        #endregion
    }
}
