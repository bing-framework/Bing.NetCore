using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>
/// 验证生成式单实体 CRUD SQL 的复用和运行时回退边界。
/// </summary>
public sealed class GeneratedMutationSqlCommandTest
{
    private const string GeneratedInsertSql =
        "Insert Into `generated_mutation_rows` (`Id`, `Name`, `Version`) Values (@_p_0, @_p_1, @_p_2)";
    private const string GeneratedUpdateSql =
        "Update `generated_mutation_rows` Set `Name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3";
    private const string GeneratedPurgeSql =
        "Delete From `generated_mutation_rows` Where `Id` = @_p_0 And `Version` = @_p_1";
    private const string SingleInsertSql = "Insert Into `generated_mutation_rows` (`Name`) Values (@_p_0)";
    private const string SingleUpdateSql = "Update `generated_mutation_rows` Set `Name` = @_p_0 Where `Id` = @_p_1 And `Version` = @_p_2";
    private const string DoubleInsertSql = "Insert Into `generated_mutation_rows` (`Id`, `Name`, `Version`) Values (@_p_0, @_p_1, @_p_2), (@_p_3, @_p_4, @_p_5)";

    /// <summary>Include/Exclude 表达的同一单列形状复用常量，参数仍来自本次实体和原始并发值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleColumnUpdate_ShouldReuseSqlAndPreserveOriginalConcurrency(bool exclude)
    {
        var options = new SqlUpdateOptions<GeneratedMutationRow>
        {
            IncludeProperties = exclude ? null : new[] { "Name" },
            ExcludeProperties = exclude ? new[] { "Version" } : null
        }.Original(row => row.Version, 3);
        var command = CreateBuilder().Update(new GeneratedMutationRow { Id = 7, Name = "new", Version = 4 }, options);
        Assert.Equal(SingleUpdateSql, command.Sql);
        Assert.Same(SingleUpdateSql, command.Sql);
        Assert.Equal(new object[] { "new", 7, 3 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.IsGeneratedExecution);
        Assert.True(command.ValidateAffectedRows);
    }

    [Fact]
    public void SingleColumnInsert_ShouldReuseSqlAndBindCurrentValue()
    {
        var command = CreateBuilder().Insert(new GeneratedMutationRow { Name = "one" },
            new SqlInsertOptions { IncludeProperties = new[] { "Name" } });
        Assert.Equal(SingleInsertSql, command.Sql);
        Assert.Same(SingleInsertSql, command.Sql);
        Assert.Equal("one", Assert.Single(command.Parameters).Value);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>双行只优化完整默认列；其他行数和列形状保持动态渲染与静态绑定。</summary>
    [Fact]
    public void CombinedInsert_ShouldReuseOnlyDeclaredDefaultShape()
    {
        var builder = CreateBuilder();
        var rows = new[] { new GeneratedMutationRow { Id = 1, Name = "first", Version = 3 },
            new GeneratedMutationRow { Id = 2, Name = "second", Version = 4 } };
        var command = builder.InsertCombined(rows);
        Assert.Equal(DoubleInsertSql, command.Sql);
        Assert.Same(DoubleInsertSql, command.Sql);
        Assert.Equal(new object[] { 1, "first", 3, 2, "second", 4 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.IsGeneratedExecution);
        var filtered = builder.InsertCombined(rows, new SqlInsertOptions { IncludeProperties = new[] { "Name" } });
        Assert.Equal("Insert Into `generated_mutation_rows` (`Name`) Values (@_p_0), (@_p_1)", filtered.Sql);
        Assert.Equal(new object[] { "first", "second" }, filtered.Parameters.Select(parameter => parameter.Value));
        var triple = builder.InsertCombined(new[] { rows[0], rows[1], rows[0] });
        Assert.Equal(DoubleInsertSql + ", (@_p_6, @_p_7, @_p_8)", triple.Sql);
        Assert.True(triple.IsGeneratedExecution);
    }

    [Theory]
    [InlineData("mapped_rows", null, "Update `mapped_rows` Set `Name` = @_p_0 Where `Id` = @_p_1 And `Version` = @_p_2")]
    [InlineData(null, "mapped_name", "Update `generated_mutation_rows` Set `mapped_name` = @_p_0 Where `Id` = @_p_1 And `Version` = @_p_2")]
    public void SingleColumnUpdate_WhenFinalMappingChanges_ShouldRenderMappedSql(string table, string column, string expected)
    {
        var command = CreateBuilder(tableName: table, nameColumnName: column).Update(
            new GeneratedMutationRow { Id = 8, Name = "mapped", Version = 2 },
            new SqlUpdateOptions { IncludeProperties = new[] { "Name" } });
        Assert.Equal(expected, command.Sql);
        Assert.NotSame(SingleUpdateSql, command.Sql);
        Assert.Equal(new object[] { "mapped", 8, 2 }, command.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void CombinedInsert_WhenFinalMappingChanges_ShouldRenderMappedSql()
    {
        var command = CreateBuilder(tableName: "mapped_rows").InsertCombined(new[]
        {
            new GeneratedMutationRow { Id = 1, Name = "a", Version = 2 },
            new GeneratedMutationRow { Id = 2, Name = "b", Version = 3 }
        });
        Assert.Equal("Insert Into `mapped_rows` (`Id`, `Name`, `Version`) Values (@_p_0, @_p_1, @_p_2), (@_p_3, @_p_4, @_p_5)", command.Sql);
        Assert.NotSame(DoubleInsertSql, command.Sql);
    }

    [Fact]
    public void FilteredUpdate_WhenConcurrent_ShouldIsolateValuesAcrossSharedPlans()
    {
        var builder = CreateBuilder();
        Parallel.For(0, 24, index =>
        {
            var command = builder.Update(new GeneratedMutationRow { Id = index, Name = $"value-{index}", Version = index },
                new SqlUpdateOptions { IncludeProperties = new[] { "Name" } });
            Assert.Equal(SingleUpdateSql, command.Sql);
            Assert.Same(SingleUpdateSql, command.Sql);
            Assert.Equal(new object[] { $"value-{index}", index, index }, command.Parameters.Select(parameter => parameter.Value));
        });
    }

    /// <summary>
    /// 测试目的：最终 SQLite 映射与生成模型一致时，Insert 应复用生成 SQL 字符串并保留生成绑定器。
    /// </summary>
    [Fact]
    public void Insert_WhenGeneratedSqlMatchesSqliteMapping_ShouldReuseConstantAndParameters()
    {
        var command = CreateBuilder().Insert(new GeneratedMutationRow { Id = 7, Name = "created", Version = 3 });

        Assert.Equal(GeneratedInsertSql, command.Sql);
        Assert.Same(GeneratedInsertSql, command.Sql);
        Assert.Equal(new object[] { 7, "created", 3 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(SqlOperationKind.InsertValues, command.OperationKind);
        Assert.Equal(SqliteSqlProvider.Instance.Key, command.ProviderKey);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>
    /// 测试目的：Update 应复用生成 SQL，同时使用并发原始值并保留受影响行数校验。
    /// </summary>
    [Fact]
    public void Update_WhenGeneratedSqlMatchesSqliteMapping_ShouldReuseConstantAndConcurrencyMetadata()
    {
        var entity = new GeneratedMutationRow { Id = 42, Name = "updated", Version = 8 };
        var options = new SqlUpdateOptions<GeneratedMutationRow>().Original(row => row.Version, 5);

        var command = CreateBuilder().Update(entity, options);

        Assert.Equal(GeneratedUpdateSql, command.Sql);
        Assert.Same(GeneratedUpdateSql, command.Sql);
        Assert.Equal(new object[] { "updated", 8, 42, 5 },
            command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(SqlOperationKind.Update, command.OperationKind);
        Assert.True(command.ValidateAffectedRows);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>
    /// 测试目的：Purge 应复用生成 Delete SQL，同时保留主键、并发条件和受影响行数校验。
    /// </summary>
    [Fact]
    public void Purge_WhenGeneratedSqlMatchesSqliteMapping_ShouldReuseConstantAndConcurrencyMetadata()
    {
        var command = CreateBuilder().Purge(new GeneratedMutationRow { Id = 12, Version = 4 });

        Assert.Equal(GeneratedPurgeSql, command.Sql);
        Assert.Same(GeneratedPurgeSql, command.Sql);
        Assert.Equal(new object[] { 12, 4 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(SqlOperationKind.Delete, command.OperationKind);
        Assert.True(command.ValidateAffectedRows);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>
    /// 测试目的：最终表名或列名被映射配置覆盖时，Update 必须回退并使用覆盖后的完整 SQL。
    /// </summary>
    /// <param name="tableName">可选的最终表名覆盖。</param>
    /// <param name="nameColumn">可选的 Name 列名覆盖。</param>
    /// <param name="expectedSql">预期的完整回退 SQL。</param>
    [Theory]
    [InlineData("mapped_mutation_rows", null,
        "Update `mapped_mutation_rows` Set `Name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3")]
    [InlineData(null, "mapped_name",
        "Update `generated_mutation_rows` Set `mapped_name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3")]
    public void Update_WhenFinalTableOrColumnMappingDiffers_ShouldRenderMappedSql(
        string tableName, string nameColumn, string expectedSql)
    {
        var command = CreateBuilder(tableName: tableName, nameColumnName: nameColumn)
            .Update(new GeneratedMutationRow { Id = 9, Name = "mapped", Version = 6 });

        Assert.Equal(expectedSql, command.Sql);
        Assert.NotSame(GeneratedUpdateSql, command.Sql);
        Assert.Equal(new object[] { "mapped", 6, 9, 6 },
            command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：Insert 的 Include 和 Exclude 会改变有序写列，因此必须回退到运行时 SQL。
    /// </summary>
    [Fact]
    public void Insert_WhenIncludeOrExcludeChangesWriteColumns_ShouldRenderFilteredSql()
    {
        var builder = CreateBuilder();
        var entity = new GeneratedMutationRow { Id = 3, Name = "filtered", Version = 2 };

        var included = builder.Insert(entity, new SqlInsertOptions
        {
            IncludeProperties = new[] { nameof(GeneratedMutationRow.Name) }
        });
        var excluded = builder.Insert(entity, new SqlInsertOptions
        {
            ExcludeProperties = new[] { nameof(GeneratedMutationRow.Version) }
        });

        Assert.Equal("Insert Into `generated_mutation_rows` (`Name`) Values (@_p_0)", included.Sql);
        Assert.NotSame(GeneratedInsertSql, included.Sql);
        Assert.Equal(new object[] { "filtered" }, included.Parameters.Select(parameter => parameter.Value));
        Assert.Equal("Insert Into `generated_mutation_rows` (`Id`, `Name`) Values (@_p_0, @_p_1)", excluded.Sql);
        Assert.NotSame(GeneratedInsertSql, excluded.Sql);
        Assert.Equal(new object[] { 3, "filtered" }, excluded.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：生成 SQL 未声明的 Provider 必须使用该 Provider 的方言重新渲染完整 Update SQL。
    /// </summary>
    [Fact]
    public void Update_WhenProviderDiffersFromGeneratedSqlProvider_ShouldRenderProviderSql()
    {
        var options = new SqlUpdateOptions<GeneratedMutationRow>().Original(row => row.Version, 5);
        var command = CreateBuilder(SqlServerSqlProvider.Instance)
            .Update(new GeneratedMutationRow { Id = 42, Name = "sql-server", Version = 8 }, options);

        Assert.Equal("Update [generated_mutation_rows] Set [Name] = @_p_0, [Version] = @_p_1 " +
                     "Where [Id] = @_p_2 And [Version] = @_p_3", command.Sql);
        Assert.NotSame(GeneratedUpdateSql, command.Sql);
        Assert.Equal(new object[] { "sql-server", 8, 42, 5 },
            command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(SqlServerSqlProvider.Instance.Key, command.ProviderKey);
        Assert.True(command.ValidateAffectedRows);
    }

    /// <summary>
    /// 测试目的：适用的写入数据边界必须保留其参数化谓词，不能被静态 Purge SQL 绕过。
    /// </summary>
    [Fact]
    public void Purge_WhenDataBoundaryApplies_ShouldRenderBoundaryPredicate()
    {
        var builder = CreateBuilder(filters: new ISqlFilter[] { new RegionBoundaryFilter() });
        var command = builder.Purge(new GeneratedMutationRow { Id = 12, Version = 4 });

        Assert.Equal("Delete From `generated_mutation_rows` Where `Id` = @_p_0 And `Version` = @_p_1 " +
                     "And `Region`=@_p_2", command.Sql);
        Assert.NotSame(GeneratedPurgeSql, command.Sql);
        Assert.Equal(new object[] { 12, 4, "region-a" },
            command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.ValidateAffectedRows);
    }

    /// <summary>
    /// 测试目的：自定义对象名格式化器应使生成 SQL 失效，并由运行时格式化器输出完整 SQL。
    /// </summary>
    [Fact]
    public void Update_WhenCustomObjectNameFormatterIsConfigured_ShouldRenderFormattedSql()
    {
        var builder = CreateBuilder(objectNameFormatter: new PrefixingObjectNameFormatter());
        var command = builder.Update(new GeneratedMutationRow { Id = 9, Name = "formatted", Version = 6 });

        Assert.Equal("Update `formatted_generated_mutation_rows` Set `Name` = @_p_0, `Version` = @_p_1 " +
                     "Where `Id` = @_p_2 And `Version` = @_p_3", command.Sql);
        Assert.NotSame(GeneratedUpdateSql, command.Sql);
        Assert.Equal(new object[] { "formatted", 6, 9, 6 },
            command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：自定义参数工厂改变参数名时应回退运行时 SQL，并保持 SQL 名称和值一一对应。
    /// </summary>
    [Fact]
    public void Update_WhenCustomParameterFactoryChangesNames_ShouldRenderMatchingSqlAndParameters()
    {
        var builder = CreateBuilder(parameterFactory: new RenamingParameterFactory());
        var command = builder.Update(new GeneratedMutationRow { Id = 9, Name = "renamed", Version = 6 });

        Assert.Equal("Update `generated_mutation_rows` Set `Name` = @custom_p_0, `Version` = @custom_p_1 " +
                     "Where `Id` = @custom_p_2 And `Version` = @custom_p_3", command.Sql);
        Assert.NotSame(GeneratedUpdateSql, command.Sql);
        Assert.Equal(new[] { "@custom_p_0", "@custom_p_1", "@custom_p_2", "@custom_p_3" },
            command.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { "renamed", 6, 9, 6 },
            command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：同一 Builder 并发构建时应复用同一 SQL 常量，且各命令参数值互相隔离。
    /// </summary>
    [Fact]
    public void Insert_WhenRequestsBuildConcurrently_ShouldReuseSqlAndIsolateParameters()
    {
        const int requestCount = 24;
        var builder = CreateBuilder();
        var commands = new SqlWriteCommand[requestCount];

        Parallel.For(0, requestCount, index => commands[index] = builder.Insert(new GeneratedMutationRow
        {
            Id = index,
            Name = $"request-{index}",
            Version = index + 100
        }));

        for (var index = 0; index < requestCount; index++)
        {
            Assert.Equal(GeneratedInsertSql, commands[index].Sql);
            Assert.Same(GeneratedInsertSql, commands[index].Sql);
            Assert.Equal(new object[] { index, $"request-{index}", index + 100 },
                commands[index].Parameters.Select(parameter => parameter.Value));
        }
    }

    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder(ISqlProvider provider = null,
        string tableName = null, string nameColumnName = null, IEnumerable<ISqlFilter> filters = null,
        ISqlObjectNameFormatter objectNameFormatter = null, ISqlParameterFactory parameterFactory = null)
    {
        var model = CreateModel();

        var metadata = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        var mapping = new EntityMappingOptions
        {
            EntityType = typeof(GeneratedMutationRow),
            TableName = tableName ?? "generated_mutation_rows"
        };
        if (nameColumnName != null)
        {
            mapping.Columns[nameof(GeneratedMutationRow.Name)] = new ColumnMappingOptions
            {
                PropertyName = nameof(GeneratedMutationRow.Name),
                ColumnName = nameColumnName
            };
        }
        metadata.EntityMappings.Add(mapping);

        var services = new SqlBuilderServices(metadataOptions: metadata,
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model), filters: filters,
            objectNameFormatter: objectNameFormatter, parameterFactory: parameterFactory);
        return new DefaultSqlEntityMutationCommandBuilder(provider ?? SqliteSqlProvider.Instance, services);
    }

    private static EntityModelMetadata CreateModel() => new(typeof(GeneratedMutationRow),
        "generated_mutation_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(GeneratedMutationRow.Id), typeof(int), true, true,
                isKey: true, generatedGetter: source => ((GeneratedMutationRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(GeneratedMutationRow.Name), typeof(string), true, true,
                generatedGetter: source => ((GeneratedMutationRow)source).Name),
            EntityPropertyMetadata.CreateGenerated(nameof(GeneratedMutationRow.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: source => ((GeneratedMutationRow)source).Version),
            EntityPropertyMetadata.CreateGenerated(nameof(GeneratedMutationRow.Region), typeof(string), true, true,
                databaseGeneratedOption: DatabaseGeneratedOption.Computed,
                generatedGetter: source => ((GeneratedMutationRow)source).Region)
        }, () => new GeneratedMutationRow(), (source, propertyName) => propertyName switch
        {
            nameof(GeneratedMutationRow.Id) => ((GeneratedMutationRow)source).Id,
            nameof(GeneratedMutationRow.Name) => ((GeneratedMutationRow)source).Name,
            nameof(GeneratedMutationRow.Version) => ((GeneratedMutationRow)source).Version,
            nameof(GeneratedMutationRow.Region) => ((GeneratedMutationRow)source).Region,
            _ => null
        })
        {
            GeneratedMutationSql = CreateGeneratedMutationSql(),
            GeneratedMutationShapeSql = (provider, operation, property, rows) => (provider, operation, property, rows) switch
            {
                ("bing.sqlite", SqlOperationKind.InsertValues, "Name", 1) => SingleInsertSql,
                ("bing.sqlite", SqlOperationKind.Update, "Name", 1) => SingleUpdateSql,
                ("bing.sqlite", SqlOperationKind.InsertValues, null, 2) => DoubleInsertSql,
                _ => null
            }
        };

    private static Func<string, SqlOperationKind, string> CreateGeneratedMutationSql() =>
        (providerKey, operation) =>
            string.Equals(providerKey, SqliteSqlProvider.Instance.Key, StringComparison.Ordinal)
                ? operation switch
                {
                    SqlOperationKind.InsertValues => GeneratedInsertSql,
                    SqlOperationKind.Update => GeneratedUpdateSql,
                    SqlOperationKind.Delete => GeneratedPurgeSql,
                    _ => null
                }
                : null;

    private sealed class GeneratedMutationRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
        public string Region { get; set; }
    }

    private sealed class RegionBoundaryFilter : ISqlFilter, ISqlDataBoundaryContributor
    {
        public void Filter(SqlFilterContext context)
        {
        }

        public bool ShouldApply(SqlDataBoundaryContext context) =>
            context.EntityType == typeof(GeneratedMutationRow) && context.Operation == SqlDataBoundaryOperation.Purge;

        public void Apply(SqlDataBoundaryContext context) =>
            context.AddEquals(nameof(GeneratedMutationRow.Region), "region-a");
    }

    private sealed class PrefixingObjectNameFormatter : ISqlObjectNameFormatter
    {
        public string Format(SqlTableReference reference, IDialect dialect, DatabaseType? databaseType) =>
            dialect.SafeName($"formatted_{reference.TableName}");
    }

    private sealed class RenamingParameterFactory : ISqlParameterFactory
    {
        private readonly ISqlParameterFactory _inner =
            new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector());

        public SqlParam Create(string name, object value, ColumnMappingMetadata column,
            DatabaseContext databaseContext, Type entityType = null, SqlParameterSource source = SqlParameterSource.Unknown)
        {
            var original = _inner.Create(name, value, column, databaseContext, entityType, source);
            return new SqlParam($"@custom_{original.Name.TrimStart('@', '_')}", original.Value, original.DbType,
                original.Direction, original.Size, original.Precision, original.Scale)
            {
                OriginalValue = original.OriginalValue,
                IsProviderValue = original.IsProviderValue,
                EntityType = original.EntityType,
                PropertyName = original.PropertyName,
                ColumnName = original.ColumnName,
                DatabaseType = original.DatabaseType,
                ProviderTypeName = original.ProviderTypeName,
                Source = original.Source,
                MetadataLevel = original.MetadataLevel,
                StorageKind = original.StorageKind,
                ConverterKind = original.ConverterKind,
                CustomConverterName = original.CustomConverterName
            };
        }
    }
}
