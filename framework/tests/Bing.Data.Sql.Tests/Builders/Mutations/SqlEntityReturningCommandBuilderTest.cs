using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Xunit;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// 整行实体 Returning 命令生成测试。
/// </summary>
public sealed class SqlEntityReturningCommandBuilderTest
{
    private const string SqliteInsertReturningSql =
        "Insert Into \u0060entity_returning_rows\u0060 (\u0060Id\u0060, \u0060Name\u0060, \u0060Version\u0060) " +
        "Values (@_p_0, @_p_1, @_p_2) Returning \u0060Id\u0060 As \u0060Id\u0060, " +
        "\u0060Name\u0060 As \u0060Name\u0060, \u0060Version\u0060 As \u0060Version\u0060";
    private const string SqliteUpdateReturningSql =
        "Update \u0060entity_returning_rows\u0060 Set \u0060Name\u0060 = @_p_0, \u0060Version\u0060 = @_p_1 " +
        "Where \u0060Id\u0060 = @_p_2 And \u0060Version\u0060 = @_p_3 Returning \u0060Id\u0060 As \u0060Id\u0060, " +
        "\u0060Name\u0060 As \u0060Name\u0060, \u0060Version\u0060 As \u0060Version\u0060";
    private const string SqliteDeleteReturningSql =
        "Delete From \u0060entity_returning_rows\u0060 Where \u0060Id\u0060 = @_p_0 And \u0060Version\u0060 = @_p_1 " +
        "Returning \u0060Id\u0060 As \u0060Id\u0060, \u0060Name\u0060 As \u0060Name\u0060, " +
        "\u0060Version\u0060 As \u0060Version\u0060";
    private const string PostgreSqlInsertReturningSql =
        "Insert Into \"entity_returning_rows\" (\"Id\", \"Name\", \"Version\") Values (@_p_0, @_p_1, @_p_2) " +
        "Returning \"Id\" As \"Id\", \"Name\" As \"Name\", \"Version\" As \"Version\"";
    private const string PostgreSqlUpdateReturningSql =
        "Update \"entity_returning_rows\" Set \"Name\" = @_p_0, \"Version\" = @_p_1 " +
        "Where \"Id\" = @_p_2 And \"Version\" = @_p_3 Returning \"Id\" As \"Id\", " +
        "\"Name\" As \"Name\", \"Version\" As \"Version\"";
    private const string PostgreSqlDeleteReturningSql =
        "Delete From \"entity_returning_rows\" Where \"Id\" = @_p_0 And \"Version\" = @_p_1 " +
        "Returning \"Id\" As \"Id\", \"Name\" As \"Name\", \"Version\" As \"Version\"";
    private const string SqlServerInsertReturningSql =
        "Insert Into [entity_returning_rows] ([Id], [Name], [Version]) " +
        "Output [Inserted].[Id] As [Id], [Inserted].[Name] As [Name], [Inserted].[Version] As [Version] " +
        "Values (@_p_0, @_p_1, @_p_2)";
    private const string SqlServerUpdateReturningSql =
        "Update [entity_returning_rows] Set [Name] = @_p_0, [Version] = @_p_1 " +
        "Output [Inserted].[Id] As [Id], [Inserted].[Name] As [Name], [Inserted].[Version] As [Version] " +
        "Where [Id] = @_p_2 And [Version] = @_p_3";
    private const string SqlServerDeleteReturningSql =
        "Delete From [entity_returning_rows] " +
        "Output [Deleted].[Id] As [Id], [Deleted].[Name] As [Name], [Deleted].[Version] As [Version] " +
        "Where [Id] = @_p_0 And [Version] = @_p_1";
    private const string SqliteInsertSql =
        "Insert Into \u0060entity_returning_rows\u0060 (\u0060Id\u0060, \u0060Name\u0060, \u0060Version\u0060) " +
        "Values (@_p_0, @_p_1, @_p_2)";

    /// <summary>各 Provider 的完整整行 Insert、Update、Delete SQL 和参数顺序。</summary>
    public static TheoryData<string, string, string, object[]> ReturningCases => new()
    {
        { "SQLite", "Insert", SqliteInsertReturningSql, new object[] { 7, "new", 6 } },
        { "SQLite", "Update", SqliteUpdateReturningSql, new object[] { "new", 6, 7, 4 } },
        { "SQLite", "Delete", SqliteDeleteReturningSql, new object[] { 7, 4 } },
        { "PostgreSQL", "Insert", PostgreSqlInsertReturningSql, new object[] { 7, "new", 6 } },
        { "PostgreSQL", "Update", PostgreSqlUpdateReturningSql, new object[] { "new", 6, 7, 4 } },
        { "PostgreSQL", "Delete", PostgreSqlDeleteReturningSql, new object[] { 7, 4 } },
        { "SQL Server", "Insert", SqlServerInsertReturningSql, new object[] { 7, "new", 6 } },
        { "SQL Server", "Update", SqlServerUpdateReturningSql, new object[] { "new", 6, 7, 4 } },
        { "SQL Server", "Delete", SqlServerDeleteReturningSql, new object[] { 7, 4 } }
    };

    /// <summary>
    /// 测试目的：默认映射返回全部列，并保留参数顺序、生成绑定和并发校验。
    /// </summary>
    [Theory]
    [MemberData(nameof(ReturningCases))]
    public void Returning_WhenDefaultShapeMatches_ShouldBuildGeneratedFullRowCommand(
        string providerName, string operationName, string expectedSql, object[] expectedValues)
    {
        var provider = GetProvider(providerName);
        var operation = ParseOperation(operationName);
        var command = BuildCommand(CreateBuilder(provider), operationName);

        Assert.Equal(expectedSql, command.Sql);
        Assert.Same(GetReturningSql(provider.Key, operation), command.Sql);
        Assert.Equal(expectedValues, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(operation, command.OperationKind);
        Assert.Equal(provider.Key, command.ProviderKey);
        Assert.True(command.HasReturning);
        Assert.Equal(operation != SqlOperationKind.InsertValues, command.ValidateAffectedRows);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>
    /// 测试目的：普通 Insert 继续使用原生成 SQL，不请求整行 Returning SQL。
    /// </summary>
    [Fact]
    public void Insert_WhenReturningFactoryIsConfigured_ShouldRemainOrdinaryGeneratedInsert()
    {
        var factoryCalls = 0;
        var builder = CreateBuilder(returningSql: (provider, operation) =>
        {
            factoryCalls++;
            return GetReturningSql(provider, operation);
        });

        var command = builder.Insert(new EntityReturningRow { Id = 7, Name = "new", Version = 6 });

        Assert.Equal(SqliteInsertSql, command.Sql);
        Assert.Same(SqliteInsertSql, command.Sql);
        Assert.Equal(new object[] { 7, "new", 6 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.False(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(0, factoryCalls);
    }

    /// <summary>
    /// 测试目的：写列筛选不缩窄返回投影，并回退到完整动态 SQL。
    /// </summary>
    [Fact]
    public void InsertReturning_WhenColumnsAreFiltered_ShouldReturnFullRowDynamically()
    {
        var factoryCalls = 0;
        var builder = CreateBuilder(returningSql: (provider, operation) =>
        {
            factoryCalls++;
            return GetReturningSql(provider, operation);
        });

        var command = builder.InsertReturning(new EntityReturningRow { Id = 7, Name = "new", Version = 6 },
            new SqlInsertOptions { IncludeProperties = new[] { nameof(EntityReturningRow.Name) } });

        Assert.Equal("Insert Into \u0060entity_returning_rows\u0060 (\u0060Name\u0060) Values (@_p_0) " +
                     "Returning \u0060Id\u0060 As \u0060Id\u0060, \u0060Name\u0060 As \u0060Name\u0060, " +
                     "\u0060Version\u0060 As \u0060Version\u0060", command.Sql);
        Assert.Equal(new object[] { "new" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(0, factoryCalls);
    }

    /// <summary>
    /// 测试目的：最终表列映射覆盖模型时应渲染覆盖后的完整返回 SQL。
    /// </summary>
    [Fact]
    public void InsertReturning_WhenMappingChanges_ShouldRenderMappedFullRow()
    {
        var factoryCalls = 0;
        var builder = CreateBuilder(tableName: "mapped_entity_rows", nameColumnName: "mapped_name",
            returningSql: (provider, operation) =>
            {
                factoryCalls++;
                return GetReturningSql(provider, operation);
            });

        var command = builder.InsertReturning(new EntityReturningRow { Id = 7, Name = "mapped", Version = 6 });

        Assert.Equal("Insert Into \u0060mapped_entity_rows\u0060 " +
                     "(\u0060Id\u0060, \u0060mapped_name\u0060, \u0060Version\u0060) " +
                     "Values (@_p_0, @_p_1, @_p_2) Returning \u0060Id\u0060 As \u0060Id\u0060, " +
                     "\u0060mapped_name\u0060 As \u0060Name\u0060, \u0060Version\u0060 As \u0060Version\u0060",
            command.Sql);
        Assert.Equal(new object[] { 7, "mapped", 6 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.HasReturning);
        Assert.Equal(0, factoryCalls);
    }

    /// <summary>
    /// 测试目的：写入数据边界必须保留在动态 Delete Returning 条件中。
    /// </summary>
    [Fact]
    public void DeleteReturning_WhenDataBoundaryApplies_ShouldRenderBoundaryAndFullRow()
    {
        var factoryCalls = 0;
        var builder = CreateBuilder(filters: new ISqlFilter[] { new ReturningBoundaryFilter() },
            returningSql: (provider, operation) =>
            {
                factoryCalls++;
                return GetReturningSql(provider, operation);
            });

        var command = builder.DeleteReturning(new EntityReturningRow { Id = 7, Name = "tenant-a", Version = 4 });

        Assert.Equal("Delete From \u0060entity_returning_rows\u0060 Where \u0060Id\u0060 = @_p_0 " +
                     "And \u0060Version\u0060 = @_p_1 And \u0060Name\u0060=@_p_2 " +
                     "Returning \u0060Id\u0060 As \u0060Id\u0060, \u0060Name\u0060 As \u0060Name\u0060, " +
                     "\u0060Version\u0060 As \u0060Version\u0060", command.Sql);
        Assert.Equal(new object[] { 7, 4, "tenant-a" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.HasReturning);
        Assert.True(command.ValidateAffectedRows);
        Assert.Equal(1, factoryCalls);
    }

    /// <summary>
    /// 测试目的：逻辑删除 Returning 返回更新后的状态列，并保留静态参数绑定和并发校验。
    /// </summary>
    [Fact]
    public void DeleteReturning_WhenEntityIsSoftDelete_ShouldReturnUpdatedState()
    {
        var builder = CreateBuilder(CreateSoftDeleteModel());

        var command = builder.DeleteReturning(new SoftDeleteReturningRow
        {
            Id = 7,
            Name = "soft",
            Version = 4,
            IsDeleted = false
        });

        Assert.Equal("Update \u0060soft_delete_returning_rows\u0060 Set \u0060IsDeleted\u0060 = @_p_0 " +
                     "Where \u0060Id\u0060 = @_p_1 And \u0060Version\u0060 = @_p_2 And \u0060IsDeleted\u0060=@_p_3 " +
                     "Returning \u0060Id\u0060 As \u0060Id\u0060, \u0060Name\u0060 As \u0060Name\u0060, " +
                     "\u0060Version\u0060 As \u0060Version\u0060, \u0060IsDeleted\u0060 As \u0060IsDeleted\u0060",
            command.Sql);
        Assert.Equal(new object[] { true, 7, 4, false }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(SqlOperationKind.Update, command.OperationKind);
        Assert.True(command.HasReturning);
        Assert.True(command.ValidateAffectedRows);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>
    /// 测试目的：不支持结果集 Returning 的 Provider 应拒绝命令。
    /// </summary>
    [Theory]
    [InlineData("MySQL")]
    [InlineData("Oracle")]
    public void InsertReturning_WhenProviderDoesNotSupportResults_ShouldThrow(string providerName)
    {
        var builder = CreateBuilder(GetProvider(providerName));

        Assert.Throws<NotSupportedException>(() => builder.InsertReturning(
            new EntityReturningRow { Id = 7, Name = "new", Version = 6 }));
    }

    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder(ISqlProvider provider = null,
        string tableName = null, string nameColumnName = null, IEnumerable<ISqlFilter> filters = null,
        Func<string, SqlOperationKind, string> returningSql = null) =>
        CreateBuilder(CreateModel(returningSql ?? GetReturningSql), provider, tableName, nameColumnName, filters);

    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder(EntityModelMetadata model,
        ISqlProvider provider = null, string tableName = null, string nameColumnName = null,
        IEnumerable<ISqlFilter> filters = null)
    {
        var metadata = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        var mapping = new EntityMappingOptions { EntityType = model.EntityType, TableName = tableName ?? model.TableName };
        if (nameColumnName != null)
        {
            mapping.Columns[nameof(EntityReturningRow.Name)] = new ColumnMappingOptions
            {
                PropertyName = nameof(EntityReturningRow.Name),
                ColumnName = nameColumnName
            };
        }
        metadata.EntityMappings.Add(mapping);
        var services = new SqlBuilderServices(metadataOptions: metadata,
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model), filters: filters);
        return new DefaultSqlEntityMutationCommandBuilder(provider ?? SqliteSqlProvider.Instance, services);
    }

    private static EntityModelMetadata CreateModel(Func<string, SqlOperationKind, string> returningSql) =>
        new(typeof(EntityReturningRow), "entity_returning_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(EntityReturningRow.Id), typeof(int), true, true,
                isKey: true, generatedGetter: source => ((EntityReturningRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(EntityReturningRow.Name), typeof(string), true, true,
                generatedGetter: source => ((EntityReturningRow)source).Name),
            EntityPropertyMetadata.CreateGenerated(nameof(EntityReturningRow.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: source => ((EntityReturningRow)source).Version)
        }, () => new EntityReturningRow(), (source, propertyName) => propertyName switch
        {
            nameof(EntityReturningRow.Id) => ((EntityReturningRow)source).Id,
            nameof(EntityReturningRow.Name) => ((EntityReturningRow)source).Name,
            nameof(EntityReturningRow.Version) => ((EntityReturningRow)source).Version,
            _ => null
        })
        {
            GeneratedMutationSql = GetOrdinarySql,
            GeneratedMutationReturningSql = returningSql
        };

    private static EntityModelMetadata CreateSoftDeleteModel() =>
        new(typeof(SoftDeleteReturningRow), "soft_delete_returning_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(SoftDeleteReturningRow.Id), typeof(int), true, true,
                isKey: true, generatedGetter: source => ((SoftDeleteReturningRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(SoftDeleteReturningRow.Name), typeof(string), true, true,
                generatedGetter: source => ((SoftDeleteReturningRow)source).Name),
            EntityPropertyMetadata.CreateGenerated(nameof(SoftDeleteReturningRow.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: source => ((SoftDeleteReturningRow)source).Version),
            EntityPropertyMetadata.CreateGenerated(nameof(SoftDeleteReturningRow.IsDeleted), typeof(bool), true, true,
                generatedGetter: source => ((SoftDeleteReturningRow)source).IsDeleted)
        }, () => new SoftDeleteReturningRow(), (source, propertyName) => propertyName switch
        {
            nameof(SoftDeleteReturningRow.Id) => ((SoftDeleteReturningRow)source).Id,
            nameof(SoftDeleteReturningRow.Name) => ((SoftDeleteReturningRow)source).Name,
            nameof(SoftDeleteReturningRow.Version) => ((SoftDeleteReturningRow)source).Version,
            nameof(SoftDeleteReturningRow.IsDeleted) => ((SoftDeleteReturningRow)source).IsDeleted,
            _ => null
        })
        {
            GeneratedMutationReturningSql = (provider, operation) =>
                provider == SqliteSqlProvider.Instance.Key && operation == SqlOperationKind.Update
                    ? "Update \u0060soft_delete_returning_rows\u0060 Set \u0060IsDeleted\u0060 = @_p_0 " +
                      "Where \u0060Id\u0060 = @_p_1 And \u0060Version\u0060 = @_p_2 " +
                      "Returning \u0060Id\u0060 As \u0060Id\u0060, \u0060Name\u0060 As \u0060Name\u0060, " +
                      "\u0060Version\u0060 As \u0060Version\u0060, \u0060IsDeleted\u0060 As \u0060IsDeleted\u0060"
                    : null
        };

    private static string GetOrdinarySql(string provider, SqlOperationKind operation) =>
        provider == SqliteSqlProvider.Instance.Key && operation == SqlOperationKind.InsertValues
            ? SqliteInsertSql
            : null;

    private static string GetReturningSql(string provider, SqlOperationKind operation) =>
        (provider, operation) switch
        {
            ("bing.sqlite", SqlOperationKind.InsertValues) => SqliteInsertReturningSql,
            ("bing.sqlite", SqlOperationKind.Update) => SqliteUpdateReturningSql,
            ("bing.sqlite", SqlOperationKind.Delete) => SqliteDeleteReturningSql,
            ("bing.postgresql", SqlOperationKind.InsertValues) => PostgreSqlInsertReturningSql,
            ("bing.postgresql", SqlOperationKind.Update) => PostgreSqlUpdateReturningSql,
            ("bing.postgresql", SqlOperationKind.Delete) => PostgreSqlDeleteReturningSql,
            ("bing.sqlserver", SqlOperationKind.InsertValues) => SqlServerInsertReturningSql,
            ("bing.sqlserver", SqlOperationKind.Update) => SqlServerUpdateReturningSql,
            ("bing.sqlserver", SqlOperationKind.Delete) => SqlServerDeleteReturningSql,
            _ => null
        };

    private static SqlOperationKind ParseOperation(string operationName) => operationName switch
    {
        "Insert" => SqlOperationKind.InsertValues,
        "Update" => SqlOperationKind.Update,
        "Delete" => SqlOperationKind.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(operationName), operationName, null)
    };

    private static SqlWriteCommand BuildCommand(DefaultSqlEntityMutationCommandBuilder builder, string operationName)
    {
        var entity = new EntityReturningRow { Id = 7, Name = "new", Version = 6 };
        return operationName switch
        {
            "Insert" => builder.InsertReturning(entity),
            "Update" => builder.UpdateReturning(entity,
                new SqlUpdateOptions<EntityReturningRow>().Original(row => row.Version, 4)),
            "Delete" => builder.DeleteReturning(entity,
                new SqlDeleteOptions<EntityReturningRow>().Original(row => row.Version, 4)),
            _ => throw new ArgumentOutOfRangeException(nameof(operationName), operationName, null)
        };
    }

    private static ISqlProvider GetProvider(string providerName) => providerName switch
    {
        "SQLite" => SqliteSqlProvider.Instance,
        "PostgreSQL" => PostgreSqlSqlProvider.Instance,
        "SQL Server" => SqlServerSqlProvider.Instance,
        "MySQL" => MySqlSqlProvider.Instance,
        "Oracle" => new OracleReturningUnsupportedProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null)
    };

    private sealed class EntityReturningRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }

    private sealed class SoftDeleteReturningRow : ISoftDelete
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
        public bool IsDeleted { get; set; }
    }

    private sealed class ReturningBoundaryFilter : ISqlFilter, ISqlDataBoundaryContributor
    {
        public void Filter(SqlFilterContext context)
        {
        }

        public bool ShouldApply(SqlDataBoundaryContext context) =>
            context.EntityType == typeof(EntityReturningRow) && context.Operation == SqlDataBoundaryOperation.Purge;

        public void Apply(SqlDataBoundaryContext context) =>
            context.AddEquals(nameof(EntityReturningRow.Name), "tenant-a");
    }

    private sealed class OracleReturningUnsupportedProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        private static MySqlSqlProvider Inner => MySqlSqlProvider.Instance;

        public string Key => "bing.oracle";
        public DatabaseType DatabaseType => DatabaseType.Oracle;
        public IDialect Dialect => Inner.Dialect;
        public ISqlClauseFactory ClauseFactory => Inner.ClauseFactory;
        public ISqlTableReferenceParser TableReferenceParser => Inner.TableReferenceParser;
        public ISqlPaginationRenderer PaginationRenderer => Inner.PaginationRenderer;
        public IParameterManagerFactory ParameterManagerFactory => Inner.ParameterManagerFactory;
        public IParamLiteralsResolver ParamLiteralsResolver => Inner.ParamLiteralsResolver;
        public SqlProviderProfile Profile => Inner.Profile;
    }
}
