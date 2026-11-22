using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Moq;

namespace Bing.Dapper.Tests.Builders;

/// <summary>完整生成查询模板保留 Oracle 参数、别名和非法比较诊断。</summary>
public sealed class OracleGeneratedQueryTemplateTest
{
    [Fact]
    public void Template_ShouldReuseCompleteOracleSql_AndRejectInvalidComparison()
    {
        var types = new[] { typeof(Row) };
        var names = new[] { "r" };
        var column = new SqlGeneratedColumn(0, nameof(Row.Id), null, typeof(int));
        var template = new SqlGeneratedQueryTemplate(new[]
        {
            new SqlGeneratedQueryTemplate.Operation((query, arguments) => SqlGeneratedQueryBridge.OptionalCompare(query,
                true, false, types, names, null, column, null, (Operator)arguments[1], arguments[0]),
                arguments => arguments[0], arguments => (Operator)arguments[1], valueArgument: 0),
            new SqlGeneratedQueryTemplate.Operation((query, _) => SqlGeneratedQueryBridge.Project(query, "Select", types, names, null, new[] { column }))
        });
        var services = SqlBuilderServices.CreateDefault();
        SqlLambdaQuery Query(object value, Operator comparison)
        {
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new OracleBuilder(services)).From<Row>("r");
            SqlGeneratedQueryTemplate.Apply(query, template, 0, new object[] { value, comparison });
            return SqlGeneratedQueryTemplate.Apply(query, template, 1, Array.Empty<object>());
        }
        var first = Query(1, Operator.Equal).ToSql();
        Assert.Equal("Select \"r\".\"Id\" \r\nFrom \"AutoRows\" \"r\" \r\nWhere \"r\".\"Id\"=:p_0", first);
        Assert.Same(first, Query(2, Operator.Equal).ToSql());
        Assert.Throws<ArgumentNullException>(() => Query(null, Operator.Greater).ToSql());
    }

    [Table("AutoRows")]
    public sealed class Row { public int Id { get; set; } }
}
