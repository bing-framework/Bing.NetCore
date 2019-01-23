namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // 清空
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 清空并初始化
        /// </summary>
        public void Clear()
        {
            AliasRegister = new EntityAliasRegister();
            ClearSelect();
            ClearFrom();
            ClearJoin();
            ClearWhere();
            ClearGroupBy();
            ClearOrderBy();
            ClearSqlParams();
            ClearPageParams();
        }

        /// <summary>
        /// 清空Select子句
        /// </summary>
        public void ClearSelect()
        {
            _selectClause = CreateSelectClause();
        }

        /// <summary>
        /// 清空From子句
        /// </summary>
        public void ClearFrom()
        {
            _fromClause = CreateFromClause();
        }

        /// <summary>
        /// 清空Join子句
        /// </summary>
        public void ClearJoin()
        {
            _joinClause = CreateJoinClause();
        }

        /// <summary>
        /// 清空Where子句
        /// </summary>
        public void ClearWhere()
        {
            _isAddFilters = false;
            _whereClause = CreateWhereClause();
        }

        /// <summary>
        /// 清空GroupBy子句
        /// </summary>
        public void ClearGroupBy()
        {
            _groupByClause = CreateGroupByClause();
        }

        /// <summary>
        /// 清空OrderBy子句
        /// </summary>
        public void ClearOrderBy()
        {
            _orderByClause = CreateOrderByClause();
        }

        /// <summary>
        /// 清空Sql参数
        /// </summary>
        public void ClearSqlParams()
        {
            _parameterManager.Clear();
        }

        /// <summary>
        /// 清空分页参数
        /// </summary>
        public void ClearPageParams()
        {
            Pager = null;
            OffsetParam = null;
            LimitParam = null;
        }
    }
}
