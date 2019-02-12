using Bing.Datas.Dapper.SqlServer;
using Bing.Datas.Sql.Builders.Clauses;
using Bing.Datas.Sql.Builders.Core;
using Bing.Datas.Test.Integration.Dapper.SqlServer.Samples;
using Bing.Datas.Test.Integration.Samples;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Datas.Test.Integration.Dapper.SqlServer.Clauses
{
    /// <summary>
    /// Group By子句测试
    /// </summary>
    public class GroupByClauseTest:TestBase
    {
        /// <summary>
        /// 分组子句
        /// </summary>
        private GroupByClause _clause;

        /// <summary>
        /// 测试初始化
        /// </summary>
        public GroupByClauseTest(ITestOutputHelper output) : base(output)
        {
            _clause = new GroupByClause(new SqlServerDialect(), new EntityResolver(), new EntityAliasRegister());
        }

        /// <summary>
        /// 获取Sql语句
        /// </summary>
        private string GetSql()
        {
            return _clause.ToSql();
        }

        /// <summary>
        /// 默认输出空
        /// </summary>
        [Fact]
        public void Test_Default()
        {
            Assert.Null(GetSql());
        }

        /// <summary>
        /// 测试分组
        /// </summary>
        [Fact]
        public void Test_GroupBy_1()
        {
            _clause.GroupBy("a");
            Assert.Equal("Group By [a]", GetSql());
        }

        /// <summary>
        /// 测试分组
        /// </summary>
        [Fact]
        public void Test_GroupBy_2()
        {
            _clause.GroupBy("a.B,c.[D]");
            Assert.Equal("Group By [a].[B],[c].[D]", GetSql());
        }

        /// <summary>
        /// 测试分组 - 验证分组字段为空
        /// </summary>
        [Fact]
        public void Test_GroupBy_3()
        {
            _clause.GroupBy("");
            Assert.Null(GetSql());
        }

        /// <summary>
        /// 测试分组 - 分组条件
        /// </summary>
        [Fact]
        public void Test_GroupBy_4()
        {
            _clause.GroupBy("a", "b");
            Assert.Equal("Group By [a] Having b", GetSql());
        }

        /// <summary>
        /// 测试分组 - lambda
        /// </summary>
        [Fact]
        public void Test_GroupBy_5()
        {
            _clause.GroupBy<Sample>(t => t.Email, "b");
            Assert.Equal("Group By [Email] Having b", GetSql());
        }

        /// <summary>
        /// 测试分组 - 别名
        /// </summary>
        [Fact]
        public void Test_GroupBy_6()
        {
            _clause = new GroupByClause(new SqlServerDialect(), new TestEntityResolver(), new TestEntityAliasRegister());
            _clause.GroupBy<Sample>(t => t.Email, "b");
            Assert.Equal("Group By [as_Sample].[t_Email] Having b", GetSql());
        }

        /// <summary>
        /// 测试分组 - 多个GroupBy
        /// </summary>
        [Fact]
        public void Test_GroupBy_7()
        {
            _clause.GroupBy("a", "b");
            _clause.GroupBy<Sample>(t => t.Email, "c");
            Assert.Equal("Group By [a],[Email] Having c", GetSql());
        }

        /// <summary>
        /// 测试分组 - Append
        /// </summary>
        [Fact]
        public void Test_GroupBy_8()
        {
            _clause.GroupBy("a");
            _clause.GroupBy("b");
            _clause.AppendSql("c");
            _clause.AppendSql("d");
            Assert.Equal("Group By [a],[b],c,d", GetSql());
        }
    }
}
