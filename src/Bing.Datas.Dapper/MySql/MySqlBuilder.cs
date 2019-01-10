using System.Text;
using Bing.Datas.Matedatas;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Core;

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
        /// <param name="parameterManager">参数管理器</param>
        public MySqlBuilder(IEntityMatedata matedata=null,IParameterManager parameterManager = null) : base(matedata, parameterManager) { }

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        public override ISqlBuilder New()
        {
            return new MySqlBuilder(EntityMatedata, ParameterManager);
        }

        /// <summary>
        /// 创建分页Sql
        /// </summary>
        /// <param name="result">Sql拼接</param>
        protected override void CreatePagerSql(StringBuilder result)
        {
            AppendSelect(result);
            AppendFrom(result);
            AppendSql(result, GetJoin());
            AppendSql(result, GetWhere());
            AppendSql(result, GetGroupBy());
            AppendSql(result, GetOrderBy());
            result.Append($"Limit {GetSkipCountParam()}, {GetPageSizeParam()}");
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
            return new MySqlFromClause(GetDialect(), EntityResolver, AliasRegister);
        }

        /// <summary>
        /// 创建Join子句
        /// </summary>
        /// <returns></returns>
        protected override IJoinClause CreateJoinClause()
        {
            return new MySqlJoinClause(this, GetDialect(), EntityResolver, AliasRegister);
        }
    }
}
