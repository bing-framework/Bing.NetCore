using Bing.Data.Sql.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Data.Test.Integration.Sql.Builders.SqlServer.Conditions;

/// <summary>
/// Sql大于查询条件测试
/// </summary>
public class GreaterConditionTest:TestBase
{
    public GreaterConditionTest(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// 获取条件
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var condition = new GreaterCondition("Age", "@Age");
        Assert.Equal("Age>@Age", condition.GetCondition());
    }
}