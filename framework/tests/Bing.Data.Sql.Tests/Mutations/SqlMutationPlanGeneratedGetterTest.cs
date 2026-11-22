using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>
/// 验证 Mutation Plan 预构建生成 Getter 的读取合同。
/// </summary>
public sealed class SqlMutationPlanGeneratedGetterTest
{
    /// <summary>
    /// 测试目的：单行与多行 Insert 应使用生成 Getter，并保持每行独立的参数值。
    /// </summary>
    [Fact]
    public void InsertAndInsertCombined_WithGeneratedModel_ShouldRenderCompleteSqlWithoutGetterCache()
    {
        var builder = new DefaultSqlEntityMutationCommandBuilder(TestMutationSqlProvider.Instance,
            CreateGeneratedServices());
        var first = new GeneratedRow { Id = 1, Name = "first" };
        var second = new GeneratedRow { Id = 2, Name = "second" };

        var single = builder.Insert(first);
        var combined = builder.InsertCombined(new[] { first, second });

        Assert.Equal("Insert Into [generated_rows] ([Id], [Name]) Values (@_p_0, @_p_1)", single.Sql);
        Assert.Equal(new object[] { 1, "first" }, single.Parameters.Select(parameter => parameter.Value));
        Assert.True(single.IsGeneratedExecution);
        Assert.Equal("Insert Into [generated_rows] ([Id], [Name]) Values (@_p_0, @_p_1), (@_p_2, @_p_3)",
            combined.Sql);
        Assert.Equal(new object[] { 1, "first", 2, "second" },
            combined.Parameters.Select(parameter => parameter.Value));
        Assert.True(combined.IsGeneratedExecution);
        Assert.Equal(0, builder.GetterCacheCount);
    }

    /// <summary>
    /// 测试目的：组合 Delete 的 IN 和配对条件均应使用生成 Getter 读取对应实体的主键。
    /// </summary>
    /// <param name="strategy">组合删除策略。</param>
    /// <param name="expectedSql">完整 SQL。</param>
    [Theory]
    [InlineData(SqlBatchDeleteStrategy.Auto, "Delete From [generated_rows] Where [Id] In (@_p_0,@_p_1)")]
    [InlineData(SqlBatchDeleteStrategy.CompositePredicate,
        "Delete From [generated_rows] Where (([Id] = @_p_0) Or ([Id] = @_p_1))")]
    public void DeleteCombined_WithGeneratedModel_ShouldRenderCompleteSqlWithoutGetterCache(
        SqlBatchDeleteStrategy strategy, string expectedSql)
    {
        var builder = new DefaultSqlEntityMutationCommandBuilder(TestMutationSqlProvider.Instance,
            CreateGeneratedServices());

        var command = builder.DeleteCombined(new[] { new GeneratedRow { Id = 1 }, new GeneratedRow { Id = 2 } },
            strategy: strategy);

        Assert.Equal(expectedSql, command.Sql);
        Assert.Equal(new object[] { 1, 2 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(0, builder.GetterCacheCount);
    }

    /// <summary>
    /// 测试目的：严格生成模型应直接使用计划 Getter，生成完整 Update SQL，并且不创建兼容 Getter 缓存项。
    /// </summary>
    [Fact]
    public void Update_WithGeneratedModel_ShouldRenderCompleteSqlWithoutGetterCache()
    {
        var services = CreateGeneratedServices();
        var builder = new DefaultSqlEntityMutationCommandBuilder(TestMutationSqlProvider.Instance, services);

        var command = builder.Update(new GeneratedRow { Id = 42, Name = "next" },
            new SqlUpdateOptions { IncludeProperties = new[] { nameof(GeneratedRow.Name) } });

        Assert.Equal("Update [generated_rows] Set [Name] = @_p_0 Where [Id] = @_p_1", command.Sql);
        Assert.Equal(new object[] { "next", 42 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(0, builder.GetterCacheCount);
    }

    /// <summary>
    /// 测试目的：批量 Update 上下文应复用计划级生成 Getter，不因重复读取回到反射缓存。
    /// </summary>
    [Fact]
    public void BatchUpdateRenderContext_WithGeneratedModel_ShouldReadWithoutGetterCache()
    {
        var services = CreateGeneratedServices();
        var builder = new DefaultSqlEntityMutationCommandBuilder(TestMutationSqlProvider.Instance, services);
        var first = new GeneratedRow { Id = 1, Name = "first" };
        var second = new GeneratedRow { Id = 2, Name = "second" };
        var context = builder.CreateUpdateRenderContext(new[] { first, second }, new SqlUpdateOptions
        {
            IncludeProperties = new[] { nameof(GeneratedRow.Name) }
        });
        var column = Assert.Single(context.UpdateColumns);

        Assert.Equal("first", context.GetValue(first, column));
        Assert.Equal("second", context.GetValue(second, column));
        Assert.Equal(0, builder.GetterCacheCount);

        var nextContext = builder.CreateUpdateRenderContext(new[] { second }, new SqlUpdateOptions
        {
            IncludeProperties = new[] { nameof(GeneratedRow.Name) }
        });
        Assert.Equal("second", nextContext.GetValue(second, Assert.Single(nextContext.UpdateColumns)));
        var cache = SqlMutationPlanCaches.Get(services.EntityMappingResolver, services.MetadataOptions,
            services.EntityModelMetadataProvider);
        Assert.Equal(2, cache.PlanCount);
        Assert.Equal(2, cache.PlanCacheMissCount);
        Assert.Equal(2, cache.PlanCacheHitCount);
    }

    /// <summary>
    /// 测试目的：写入列、主键和并发列应共享计划级生成 Getter，且每个属性只建立一个索引项。
    /// </summary>
    [Fact]
    public void Create_ShouldIndexGeneratedGettersForWriteKeysAndConcurrency()
    {
        var calls = 0;
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
                isKey: true, generatedGetter: source =>
                {
                    calls++;
                    return ((Row)source).Id;
                }),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                generatedGetter: source =>
                {
                    calls++;
                    return ((Row)source).Name;
                }),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: source =>
                {
                    calls++;
                    return ((Row)source).Version;
                })
        });
        var mapping = CreateMapping(model);
        var plan = SqlMutationPlan.Create(mapping, SqlMutationOperation.Update, null, null);
        var row = new Row { Id = 7, Name = "alpha", Version = 3 };

        Assert.Equal(3, plan.GeneratedGetterCount);
        Assert.True(plan.TryGetGeneratedValue(row, mapping.Columns[nameof(Row.Id)], out var id));
        Assert.True(plan.TryGetGeneratedValue(row, mapping.Columns[nameof(Row.Name)], out var name));
        Assert.True(plan.TryGetGeneratedValue(row, mapping.Columns[nameof(Row.Version)], out var version));
        Assert.Equal(7, id);
        Assert.Equal("alpha", name);
        Assert.Equal(3, version);
        Assert.Equal(3, calls);
    }

    /// <summary>
    /// 测试目的：完整生成模型应优先使用生成式值读取器，避免为同一计划建立重复 Getter 字典。
    /// </summary>
    [Fact]
    public void TryGetGeneratedValue_WithGeneratedValueReader_ShouldUseReaderBeforeGetterIndex()
    {
        var getterCalls = 0;
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
                isKey: true, generatedGetter: _ =>
                {
                    getterCalls++;
                    throw new InvalidOperationException("不应回退到单属性 Getter。");
                }),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                generatedGetter: _ =>
                {
                    getterCalls++;
                    throw new InvalidOperationException("不应回退到单属性 Getter。");
                }),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: _ =>
                {
                    getterCalls++;
                    throw new InvalidOperationException("不应回退到单属性 Getter。");
                })
        }, () => new Row(), (source, propertyName) => propertyName switch
        {
            nameof(Row.Id) => ((Row)source).Id,
            nameof(Row.Name) => ((Row)source).Name,
            nameof(Row.Version) => ((Row)source).Version,
            _ => null
        });
        var plan = SqlMutationPlan.Create(CreateMapping(model), SqlMutationOperation.Update, null, null);
        var row = new Row { Id = 9, Name = "generated-reader" };

        Assert.True(plan.TryGetGeneratedValue(row, new ColumnMappingMetadata
        {
            PropertyName = nameof(Row.Id),
            ColumnName = "Id"
        }, out var id));
        Assert.True(plan.TryGetGeneratedValue(row, new ColumnMappingMetadata
        {
            PropertyName = nameof(Row.Name),
            ColumnName = "Name"
        }, out var name));

        Assert.Equal(9, id);
        Assert.Equal("generated-reader", name);
        Assert.Equal(0, getterCalls);
        Assert.Equal(0, plan.GeneratedGetterCount);
    }

    /// <summary>
    /// 测试目的：登记基类的生成 Getter 应可读取派生实例，并按映射基类属性读取隐藏成员。
    /// </summary>
    [Fact]
    public void TryGetGeneratedValue_WithDerivedSource_ShouldUseRegisteredBaseGetter()
    {
        var model = CreateRowModel();
        var mapping = CreateMapping(model);
        var plan = SqlMutationPlan.Create(mapping, SqlMutationOperation.Update, null, null);
        var source = CreateDerivedRow();

        Assert.True(plan.TryGetGeneratedValue(source, mapping.Columns[nameof(Row.Id)], out var id));
        Assert.Equal(42, id);
        Assert.True(plan.TryGetGeneratedValue(source, mapping.Columns[nameof(Row.Name)], out var name));
        Assert.Equal("base-name", name);
    }

    /// <summary>
    /// 测试目的：基类登记模型驱动 Insert 和 Update 时应复用生成 Getter，而不建立反射 Getter 缓存。
    /// </summary>
    [Fact]
    public void InsertAndUpdate_WithDerivedSource_ShouldUseRegisteredBaseGettersWithoutGetterCache()
    {
        var builder = new DefaultSqlEntityMutationCommandBuilder(TestMutationSqlProvider.Instance,
            CreateGeneratedRowServices());
        Row entity = CreateDerivedRow();

        var insert = builder.Insert(entity);
        var update = builder.Update(entity,
            new SqlUpdateOptions { IncludeProperties = new[] { nameof(Row.Name) } });

        Assert.Equal("Insert Into [rows] ([Id], [Name]) Values (@_p_0, @_p_1)", insert.Sql);
        Assert.Equal(new object[] { 42, "base-name" }, insert.Parameters.Select(parameter => parameter.Value));
        Assert.Equal("Update [rows] Set [Name] = @_p_0 Where [Id] = @_p_1", update.Sql);
        Assert.Equal(new object[] { "base-name", 42 }, update.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(0, builder.GetterCacheCount);
    }

    /// <summary>
    /// 测试目的：生成 Getter 只能用于登记的实体类型，其他来源应安全回退兼容读取器。
    /// </summary>
    [Fact]
    public void TryGetGeneratedValue_ShouldRejectDifferentSourceType()
    {
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
                isKey: true, generatedGetter: source => ((Row)source).Id)
        });
        var mapping = CreateMapping(model);
        var plan = SqlMutationPlan.Create(mapping, SqlMutationOperation.Delete, null, null);

        Assert.False(plan.TryGetGeneratedValue(new OtherRow { Id = 9 },
            mapping.Columns[nameof(Row.Id)], out var value));
        Assert.Null(value);
    }

    /// <summary>
    /// 测试目的：没有生成模型时计划应保持空索引，不改变兼容回退契约。
    /// </summary>
    [Fact]
    public void Create_WithoutGeneratedModel_ShouldKeepEmptyGeneratedIndex()
    {
        var mapping = new EntityMappingMetadata
        {
            EntityType = typeof(Row),
            Table = new SqlTableReference { TableName = "rows" },
            Columns = new Dictionary<string, ColumnMappingMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                [nameof(Row.Id)] = new ColumnMappingMetadata
                {
                    PropertyName = nameof(Row.Id),
                    ColumnName = "Id",
                    ClrType = typeof(int),
                    IsKey = true,
                    CanUpdate = false
                }
            }
        };

        var plan = SqlMutationPlan.Create(mapping, SqlMutationOperation.Delete, null, null);

        Assert.Equal(0, plan.GeneratedGetterCount);
        Assert.False(plan.TryGetGeneratedValue(new Row { Id = 1 }, mapping.Columns[nameof(Row.Id)], out _));
    }

    private static EntityMappingMetadata CreateMapping(EntityModelMetadata model) => new()
    {
        EntityType = typeof(Row),
        Model = model,
        Table = new SqlTableReference { TableName = "rows" },
        Columns = new Dictionary<string, ColumnMappingMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(Row.Id)] = new ColumnMappingMetadata
            {
                PropertyName = nameof(Row.Id),
                ColumnName = "Id",
                ClrType = typeof(int),
                IsKey = true,
                CanUpdate = false
            },
            [nameof(Row.Name)] = new ColumnMappingMetadata
            {
                PropertyName = nameof(Row.Name),
                ColumnName = "Name",
                ClrType = typeof(string),
                CanUpdate = true
            },
            [nameof(Row.Version)] = new ColumnMappingMetadata
            {
                PropertyName = nameof(Row.Version),
                ColumnName = "Version",
                ClrType = typeof(int),
                IsConcurrencyToken = true,
                CanUpdate = true
            }
        }
    };

    private static EntityModelMetadata CreateRowModel() => new(typeof(Row), "rows", null, new[]
    {
        EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
            isKey: true, generatedGetter: source => ((Row)source).Id),
        EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
            generatedGetter: source => ((Row)source).Name)
    }, () => new Row());

    private static Row CreateDerivedRow()
    {
        var result = new DerivedRow { Id = 900, Name = "derived-name" };
        ((Row)result).Id = 42;
        ((Row)result).Name = "base-name";
        return result;
    }

    private static SqlBuilderServices CreateGeneratedRowServices()
    {
        var metadata = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Row),
            TableName = "rows"
        });
        return new SqlBuilderServices(metadataOptions: metadata,
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(CreateRowModel()));
    }

    /// <summary>
    /// 创建严格生成模型使用的 Mutation Builder 服务。
    /// </summary>
    private static SqlBuilderServices CreateGeneratedServices()
    {
        var model = new EntityModelMetadata(typeof(GeneratedRow), "generated_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(GeneratedRow.Id), typeof(int), true, true,
                isKey: true, generatedGetter: source => ((GeneratedRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(GeneratedRow.Name), typeof(string), true, true,
                generatedGetter: source => ((GeneratedRow)source).Name)
        }, () => new GeneratedRow());
        var metadata = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(GeneratedRow),
            TableName = "generated_rows"
        });
        return new SqlBuilderServices(metadataOptions: metadata,
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model));
    }

    private class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }

    private sealed class DerivedRow : Row
    {
        public new int Id { get; set; }
        public new string Name { get; set; }
    }

    private sealed class OtherRow
    {
        public int Id { get; set; }
    }

    private sealed class GeneratedRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
