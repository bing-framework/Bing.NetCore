using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>验证显式类型处理适配与 Provider 参数配置可同时生效。</summary>
public sealed class SqlTypeHandlerAdapterTest
{
    [Fact]
    public void Adapter_ShouldParseNullableColumnAndConfigureRealParameterAfterProvider()
    {
        var adapter = new SqlTypeHandlerAdapter<int>(value => Convert.ToInt32(value) + 1,
            (parameter, value) => { parameter.Value = value * 2; parameter.Size = 23; });
        Assert.True(adapter.CanConvert(new ColumnMappingMetadata { ClrType = typeof(int?) }, null));
        Assert.False(adapter.CanConvert(new ColumnMappingMetadata { ClrType = typeof(string) }, null));
        Assert.Equal(8, adapter.ConvertFromProvider(7L, null, null));
        Assert.Equal(DBNull.Value, adapter.ConvertFromProvider(DBNull.Value, null, null));
        Assert.Equal(7, adapter.ConvertToProvider(7, null, null));

        var parameter = new Mock<IDbDataParameter>();
        parameter.SetupAllProperties();
        var provider = new Mock<ISqlDbParameterCustomizer>();
        provider.Setup(item => item.CanHandle(It.IsAny<DatabaseType>())).Returns(true);
        provider.Setup(item => item.Configure(parameter.Object, It.IsAny<SqlParam>()))
            .Callback(() => parameter.Object.DbType = DbType.Int32);
        var command = new Mock<IDbCommand>();
        command.Setup(item => item.CreateParameter()).Returns(parameter.Object);
        command.SetupGet(item => item.Parameters).Returns(new Mock<IDataParameterCollection>().Object);
        var binder = new DefaultSqlParameterBinder(dbParameterCustomizers: new ISqlDbParameterCustomizer[] { adapter, provider.Object });
        var bound = (ISqlStaticParameterSource)binder.Bind(new[] { new SqlParam("Id", 7) });
        bound.BindTo(command.Object);

        provider.Verify(item => item.Configure(parameter.Object, It.IsAny<SqlParam>()), Times.Once);
        Assert.Equal(DbType.Int32, parameter.Object.DbType);
        Assert.Equal(14, parameter.Object.Value);
        Assert.Equal(23, parameter.Object.Size);
    }

    [Fact]
    public void ScalarReader_ShouldUseExplicitParserAndKeepNullDefault()
    {
        var adapter = new SqlTypeHandlerAdapter<string>(value => "parsed:" + value, (parameter, value) => parameter.Value = value);
        var mapper = new SqlStaticResultReader<string>(null, new DefaultFieldValueConverterSelector(new[] { adapter }), new DatabaseContext());
        var table = new DataTable();
        table.Columns.Add("value", typeof(string));
        table.Rows.Add("one");
        table.Rows.Add(DBNull.Value);
        using var reader = table.CreateDataReader();
        var read = mapper.Bind(reader);
        Assert.True(reader.Read());
        Assert.Equal("parsed:one", read());
        Assert.True(reader.Read());
        Assert.Null(read());
    }

    /// <summary>转换器明确返回 null 时应保留转换结果，不能改回原始数据库值。</summary>
    [Fact]
    public void ScalarReader_WhenParserReturnsNull_ShouldPreserveNull()
    {
        var adapter = new SqlTypeHandlerAdapter<string>(_ => null, (parameter, value) => parameter.Value = value);
        var mapper = new SqlStaticResultReader<string>(null, new DefaultFieldValueConverterSelector(new[] { adapter }), new DatabaseContext());
        var table = new DataTable();
        table.Columns.Add("value", typeof(string));
        table.Rows.Add("redacted");
        using var reader = table.CreateDataReader();
        var read = mapper.Bind(reader);
        Assert.True(reader.Read());
        Assert.Null(read());
    }
}
