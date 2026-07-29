using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// SQL 子句公共合同测试。
/// </summary>
public class SqlClauseContractTest
{
    /// <summary>
    /// 测试目的：通用 SQL 子句合同必须继承内容追加能力，并保留布尔验证方法。
    /// </summary>
    [Fact]
    public void ISqlClause_ShouldInheritSqlContentAndDeclareBooleanValidate()
    {
        // Arrange
        var validate = typeof(ISqlClause).GetMethod(nameof(ISqlClause.Validate));

        // Act
        var inheritsSqlContent = typeof(ISqlContent).IsAssignableFrom(typeof(ISqlClause));

        // Assert
        Assert.True(inheritsSqlContent);
        Assert.NotNull(validate);
        Assert.Equal(typeof(bool), validate.ReturnType);
        Assert.Empty(validate.GetParameters());
    }
}