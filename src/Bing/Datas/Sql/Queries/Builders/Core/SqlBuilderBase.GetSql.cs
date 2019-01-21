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
            var whereClause = WhereClause.Clone(AliasRegister, ParameterManager.Clone());
            AddFilters(whereClause);
            return whereClause.ToSql();
        }

        /// <summary>
        /// 添加过滤器列表
        /// </summary>
        /// <param name="whereClause">Where子句</param>
        private void AddFilters(IWhereClause whereClause)
        {
            var context = new SqlQueryContext(AliasRegister, whereClause, EntityMatedata);
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

        /// <summary>
        /// 参数字面值解析器
        /// </summary>
        private IParamLiteralsResolver _paramLiteralsResolver;

        /// <summary>
        /// 参数字面值解析器
        /// </summary>
        protected IParamLiteralsResolver ParamLiteralsResolver =>
            _paramLiteralsResolver ?? (_paramLiteralsResolver = GetParamLiteralsResolver());

        /// <summary>
        /// 获取参数字面值解析器
        /// </summary>
        /// <returns></returns>
        protected virtual IParamLiteralsResolver GetParamLiteralsResolver()
        {
            return new ParamLiteralsResolver();
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
            OrderByClause.OrderBy(_pager?.Order);
        }

        /// <summary>
        /// 验证
        /// </summary>
        public virtual void Validate()
        {
            FromClause.Validate();
            OrderByClause.Validate(_pager);
        }

        /// <summary>
        /// 创建Sql语句
        /// </summary>
        /// <param name="result"></param>
        protected virtual void CreateSql(StringBuilder result)
        {
            if (_pager == null)
            {
                CreateNoPagerSql(result);
                return;
            }
            CreatePagerSql(result);
        }

        /// <summary>
        /// 创建不分页Sql
        /// </summary>
        /// <param name="result">Sql拼接器</param>
        protected virtual void CreateNoPagerSql(StringBuilder result)
        {
            AppendSelect(result);
            AppendFrom(result);
            AppendSql(result, GetJoin());
            AppendSql(result, GetWhere());
            AppendSql(result, GetGroupBy());
            AppendSql(result, GetOrderBy());
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
        /// 创建分页Sql
        /// </summary>
        /// <param name="result">Sql拼接</param>
        protected abstract void CreatePagerSql(StringBuilder result);

        #endregion

        #region ToCountDebugSql(生成获取行数调试Sql语句)

        /// <summary>
        /// 生成获取行数调试Sql语句
        /// </summary>
        /// <returns></returns>
        public virtual string ToCountDebugSql()
        {
            return GetDebugSql(ToCountSql());
        }

        #endregion

        #region ToCountSql(生成获取行数Sql语句)

        /// <summary>
        /// 生成获取行数Sql语句
        /// </summary>
        /// <returns></returns>
        public virtual string ToCountSql()
        {
            Init();
            Validate();
            var result = new StringBuilder();
            if (GroupByClause.IsGroupBy)
            {
                AppendGroupCountSql(result);
            }
            else
            {
                AppendNoGroupCountSql(result);
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// 添加未分组的获取行数Sql语句
        /// </summary>
        /// <param name="result">Sql拼接器</param>
        private void AppendNoGroupCountSql(StringBuilder result)
        {
            result.AppendLine("Select Count(*) ");
            AppendFrom(result);
            AppendSql(result, GetJoin());
            AppendSql(result, GetWhere());
        }

        /// <summary>
        /// 添加分组的获取行数Sql语句
        /// </summary>
        /// <param name="result">Sql拼接器</param>
        private void AppendGroupCountSql(StringBuilder result)
        {
            result.AppendLine("Select Count(*) ");
            result.AppendLine("From (");
            result.AppendLine($"Select {GroupByClause.GroupByColumns} ");
            AppendFrom(result);
            AppendSql(result, GetJoin());
            AppendSql(result, GetWhere());
            result.AppendLine(GetGroupBy());
            result.Append(") As t");
        }

        #endregion
    }
}
