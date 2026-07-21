using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// SQL 元数据与结构化表引用性能基线。
/// </summary>
[MemoryDiagnoser]
public class SqlMetadataBenchmarks
{
    private DefaultEntityMappingResolver _mappingResolver;
    private readonly DefaultSqlObjectNameFormatter _formatter = new();
    private readonly BenchmarkDialect _dialect = new();
    private readonly DatabaseContext _databaseContext = new()
    {
        DbKey = "benchmark",
        DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
    };
    private readonly SqlTableReference _reference = new()
    {
        Database = "benchmark",
        Schema = "dbo",
        TableName = "orders",
        Alias = "o"
    };
    private readonly SqlTableReference _mySqlReference = new()
    {
        Schema = "benchmark",
        TableName = "orders",
        Alias = "o"
    };
    private BenchmarkBuilder _builder;

    /// <summary>
    /// 映射配置规模。
    /// </summary>
    [Params(1, 10, 100)]
    public int MappingConfigurationCount { get; set; }

    /// <summary>
    /// 初始化 Builder 基线状态。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var metadataOptions = new SqlMetadataOptions();
        for (var index = 0; index < MappingConfigurationCount; index++)
            metadataOptions.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(BenchmarkEntity),
                MappingProfile = $"profile-{index}",
                TableName = $"orders_{index}"
            });
        _mappingResolver = new DefaultEntityMappingResolver(options: metadataOptions);
        _databaseContext.MappingProfile = $"profile-{MappingConfigurationCount - 1}";
        _builder = new BenchmarkBuilder();
        _builder.FromClause.From(_reference);
    }

    /// <summary>
    /// 测量实体映射缓存命中性能。
    /// </summary>
    /// <returns>实体映射元数据。</returns>
    [Benchmark]
    public EntityMappingMetadata ResolveMappingCacheHit() =>
        _mappingResolver.Resolve(typeof(BenchmarkEntity), _databaseContext);

    /// <summary>
    /// 测量未命中实例缓存时的实体映射解析性能。
    /// </summary>
    /// <returns>实体映射元数据。</returns>
    [Benchmark]
    public EntityMappingMetadata ResolveMappingCold()
    {
        var metadataOptions = new SqlMetadataOptions();
        for (var index = 0; index < MappingConfigurationCount; index++)
            metadataOptions.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(BenchmarkEntity),
                MappingProfile = $"profile-{index}",
                TableName = $"orders_{index}"
            });
        return new DefaultEntityMappingResolver(options: metadataOptions).Resolve(typeof(BenchmarkEntity),
            _databaseContext);
    }

    /// <summary>
    /// 测量结构化表对象名称格式化性能。
    /// </summary>
    /// <returns>格式化后的 SQL 对象名称。</returns>
    [Benchmark]
    public string FormatStructuredTableReference() =>
        _formatter.Format(_reference, _dialect, DatabaseType.SqlServer);

    /// <summary>
    /// 测量 MySQL 结构化表对象名称格式化性能。
    /// </summary>
    /// <returns>格式化后的 SQL 对象名称。</returns>
    [Benchmark]
    public string FormatMySqlStructuredTableReference() =>
        _formatter.Format(_mySqlReference, _dialect, DatabaseType.MySql);

    /// <summary>
    /// 测量仅包含结构化 From 的重复渲染性能。
    /// </summary>
    /// <returns>渲染后的 From SQL。</returns>
    [Benchmark]
    public string RenderStructuredFrom() => _builder.FromClause.ToSql();

    /// <summary>
    /// 测量包含结构化 From 与 Join 的渲染性能。
    /// </summary>
    /// <returns>渲染后的 From 和 Join SQL。</returns>
    [Benchmark]
    public string RenderStructuredFromAndJoin()
    {
        var builder = new BenchmarkBuilder();
        builder.FromClause.From(_reference);
        builder.JoinClause.Join(new SqlTableReference
        {
            Database = "benchmark",
            Schema = "dbo",
            TableName = "customers",
            Alias = "c"
        });
        return $"{builder.FromClause.ToSql()} {builder.JoinClause.ToSql()}";
    }

    /// <summary>
    /// 测量包含结构化 From 的 Builder 克隆性能。
    /// </summary>
    /// <returns>克隆后的 SQL Builder。</returns>
    [Benchmark]
    public ISqlBuilder CloneBuilder() => _builder.Clone();

    private sealed class BenchmarkEntity
    {
        public int Id { get; set; }
    }

    private sealed class BenchmarkBuilder : SqlBuilderBase
    {
        protected override DatabaseType ProviderDatabaseType => DatabaseType.SqlServer;

        protected override IDialect GetDialect() => new BenchmarkDialect();

        protected override string CreateLimitSql() =>
            $"Offset {GetOffsetParam()} Rows Fetch Next {GetLimitParam()} Rows Only";

        public override ISqlBuilder Clone()
        {
            var builder = new BenchmarkBuilder();
            builder.Clone(this);
            return builder;
        }

        public override ISqlBuilder New() => new BenchmarkBuilder();
    }

    private sealed class BenchmarkDialect : DialectBase
    {
        public override char OpeningIdentifier => '[';

        public override char ClosingIdentifier => ']';

        public override string GetPrefix() => "@";
    }
}

internal static class Program
{
    private static void Main(string[] args) => BenchmarkRunner.Run<SqlMetadataBenchmarks>();
}