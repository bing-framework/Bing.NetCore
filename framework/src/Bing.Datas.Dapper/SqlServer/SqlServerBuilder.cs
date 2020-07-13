using Bing.Datas.Sql;
using Bing.Datas.Sql.Builders;
using Bing.Datas.Sql.Builders.Core;
using Bing.Datas.Sql.Matedatas;

namespace Bing.Datas.Dapper.SqlServer
{
    /// <summary>
    /// Sql Server Sql生成器
    /// </summary>
    public class SqlServerBuilder : SqlBuilderBase
    {
        /// <summary>
        /// 初始化一个<see cref="SqlServerBuilder"/>类型的实例
        /// </summary>
        /// <param name="matedata">实体元数据解析器</param>
        /// <param name="tableDatabase">表数据库</param>
        /// <param name="parameterManager">参数管理器</param>
        public SqlServerBuilder(IEntityMatedata matedata = null, ITableDatabase tableDatabase = null, IParameterManager parameterManager = null) : base(matedata, tableDatabase, parameterManager) { }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public override ISqlBuilder Clone()
        {
            var sqlBuilder = new SqlServerBuilder();
            sqlBuilder.Clone(this);
            return sqlBuilder;
        }

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        public override ISqlBuilder New() => new SqlServerBuilder(EntityMatedata, TableDatabase, ParameterManager);

        /// <summary>
        /// 创建分页Sql
        /// </summary>
        protected override string CreateLimitSql() => $"Offset {GetOffsetParam()} Rows Fetch Next {GetLimitParam()} Rows Only";

        /// <summary>
        /// 获取Sql方言
        /// </summary>
        protected override IDialect GetDialect() => new SqlServerDialect();
    }
}
