using System.Text;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Core;

namespace Bing.Datas.Dapper.SqlServer
{
    public class SqlServerBuilder:SqlBuilderBase
    {
        public override ISqlBuilder New()
        {
            throw new System.NotImplementedException();
        }

        protected override void CreatePagerSql(StringBuilder result)
        {
            throw new System.NotImplementedException();
        }

        protected override IDialect GetDialect()
        {
            throw new System.NotImplementedException();
        }
    }
}
