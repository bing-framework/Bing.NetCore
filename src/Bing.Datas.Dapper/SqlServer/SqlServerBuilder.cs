using System.Text;
using Bing.Datas.Matedatas;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Core;

namespace Bing.Datas.Dapper.SqlServer
{
    /// <summary>
    /// Sql Server Sql生成器
    /// </summary>
    public class SqlServerBuilder:SqlBuilderBase
    {
        /// <summary>
        /// 初始化一个<see cref="SqlServerBuilder"/>类型的实例
        /// </summary>
        /// <param name="matedata">实体元数据解析器</param>
        /// <param name="parameterManager">参数管理器</param>
        public SqlServerBuilder(IEntityMatedata matedata=null,IParameterManager parameterManager = null) : base(matedata, parameterManager) { }

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
        /// <returns></returns>
        public override ISqlBuilder New()
        {
            return new SqlServerBuilder(EntityMatedata, ParameterManager);
        }

        /// <summary>
        /// 创建分页Sql
        /// </summary>
        /// <param name="result">Sql拼接</param>
        protected override void CreatePagerSql(StringBuilder result)
        {
            AppendSelect(result);
            AppendFrom(result);
            AppendSql(result,GetJoin());
            AppendSql(result,GetWhere());
            AppendSql(result, GetGroupBy());
            AppendSql(result,GetOrderBy());
            result.Append($"Offset {GetSkipCountParam()} Rows Fetch Next {GetPageSizeParam()} Rows Only");
        }

        /// <summary>
        /// 获取Sql方言
        /// </summary>
        /// <returns></returns>
        protected override IDialect GetDialect()
        {
            return new SqlServerDialect();
        }
    }
}
