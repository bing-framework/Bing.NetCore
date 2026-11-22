using System.Text.Json.Serialization;
using Bing.Dapper.PostgreSql;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>验证 PostgreSQL 使用源生成元数据转换 JSON 列。</summary>
public class PostgreSqlJsonConverterTest
{
    [Theory]
    [InlineData("json", true)]
    [InlineData("JSONB", false)]
    public void JsonColumn_RoundTripsWithEitherRegistrationOrder(string typeName, bool beforeProvider)
    {
        var services = new ServiceCollection();
        if (beforeProvider)
            services.AddPostgreSqlJson(PostgreSqlTestJsonContext.Default.JsonPayload);
        services.AddPostgreSqlProvider();
        if (!beforeProvider)
            services.AddPostgreSqlJson(PostgreSqlTestJsonContext.Default.JsonPayload);
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<IFieldValueConverterSelector>();
        var column = new ColumnMappingMetadata { ClrType = typeof(JsonPayload), ProviderTypeName = typeName };
        var context = Context(DatabaseType.PgSql);
        var payload = new JsonPayload { Items = new[] { 1, 2 } };

        var json = Assert.IsType<string>(selector.ConvertToProvider(payload, column, context));
        Assert.Equal("{\"items\":[1,2]}", json);
        Assert.Equal(payload.Items, Assert.IsType<JsonPayload>(selector.ConvertFromProvider(json, column, context)).Items);
        Assert.Null(selector.ConvertToProvider(null, column, context));
        Assert.Null(selector.ConvertFromProvider(null, column, context));
        Assert.Null(selector.ConvertFromProvider("null", column, context));
    }

    [Fact]
    public void Registration_LeavesTextOtherProvidersAndExplicitConvertersAlone()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();
        services.AddPostgreSqlJson(PostgreSqlTestJsonContext.Default.JsonPayload);
        services.AddPostgreSqlJson(PostgreSqlTestJsonContext.Default.String);
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<IFieldValueConverterSelector>();
        const string json = "{\"items\":[1,2]}";
        var jsonColumn = new ColumnMappingMetadata { ClrType = typeof(JsonPayload), ProviderTypeName = "jsonb" };
        var textColumn = new ColumnMappingMetadata { ClrType = typeof(JsonPayload), ProviderTypeName = "text" };
        var rawColumn = new ColumnMappingMetadata { ClrType = typeof(string), ProviderTypeName = "jsonb" };
        var customColumn = new ColumnMappingMetadata
        {
            ClrType = typeof(JsonPayload), ProviderTypeName = "jsonb", ConverterKind = FieldValueConverterKind.Custom
        };
        var postgres = Context(DatabaseType.PgSql);

        Assert.Same(json, selector.ConvertFromProvider(json, jsonColumn, Context(DatabaseType.SqlServer)));
        Assert.Same(json, selector.ConvertFromProvider(json, textColumn, postgres));
        Assert.Same(json, selector.ConvertToProvider(json, rawColumn, postgres));
        Assert.Same(json, selector.ConvertFromProvider(json, customColumn, postgres));
    }

    private static DatabaseContext Context(DatabaseType databaseType) => new()
    {
        DataSource = new SqlDataSourceDescriptor { DatabaseType = databaseType }
    };
}

internal sealed class JsonPayload
{
    [JsonPropertyName("items")]
    public int[] Items { get; set; }
}

[JsonSerializable(typeof(JsonPayload))]
[JsonSerializable(typeof(string))]
internal partial class PostgreSqlTestJsonContext : JsonSerializerContext { }
