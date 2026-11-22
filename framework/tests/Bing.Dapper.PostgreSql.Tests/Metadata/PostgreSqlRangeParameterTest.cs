using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Npgsql;
using NpgsqlTypes;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>验证 PostgreSQL 才能表达的范围数据类型。</summary>
public class PostgreSqlRangeParameterTest
{
    [Theory]
    [InlineData("int4range", NpgsqlDbType.IntegerRange)]
    [InlineData("INT4MULTIRANGE", NpgsqlDbType.IntegerMultirange)]
    [InlineData("int8range", NpgsqlDbType.BigIntRange)]
    [InlineData("int8multirange", NpgsqlDbType.BigIntMultirange)]
    [InlineData("numrange", NpgsqlDbType.NumericRange)]
    [InlineData("nummultirange", NpgsqlDbType.NumericMultirange)]
    [InlineData("daterange", NpgsqlDbType.DateRange)]
    [InlineData("datemultirange", NpgsqlDbType.DateMultirange)]
    [InlineData("tsrange", NpgsqlDbType.TimestampRange)]
    [InlineData("tsmultirange", NpgsqlDbType.TimestampMultirange)]
    [InlineData("tstzrange", NpgsqlDbType.TimestampTzRange)]
    [InlineData("tstzmultirange", NpgsqlDbType.TimestampTzMultirange)]
    public void Configure_WithRangeTypeNameAndNull_ShouldKeepNativeParameterType(string typeName, NpgsqlDbType expected)
    {
        var parameter = new NpgsqlParameter { Value = DBNull.Value };
        new PostgreSqlDbParameterCustomizer().Configure(parameter, new SqlParam("value", null) { ProviderTypeName = typeName });
        Assert.Equal(expected, parameter.NpgsqlDbType);
        Assert.Same(DBNull.Value, parameter.Value);
    }

    [Theory]
    [InlineData("int4range")]
    [InlineData("INT4MULTIRANGE")]
    [InlineData("int8range")]
    [InlineData("int8multirange")]
    [InlineData("numrange")]
    [InlineData("nummultirange")]
    [InlineData("daterange")]
    [InlineData("datemultirange")]
    [InlineData("tsrange")]
    [InlineData("tsmultirange")]
    [InlineData("tstzrange")]
    [InlineData("tstzmultirange")]
    public void ToDbType_WithRangeTypeName_ShouldLeaveTypeToProvider(string typeName)
    {
        Assert.Null(new PostgreSqlTypeConverter().ToDbType(typeName));
    }
}
