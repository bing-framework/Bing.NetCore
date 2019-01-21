using System.Collections.Generic;
using Bing.Datas.Matedatas;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Clauses;
using Bing.Domains.Repositories;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    /// <summary>
    /// Sql生成器基类
    /// </summary>
    public abstract partial class SqlBuilderBase:ISqlBuilder
    {
        #region 字段

        /// <summary>
        /// 参数管理器
        /// </summary>
        private IParameterManager _parameterManager;

        /// <summary>
        /// 方言
        /// </summary>
        private IDialect _dialect;

        /// <summary>
        /// Select子句
        /// </summary>
        private ISelectClause _selectClause;

        /// <summary>
        /// From子句
        /// </summary>
        private IFromClause _fromClause;

        /// <summary>
        /// Join子句
        /// </summary>
        private IJoinClause _joinClause;

        /// <summary>
        /// Where子句
        /// </summary>
        private IWhereClause _whereClause;

        /// <summary>
        /// 分组字句
        /// </summary>
        private IGroupByClause _groupByClause;

        /// <summary>
        /// 排序子句
        /// </summary>
        private IOrderByClause _orderByClause;

        /// <summary>
        /// 分页
        /// </summary>
        private IPager _pager;

        /// <summary>
        /// 分页跳过行数参数名
        /// </summary>
        private string _skipCountParam;

        /// <summary>
        /// 分页大小参数名
        /// </summary>
        private string _pageSizeParam;

        #endregion

        #region 属性

        /// <summary>
        /// 实体元数据解析器
        /// </summary>
        protected IEntityMatedata EntityMatedata { get; private set; }

        /// <summary>
        /// 实体解析器
        /// </summary>
        protected IEntityResolver EntityResolver { get; private set; }

        /// <summary>
        /// 实体别名注册器
        /// </summary>
        protected IEntityAliasRegister AliasRegister { get; private set; }

        /// <summary>
        /// 参数管理器
        /// </summary>
        protected IParameterManager ParameterManager => _parameterManager ?? (_parameterManager = CreateParameterManager());

        /// <summary>
        /// Sql方言
        /// </summary>
        protected IDialect Dialect => _dialect ?? (_dialect = GetDialect());

        /// <summary>
        /// Select子句
        /// </summary>
        protected ISelectClause SelectClause => _selectClause ?? (_selectClause = CreateSelectClause());

        /// <summary>
        /// From子句
        /// </summary>
        protected IFromClause FromClause => _fromClause ?? (_fromClause = CreateFromClause());

        /// <summary>
        /// Join子句
        /// </summary>
        protected IJoinClause JoinClause => _joinClause ?? (_joinClause = CreateJoinClause());

        /// <summary>
        /// Where子句
        /// </summary>
        protected IWhereClause WhereClause => _whereClause ?? (_whereClause = CreateWhereClause());

        /// <summary>
        /// 分组子句
        /// </summary>
        protected IGroupByClause GroupByClause => _groupByClause ?? (_groupByClause = CreateGroupByClause());

        /// <summary>
        /// 排序子句
        /// </summary>
        protected IOrderByClause OrderByClause => _orderByClause ?? (_orderByClause = CreateOrderByClause());

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="SqlBuilderBase"/>类型的实例
        /// </summary>
        /// <param name="matedata">实体元数据解析器</param>
        /// <param name="parameterManager">参数管理器</param>
        protected SqlBuilderBase(IEntityMatedata matedata = null, IParameterManager parameterManager = null)
        {
            EntityMatedata = matedata;
            _parameterManager = parameterManager;
            EntityResolver = new EntityResolver(matedata);
            AliasRegister = new EntityAliasRegister();
        }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建参数管理器
        /// </summary>
        /// <returns></returns>
        protected virtual IParameterManager CreateParameterManager()
        {
            return new ParameterManager(Dialect);
        }

        /// <summary>
        /// 获取Sql方言
        /// </summary>
        /// <returns></returns>
        protected abstract IDialect GetDialect();

        /// <summary>
        /// 创建Select子句
        /// </summary>
        /// <returns></returns>
        protected virtual ISelectClause CreateSelectClause()
        {
            return new SelectClause(this, Dialect, EntityResolver, AliasRegister);
        }

        /// <summary>
        /// 创建From子句
        /// </summary>
        /// <returns></returns>
        protected virtual IFromClause CreateFromClause()
        {
            return new FromClause(Dialect, EntityResolver, AliasRegister);
        }

        /// <summary>
        /// 创建Join子句
        /// </summary>
        /// <returns></returns>
        protected virtual IJoinClause CreateJoinClause()
        {
            return new JoinClause(this, Dialect, EntityResolver, AliasRegister);
        }

        /// <summary>
        /// 创建Where子句
        /// </summary>
        /// <returns></returns>
        protected virtual IWhereClause CreateWhereClause()
        {
            return new WhereClause(Dialect, EntityResolver, AliasRegister, ParameterManager);
        }

        /// <summary>
        /// 创建分组子句
        /// </summary>
        /// <returns></returns>
        protected virtual IGroupByClause CreateGroupByClause()
        {
            return new GroupByClause(Dialect, EntityResolver, AliasRegister);
        }

        /// <summary>
        /// 创建排序子句
        /// </summary>
        /// <returns></returns>
        protected virtual IOrderByClause CreateOrderByClause()
        {
            return new OrderByClause(Dialect, EntityResolver, AliasRegister);
        }

        #endregion

        #region Clone(克隆)

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public abstract ISqlBuilder Clone();

        /// <summary>
        /// 克隆
        /// </summary>
        /// <param name="sqlBuilder">源生成器</param>
        protected void Clone(SqlBuilderBase sqlBuilder)
        {
            EntityMatedata = sqlBuilder.EntityMatedata;
            _parameterManager = sqlBuilder._parameterManager?.Clone();
            EntityResolver = sqlBuilder.EntityResolver ?? new EntityResolver(EntityMatedata);
            AliasRegister = sqlBuilder.AliasRegister?.Clone() ?? new EntityAliasRegister();
            _selectClause = sqlBuilder._selectClause?.Clone(this, AliasRegister);
            _fromClause = sqlBuilder._fromClause?.Clone(AliasRegister);
            _joinClause = sqlBuilder._joinClause?.Clone(this, AliasRegister);
            _whereClause = sqlBuilder._whereClause?.Clone(AliasRegister, _parameterManager);
            _groupByClause = sqlBuilder._groupByClause?.Clone(AliasRegister);
            _orderByClause = sqlBuilder._orderByClause?.Clone(AliasRegister);
            _pager = sqlBuilder._pager;
        }

        #endregion

        #region New(创建Sql生成器)

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        public abstract ISqlBuilder New();

        #endregion

        #region AddParam(添加参数)

        /// <summary>
        /// 添加Sql参数
        /// </summary>
        /// <param name="name">参数名</param>
        /// <param name="value">参数值</param>
        public void AddParam(string name, object value)
        {
            ParameterManager.Add(name, value);
        }

        #endregion

        #region GetParams(获取参数)

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, object> GetParams()
        {
            return ParameterManager.GetParams();
        }

        #endregion

        #region Pager(设置分页)

        /// <summary>
        /// 获取分页参数
        /// </summary>
        /// <returns></returns>
        protected IPager GetPager()
        {
            return _pager;
        }

        /// <summary>
        /// 获取分页跳过行数的参数
        /// </summary>
        /// <returns></returns>
        protected string GetSkipCountParam()
        {
            if (string.IsNullOrWhiteSpace(_skipCountParam) == false)
            {
                return _skipCountParam;
            }

            _skipCountParam = ParameterManager.GenerateName();
            ParameterManager.Add(_skipCountParam, GetPager().GetSkipCount());
            return _skipCountParam;
        }

        /// <summary>
        /// 获取分页大小的额参数
        /// </summary>
        /// <returns></returns>
        protected string GetPageSizeParam()
        {
            if (string.IsNullOrWhiteSpace(_pageSizeParam) == false)
            {
                return _pageSizeParam;
            }

            _pageSizeParam = ParameterManager.GenerateName();
            ParameterManager.Add(_pageSizeParam, GetPager().PageSize);
            return _pageSizeParam;
        }

        /// <summary>
        /// 设置分页
        /// </summary>
        /// <param name="pager">分页参数</param>
        /// <returns></returns>
        public virtual ISqlBuilder Page(IPager pager)
        {
            _pager = pager;
            return this;
        }

        #endregion
    }
}
