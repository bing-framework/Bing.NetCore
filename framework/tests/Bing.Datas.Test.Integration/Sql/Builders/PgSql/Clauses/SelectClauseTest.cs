using Bing.Datas.Dapper.PgSql;
using Bing.Datas.Sql.Builders.Clauses;
using Bing.Datas.Sql.Builders.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Datas.Test.Integration.Sql.Builders.PgSql.Clauses
{
    /// <summary>
    /// Select子句测试
    /// </summary>
    public class SelectClauseTest : TestBase
    {
        /// <summary>
        /// Select子句
        /// </summary>
        private SelectClause _clause;

        /// <summary>
        /// 初始化一个<see cref="SelectClauseTest"/>类型的实例
        /// </summary>
        public SelectClauseTest(ITestOutputHelper output) : base(output)
        {
            _clause = new SelectClause(new PgSqlBuilder(), new PgSqlDialect(), new EntityResolver(), new EntityAliasRegister());
        }

        /// <summary>
        /// 获取Sql语句
        /// </summary>
        private string GetSql() => _clause.ToSql();

        /// <summary>
        /// 添加Select子句
        /// </summary>
        [Fact]
        public void Test_AppendSql_1()
        {
            _clause.AppendSql("a");
            Assert.Equal("Select a", GetSql());
        }

        /// <summary>
        /// 添加Select子句 - 带方括号
        /// </summary>
        [Fact]
        public void Test_AppendSql_2()
        {
            _clause.AppendSql("[a].[b]");
            Assert.Equal("Select \"a\".\"b\"", GetSql());
        }
    }
}
