using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Npgsql;
using NpgsqlTypes;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// 测试目的：验证 Dapper 参数绑定器会把增强参数元数据写入实际的 ADO.NET 参数对象。
/// </summary>
public class DefaultSqlParameterBinderTest
{
    /// <summary>
    /// 测试 - PostgreSQL 组合 Provider 类型应写入实际参数的 NpgsqlDbType。
    /// </summary>
    [Fact]
    public void PostgreSqlDbParameterCustomizer_WithCombinedFlags_ShouldConfigureNpgsqlDbType()
    {
        // Arrange
        var customizer = new PostgreSqlDbParameterCustomizer();
        var parameter = new NpgsqlParameter();
        var sqlParameter = new SqlParam("ids", Array.Empty<Guid>())
        {
            ProviderTypeName = "Array | Uuid"
        };

        // Act
        customizer.Configure(parameter, sqlParameter);

        // Assert
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Uuid, parameter.NpgsqlDbType);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 参数类型名称包含精度声明时，应按基础类型配置 Npgsql 参数。
    /// </summary>
    [Fact]
    public void PostgreSqlDbParameterCustomizer_WithPrecisionTypeName_ShouldConfigureNumericType()
    {
        // Arrange
        var customizer = new PostgreSqlDbParameterCustomizer();
        var parameter = new NpgsqlParameter();
        var sqlParameter = new SqlParam("amount", 123.456m)
        {
            ProviderTypeName = "numeric(24,6)"
        };

        // Act
        customizer.Configure(parameter, sqlParameter);

        // Assert
        Assert.Equal(NpgsqlDbType.Numeric, parameter.NpgsqlDbType);
    }

    /// <summary>
    /// 测试目的：PostgreSQL JSONB 类型名称应配置 Npgsql 的 Jsonb 参数类型。
    /// </summary>
    [Fact]
    public void PostgreSqlDbParameterCustomizer_WithJsonbTypeName_ShouldConfigureJsonbType()
    {
        // Arrange
        var customizer = new PostgreSqlDbParameterCustomizer();
        var parameter = new NpgsqlParameter();
        var sqlParameter = new SqlParam("payload", "{\"enabled\":true}")
        {
            ProviderTypeName = "jsonb"
        };

        // Act
        customizer.Configure(parameter, sqlParameter);

        // Assert
        Assert.Equal(NpgsqlDbType.Jsonb, parameter.NpgsqlDbType);
    }

    /// <summary>
    /// 测试目的：无法识别的 Provider 类型名称不应覆盖 Npgsql 参数的既有类型。
    /// </summary>
    [Fact]
    public void PostgreSqlDbParameterCustomizer_WithUnknownTypeName_ShouldKeepExistingType()
    {
        // Arrange
        var customizer = new PostgreSqlDbParameterCustomizer();
        var parameter = new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text };
        var sqlParameter = new SqlParam("value", "test")
        {
            ProviderTypeName = "unknown_type"
        };

        // Act
        customizer.Configure(parameter, sqlParameter);

        // Assert
        Assert.Equal(NpgsqlDbType.Text, parameter.NpgsqlDbType);
    }

}
