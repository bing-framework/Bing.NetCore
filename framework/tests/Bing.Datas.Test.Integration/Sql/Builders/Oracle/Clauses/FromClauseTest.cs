using Bing.Datas.Dapper.Oracle;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Matedatas;
using Bing.Data.Test.Integration.Sql.Builders.Samples;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Data.Test.Integration.Sql.Builders.Oracle.Clauses
{
    /// <summary>
    /// From子句测试
    /// </summary>
    public class FromClauseTest:TestBase
    {
        /// <summary>
        /// From子句
        /// </summary>
        private readonly FromClause _clause;

        /// <summary>
        /// 表数据库
        /// </summary>
        private readonly ITableDatabase _database;

        /// <summary>
        /// 初始化一个<see cref="FromClauseTest"/>类型的实例
        /// </summary>
        public FromClauseTest(ITestOutputHelper output) : base(output)
        {
            _database = new TestTableDatabase();
            _clause = new OracleFromClause(null, new OracleDialect(), new EntityResolver(), new EntityAliasRegister(), null);
        }

        /// <summary>
        /// 获取Sql语句
        /// </summary>
        private string GetSql() => _clause.ToSql();

        /// <summary>
        /// 默认输出空
        /// </summary>
        [Fact]
        public void Test_Default()
        {
            Assert.Null(GetSql());
        }

        /// <summary>
        /// 设置表
        /// </summary>
        [Fact]
        public void Test_From_1()
        {
            _clause.From("a");
            Assert.Equal("From \"a\"", GetSql());
        }

        /// <summary>
        /// 设置表 - 别名
        /// </summary>
        [Fact]
        public void Test_From_2()
        {
            _clause.From("a", "b");
            Assert.Equal("From \"a\" \"b\"", GetSql());
        }

        /// <summary>
        /// 设置表 - 架构
        /// </summary>
        [Fact]
        public void Test_From_3()
        {
            _clause.From("a.b", "c");
            Assert.Equal("From \"a\".\"b\" \"c\"", GetSql());
        }
    }
}
