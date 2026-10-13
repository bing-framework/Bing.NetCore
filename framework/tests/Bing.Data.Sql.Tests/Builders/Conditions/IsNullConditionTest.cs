using Bing.Data.Sql.Builders.Conditions;

namespace Bing.Data.Sql.Tests.Builders.Conditions;

/// <summary>
/// Is Null查询条件测试
/// </summary>
public class IsNullConditionTest
{
    /// <summary>
    /// 获取结果
    /// </summary>
    /// <param name="condition">要追加到缓冲区的 SQL 条件。</param>
    /// <returns>返回 SQL 条件生成的文本。</returns>
    private string GetResult(ISqlCondition condition)
    {
        var result = new StringBuilder();
        condition.AppendTo(result);
        return result.ToString();
    }

    /// <summary>
    /// 测试 - 获取条件
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var condition = new IsNullSqlCondition("Email");
        Assert.Equal("Email Is Null", GetResult(condition));
    }

    /// <summary>
    /// 测试 - 验证列为空
    /// </summary>
    [Fact]
    public void Test_Column_IsNull()
    {
        var condition = new IsNullSqlCondition("");
        Assert.Empty(GetResult(condition));
    }
}
