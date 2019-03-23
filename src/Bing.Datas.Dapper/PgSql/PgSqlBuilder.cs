using Bing.Datas.Sql;
using Bing.Datas.Sql.Builders;
using Bing.Datas.Sql.Builders.Core;
using Bing.Datas.Sql.Matedatas;

namespace Bing.Datas.Dapper.PgSql
{
    /// <summary>
    /// PgSql Sql生成器
    /// </summary>
    public class PgSqlBuilder:SqlBuilderBase
    {
        /// <summary>
        /// 初始化一个<see cref="PgSqlBuilder"/>类型的实例
        /// </summary>
        /// <param name="matedata">实体元数据解析器</param>
        /// <param name="tableDatabase">表数据库</param>
        /// <param name="parameterManager">参数管理器</param>
        public PgSqlBuilder(IEntityMatedata matedata = null,ITableDatabase tableDatabase = null, IParameterManager parameterManager = null) : base(matedata,tableDatabase, parameterManager) { }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public override ISqlBuilder Clone()
        {
            var sqlBuilder = new PgSqlBuilder();
            sqlBuilder.Clone(this);
            return sqlBuilder;
        }

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        public override ISqlBuilder New()
        {
            return new PgSqlBuilder(EntityMatedata, TableDatabase, ParameterManager);
        }

        /// <summary>
        /// 获取Sql方言
        /// </summary>
        /// <returns></returns>
        protected override IDialect GetDialect()
        {
            return new PgSqlDialect();
        }

        /// <summary>
        /// 获取参数字面值解析器
        /// </summary>
        /// <returns></returns>
        protected override IParamLiteralsResolver GetParamLiteralsResolver()
        {
            return new PgSqlParamLiteralsResolver();
        }

        /// <summary>
        /// 创建分页Sql
        /// </summary>
        protected override string CreateLimitSql()
        {
            return $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";
        }
    }
}
