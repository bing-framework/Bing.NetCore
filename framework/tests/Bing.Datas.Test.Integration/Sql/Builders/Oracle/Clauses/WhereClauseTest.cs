using Bing.Datas.Dapper.Oracle;
using Bing.Datas.Dapper.SqlServer;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Matedatas;
using Bing.Data.Test.Integration.Sql.Builders.Samples;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Data.Test.Integration.Sql.Builders.Oracle.Clauses
{
    /// <summary>
    /// Where子句测试
    /// </summary>
    public class WhereClauseTest : TestBase
    {
        /// <summary>
        /// 参数管理器
        /// </summary>
        private readonly ParameterManager _parameterManager;

        /// <summary>
        /// 表数据库
        /// </summary>
        private readonly ITableDatabase _database;

        /// <summary>
        /// Sql生成器
        /// </summary>
        private readonly OracleBuilder _builder;

        /// <summary>
        /// Where子句
        /// </summary>
        private readonly WhereClause _clause;

        /// <summary>
        /// 初始化一个<see cref="WhereClauseTest"/>类型的实例
        /// </summary>
        public WhereClauseTest(ITestOutputHelper output) : base(output)
        {
            _parameterManager = new ParameterManager(new OracleDialect());
            _database = new TestTableDatabase();
            _builder = new OracleBuilder(new TestEntityMatedata(), _database, _parameterManager);
            _clause = new WhereClause(_builder, new OracleDialect(), new EntityResolver(), new EntityAliasRegister(), _parameterManager);
        }

        /// <summary>
        /// 获取Sql语句
        /// </summary>
        private string GetSql() => _clause.ToSql();

        /// <summary>
        /// 设置条件
        /// </summary>
        [Fact]
        public void TestWhere_1()
        {
            _clause.Where("Name", "a");
            Assert.Equal("Where \"Name\"=:p_0", GetSql());
        }
    }
}
