using Bing.Datas.Sql;
using Bing.Datas.Sql.Builders;
using Bing.Datas.Sql.Builders.Core;
using Bing.Datas.Sql.Matedatas;

namespace Bing.Datas.Dapper.MySql
{
    /// <summary>
    /// MySql Sql生成器
    /// </summary>
    public class MySqlBuilder:SqlBuilderBase
    {
        /// <summary>
        /// 初始化一个<see cref="MySqlBuilder"/>类型的实例
        /// </summary>
        /// <param name="matedata">实体元数据解析器</param>
        /// <param name="tableDatabase">表数据库</param>
        /// <param name="parameterManager">参数管理器</param>
        public MySqlBuilder(IEntityMatedata matedata = null, ITableDatabase tableDatabase = null, IParameterManager parameterManager = null) : base(matedata, tableDatabase, parameterManager) { }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public override ISqlBuilder Clone()
        {
            var sqlBuilder = new MySqlBuilder();
            sqlBuilder.Clone(this);
            return sqlBuilder;
        }

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        public override ISqlBuilder New()
        {
            return new MySqlBuilder(EntityMatedata, TableDatabase, ParameterManager);
        }

        /// <summary>
        /// 获取Sql方言
        /// </summary>
        /// <returns></returns>
        protected override IDialect GetDialect()
        {
            return new MySqlDialect();
        }

        /// <summary>
        /// 创建From子句
        /// </summary>
        /// <returns></returns>
        protected override IFromClause CreateFromClause()
        {
            return new MySqlFromClause(this, GetDialect(), EntityResolver, AliasRegister, TableDatabase);
        }

        /// <summary>
        /// 创建Join子句
        /// </summary>
        /// <returns></returns>
        protected override IJoinClause CreateJoinClause()
        {
            return new MySqlJoinClause(this, GetDialect(), EntityResolver, AliasRegister, ParameterManager,
                TableDatabase);
        }

        /// <summary>
        /// 创建分页Sql
        /// </summary>
        protected override string CreateLimitSql()
        {
            return $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";
        }

        /// <summary>
        /// 获取CTE关键字
        /// </summary>
        protected override string GetCteKeyWord() => "With Recursive";
    }
}
