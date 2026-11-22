using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.SqlClient;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>测试 SQL Server 特有参数类型在驱动上的绑定。</summary>
public sealed class SqlServerDbParameterCustomizerTest
{
    /// <summary>显式声明 MAX 时，短值及 SQL NULL 也应使用无长度上限的驱动参数。</summary>
    [Theory]
    [InlineData("nvarchar(max)", SqlDbType.NVarChar, "字")]
    [InlineData(" VARCHAR ( MAX ) ", SqlDbType.VarChar, "a")]
    [InlineData("varbinary(max)", SqlDbType.VarBinary, null)]
    public void Configure_WhenColumnDeclaresMax_ShouldKeepUnboundedParameter(string typeName, SqlDbType type, string value)
    {
        var parameter = new SqlParameter { Value = (object)value ?? DBNull.Value };
        var source = new SqlParam("value", value) { ProviderTypeName = typeName, DatabaseType = DatabaseType.SqlServer };

        new SqlServerDbParameterCustomizer().Configure(parameter, source);

        Assert.Equal(type, parameter.SqlDbType);
        Assert.Equal(-1, parameter.Size);
        Assert.Equal((object)value ?? DBNull.Value, parameter.Value);
    }

    /// <summary>普通长度声明保持驱动的默认长度推断。</summary>
    [Fact]
    public void Configure_WhenColumnDeclaresFixedLength_ShouldKeepInferredParameterSize()
    {
        var parameter = new SqlParameter { Value = "abc" };
        var source = new SqlParam("value", "abc") { ProviderTypeName = "nvarchar(40)" };

        new SqlServerDbParameterCustomizer().Configure(parameter, source);

        Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
        Assert.Equal(3, parameter.Size);
    }

    /// <summary>显式提供的参数长度应继续生效。</summary>
    [Theory]
    [InlineData("nvarchar(40)")]
    [InlineData("nvarchar(max)")]
    public void Configure_WhenParameterSizeIsExplicit_ShouldPreserveIt(string typeName)
    {
        var parameter = new SqlParameter { Value = "abc", Size = 40 };
        var source = new SqlParam("value", "abc", size: 40) { ProviderTypeName = typeName };

        new SqlServerDbParameterCustomizer().Configure(parameter, source);

        Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
        Assert.Equal(40, parameter.Size);
    }

    /// <summary>离线生成命令保留的 SQL 类型名应直接决定 sql_variant 参数类型。</summary>
    [Theory]
    [InlineData("sql_variant", 42)]
    [InlineData(" SQL_VARIANT ", "短值")]
    [InlineData("sql_variant", null)]
    public void Configure_WhenColumnDeclaresSqlVariant_ShouldNotInferTypeFromValue(string typeName, object value)
    {
        var parameter = new SqlParameter { Value = value ?? DBNull.Value };
        var source = new SqlParam("value", value) { ProviderTypeName = typeName };

        new SqlServerDbParameterCustomizer().Configure(parameter, source);

        Assert.Equal(SqlDbType.Variant, parameter.SqlDbType);
        Assert.Equal(value ?? DBNull.Value, parameter.Value);
    }
}
