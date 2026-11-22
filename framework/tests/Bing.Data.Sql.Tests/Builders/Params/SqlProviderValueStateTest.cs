using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Moq;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>字段转换状态在参数复制和查询快照之间的合同测试。</summary>
public sealed class SqlProviderValueStateTest
{
    [Fact]
    public void Factory_ShouldConvertOnceAndMarkFinalValue()
    {
        var column = new ColumnMappingMetadata { ClrType = typeof(int), PropertyName = "Value" };
        var converters = new Mock<IFieldValueConverterSelector>(MockBehavior.Strict);
        converters.Setup(item => item.ConvertToProvider(7, column, It.IsAny<DatabaseContext>())).Returns("converted");
        var parameter = new DefaultSqlParameterFactory(converters.Object).Create("value", 7, column, null);
        Assert.Equal("converted", parameter.Value);
        Assert.Equal(7, parameter.OriginalValue);
        Assert.True(parameter.IsProviderValue);
        converters.Verify(item => item.ConvertToProvider(7, column, It.IsAny<DatabaseContext>()), Times.Once);
        parameter.Value = 9;
        Assert.False(parameter.IsProviderValue);
    }

    [Fact]
    public void ManagerCloneAndQuerySnapshot_ShouldPreserveIndependentConversionState()
    {
        var parameter = new SqlParam("@value", "converted") { IsProviderValue = true };
        var manager = new ParameterManager(SqlServerSqlProvider.Instance.Dialect);
        manager.Add(parameter);
        var clone = (IAdvancedParameterManager)manager.Clone();
        var snapshot = Assert.IsAssignableFrom<IEnumerable<SqlParam>>(SqlQueryPlan.Create("select @value", new[] { parameter }).Parameters);
        var frozen = Assert.Single(snapshot);
        Assert.True(clone.GetSqlParams()["@value"].IsProviderValue);
        Assert.True(frozen.IsProviderValue);
        frozen.Value = "changed";
        Assert.False(frozen.IsProviderValue);
        Assert.True(parameter.IsProviderValue);
        Assert.True(manager.GetSqlParams()["@value"].IsProviderValue);
    }
}
