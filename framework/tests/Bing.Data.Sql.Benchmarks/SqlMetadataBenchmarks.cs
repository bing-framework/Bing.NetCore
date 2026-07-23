using System;
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
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
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
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

/// <summary>
/// MySQL 表名解析性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class MySqlTableNameParserBenchmarks
{
    /// <summary>
    /// 测量简单物理表名解析。
    /// </summary>
    [Benchmark]
    public string ParseSimpleTable() => MySqlTableNameParser.Parse("orders").TableName;

    /// <summary>
    /// 测量带点物理表名解析。
    /// </summary>
    [Benchmark]
    public string ParseDottedPhysicalTable() => MySqlTableNameParser.Parse("Merchants.Company").TableName;

    /// <summary>
    /// 测量反引号带点物理表名解析。
    /// </summary>
    [Benchmark]
    public string ParseQuotedDottedPhysicalTable() => MySqlTableNameParser.Parse("`Merchants.Company`").TableName;

    /// <summary>
    /// 测量限定带点物理表名解析。
    /// </summary>
    [Benchmark]
    public string ParseQualifiedDottedPhysicalTable() =>
        MySqlTableNameParser.Parse("`archive_db`.`Merchants.Company`").TableName;

    /// <summary>
    /// 测量包含别名的限定表名解析。
    /// </summary>
    [Benchmark]
    public string ParseQualifiedTableWithAlias() =>
        MySqlTableNameParser.Parse("`archive_db`.`Merchants.Company` As merchant").Alias;
}

/// <summary>
/// MySQL Builder 真实 Provider 渲染性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class MySqlBuilderBenchmarks
{
    private MySqlBuilder _simpleBuilder;
    private MySqlBuilder _dottedTableBuilder;
    private MySqlBuilder _qualifiedTableBuilder;
    private MySqlBuilder _complexBuilder;

    /// <summary>
    /// 初始化稳定的 MySQL Builder 状态。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _simpleBuilder = new MySqlBuilder().Select("o.Id").From("orders", "o") as MySqlBuilder;
        _dottedTableBuilder = new MySqlBuilder().Select("c.CompanyId,c.Name")
            .From("Merchants.Company", "c") as MySqlBuilder;
        _qualifiedTableBuilder = new MySqlBuilder().Select("c.CompanyId,c.Name")
            .From("`archive_db`.`Merchants.Company`", "c") as MySqlBuilder;
        _complexBuilder = CreateComplexBuilder();
    }

    /// <summary>
    /// 测量简单表 SQL 渲染。
    /// </summary>
    [Benchmark]
    public string RenderSimpleTable() => _simpleBuilder.ToSql();

    /// <summary>
    /// 测量带点物理表 SQL 渲染。
    /// </summary>
    [Benchmark]
    public string RenderDottedPhysicalTable() => _dottedTableBuilder.ToSql();

    /// <summary>
    /// 测量限定带点物理表 SQL 渲染。
    /// </summary>
    [Benchmark]
    public string RenderQualifiedDottedPhysicalTable() => _qualifiedTableBuilder.ToSql();

    /// <summary>
    /// 测量复杂 Join、参数和分页 SQL 渲染。
    /// </summary>
    [Benchmark]
    public string RenderComplexQuery() => _complexBuilder.ToSql();

    /// <summary>
    /// 测量复杂 Builder 克隆。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneComplexBuilder() => _complexBuilder.Clone();

    /// <summary>
    /// 创建复杂 MySQL Builder。
    /// </summary>
    private static MySqlBuilder CreateComplexBuilder()
    {
        var builder = new MySqlBuilder();
        builder.Select("c.CompanyId,c.Name,m.Name As MerchantName")
            .From("Merchants.Company", "c")
            .LeftJoin("`archive_db`.`Merchants.Merchant`", "m")
            .AppendOn("m.MerchantId=c.MerchantId")
            .Where("c.Enabled", true)
            .OrderBy("c.CompanyId")
            .Skip(10)
            .Take(20);
        return builder;
    }
}

/// <summary>
/// MySQL Builder 重复渲染性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class MySqlRepeatedRenderBenchmarks
{
    private MySqlBuilder _builder;

    /// <summary>
    /// 初始化重复渲染使用的复杂 Builder。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new MySqlBuilder();
        _builder.Select("c.CompanyId,c.Name,m.Name As MerchantName")
            .From("Merchants.Company", "c")
            .LeftJoin("`archive_db`.`Merchants.Merchant`", "m")
            .AppendOn("m.MerchantId=c.MerchantId")
            .Where("c.Enabled", true)
            .OrderBy("c.CompanyId")
            .Skip(10)
            .Take(20);
    }

    /// <summary>
    /// 测量单次重复渲染。
    /// </summary>
    [Benchmark]
    public string RenderOnce() => _builder.ToSql();

    /// <summary>
    /// 测量十次重复渲染。
    /// </summary>
    [Benchmark]
    public string RenderTenTimes() => Render(10);

    /// <summary>
    /// 测量一百次重复渲染。
    /// </summary>
    [Benchmark]
    public string RenderOneHundredTimes() => Render(100);

    /// <summary>
    /// 执行固定次数的 SQL 渲染。
    /// </summary>
    private string Render(int count)
    {
        string sql = null;
        for (var index = 0; index < count; index++)
            sql = _builder.ToSql();
        return sql;
    }
}

/// <summary>
/// MySQL 原始 Append 构建性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class MySqlAppendBenchmarks
{
    /// <summary>
    /// 测量一个原始 Join 片段的构建和渲染。
    /// </summary>
    [Benchmark]
    public string BuildOneRawJoin() => BuildRawJoins(1);

    /// <summary>
    /// 测量五个原始 Join 片段的构建和渲染。
    /// </summary>
    [Benchmark]
    public string BuildFiveRawJoins() => BuildRawJoins(5);

    /// <summary>
    /// 测量二十个原始 Join 片段的构建和渲染。
    /// </summary>
    [Benchmark]
    public string BuildTwentyRawJoins() => BuildRawJoins(20);

    /// <summary>
    /// 构造指定数量的原始 Join 片段。
    /// </summary>
    private static string BuildRawJoins(int count)
    {
        var builder = new MySqlBuilder();
        builder.AppendSelect("c.CompanyId").AppendFrom("`Merchants.Company` As `c`");
        for (var index = 0; index < count; index++)
        {
            builder.AppendLeftJoin($"`archive_db`.`Merchants.Merchant` As `m{index}`");
            builder.AppendOn($"m{index}.MerchantId=c.MerchantId");
        }
        return builder.ToSql();
    }
}

/// <summary>
/// MySQL Builder 构建性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class MySqlBuilderConstructionBenchmarks
{
    /// <summary>
    /// 测量仅 From 的 Builder 构建与渲染。
    /// </summary>
    [Benchmark]
    public string BuildFromOnly() => CreateBuilder(0, 0, false).ToSql();

    /// <summary>
    /// 测量一个结构化 Join 的 Builder 构建与渲染。
    /// </summary>
    [Benchmark]
    public string BuildOneStructuredJoin() => CreateBuilder(1, 0, false).ToSql();

    /// <summary>
    /// 测量五个结构化 Join 的 Builder 构建与渲染。
    /// </summary>
    [Benchmark]
    public string BuildFiveStructuredJoins() => CreateBuilder(5, 0, false).ToSql();

    /// <summary>
    /// 测量二十个结构化 Join 的 Builder 构建与渲染。
    /// </summary>
    [Benchmark]
    public string BuildTwentyStructuredJoins() => CreateBuilder(20, 0, false).ToSql();

    /// <summary>
    /// 测量包含十个参数的 Builder 构建与渲染。
    /// </summary>
    [Benchmark]
    public string BuildWithTenParameters() => CreateBuilder(1, 10, false).ToSql();

    /// <summary>
    /// 测量包含五十个参数的 Builder 构建与渲染。
    /// </summary>
    [Benchmark]
    public string BuildWithFiftyParameters() => CreateBuilder(1, 50, false).ToSql();

    /// <summary>
    /// 测量原始与结构化 Join 混合的 Builder 构建与渲染。
    /// </summary>
    [Benchmark]
    public string BuildMixedRawAndStructuredJoins() => CreateBuilder(5, 10, true).ToSql();

    /// <summary>
    /// 创建指定规模的 MySQL Builder。
    /// </summary>
    private static MySqlBuilder CreateBuilder(int joinCount, int parameterCount, bool includeRawJoin)
    {
        var builder = new MySqlBuilder();
        builder.Select("c.CompanyId,c.Name").From("Merchants.Company", "c");
        for (var index = 0; index < joinCount; index++)
        {
            if (includeRawJoin && index % 2 == 0)
            {
                builder.AppendLeftJoin($"`archive_db`.`Merchants.Merchant` As `r{index}`");
                builder.AppendOn($"r{index}.MerchantId=c.MerchantId");
                continue;
            }
            builder.LeftJoin("`archive_db`.`Merchants.Merchant`", $"m{index}")
                .AppendOn($"m{index}.MerchantId=c.MerchantId");
        }
        for (var index = 0; index < parameterCount; index++)
            builder.Where($"c.Parameter{index}", index);
        return builder;
    }
}

/// <summary>
/// MySQL Builder Clone 性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class MySqlBuilderCloneBenchmarks
{
    private MySqlBuilder _fromOnlyBuilder;
    private MySqlBuilder _oneJoinBuilder;
    private MySqlBuilder _fiveJoinBuilder;
    private MySqlBuilder _twentyJoinBuilder;
    private MySqlBuilder _tenParameterBuilder;
    private MySqlBuilder _fiftyParameterBuilder;
    private MySqlBuilder _mixedBuilder;

    /// <summary>
    /// 初始化 Clone 使用的稳定 Builder 状态。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _fromOnlyBuilder = CreateBuilder(0, 0, false);
        _oneJoinBuilder = CreateBuilder(1, 0, false);
        _fiveJoinBuilder = CreateBuilder(5, 0, false);
        _twentyJoinBuilder = CreateBuilder(20, 0, false);
        _tenParameterBuilder = CreateBuilder(1, 10, false);
        _fiftyParameterBuilder = CreateBuilder(1, 50, false);
        _mixedBuilder = CreateBuilder(5, 10, true);
    }

    /// <summary>
    /// 测量仅 From Builder Clone。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneFromOnly() => _fromOnlyBuilder.Clone();

    /// <summary>
    /// 测量一个 Join Builder Clone。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneOneJoin() => _oneJoinBuilder.Clone();

    /// <summary>
    /// 测量五个 Join Builder Clone。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneFiveJoins() => _fiveJoinBuilder.Clone();

    /// <summary>
    /// 测量二十个 Join Builder Clone。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneTwentyJoins() => _twentyJoinBuilder.Clone();

    /// <summary>
    /// 测量十个参数 Builder Clone。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneWithTenParameters() => _tenParameterBuilder.Clone();

    /// <summary>
    /// 测量五十个参数 Builder Clone。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneWithFiftyParameters() => _fiftyParameterBuilder.Clone();

    /// <summary>
    /// 测量原始与结构化 Join 混合 Builder Clone。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneMixedRawAndStructuredJoins() => _mixedBuilder.Clone();

    /// <summary>
    /// 创建指定规模的 MySQL Builder。
    /// </summary>
    private static MySqlBuilder CreateBuilder(int joinCount, int parameterCount, bool includeRawJoin)
    {
        var builder = new MySqlBuilder();
        builder.Select("c.CompanyId,c.Name").From("Merchants.Company", "c");
        for (var index = 0; index < joinCount; index++)
        {
            if (includeRawJoin && index % 2 == 0)
            {
                builder.AppendLeftJoin($"`archive_db`.`Merchants.Merchant` As `r{index}`");
                builder.AppendOn($"r{index}.MerchantId=c.MerchantId");
                continue;
            }
            builder.LeftJoin("`archive_db`.`Merchants.Merchant`", $"m{index}")
                .AppendOn($"m{index}.MerchantId=c.MerchantId");
        }
        for (var index = 0; index < parameterCount; index++)
            builder.Where($"c.Parameter{index}", index);
        return builder;
    }
}

/// <summary>
/// MySQL 调试 SQL 渲染性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlDebugRenderingBenchmarks
{
    private MySqlBuilder _builder;
    private string _sql;

    /// <summary>
    /// 初始化带参数的稳定 Builder 状态。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new MySqlBuilder();
        _builder.Select("c.CompanyId,c.Name")
            .From("Merchants.Company", "c")
            .Where("c.Enabled", true)
            .Where("c.Name", "benchmark")
            .Where("c.CreatedTime", new DateTime(2026, 7, 23, 10, 30, 0));
        _sql = _builder.ToSql();
    }

    /// <summary>
    /// 测量无参调试 SQL 渲染，包含一次额外 ToSql 调用。
    /// </summary>
    /// <returns>调试 SQL。</returns>
    [Benchmark(Baseline = true)]
    public string RenderDebugSqlWithSecondRender() => _builder.ToDebugSql();

    /// <summary>
    /// 测量复用已生成 SQL 的调试渲染。
    /// </summary>
    /// <returns>调试 SQL。</returns>
    [Benchmark]
    public string RenderDebugSqlWithExistingSql() => _builder.ToDebugSql(_sql);
}

/// <summary>
/// SQL 查询执行准备性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlQueryExecutionPreparationBenchmarks
{
    private MySqlBuilder _builder;

    /// <summary>
    /// 初始化执行准备使用的稳定 Builder 状态。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new MySqlBuilder();
        _builder.Select("c.CompanyId,c.Name,m.Name As MerchantName")
            .From("Merchants.Company", "c")
            .LeftJoin("`archive_db`.`Merchants.Merchant`", "m")
            .AppendOn("m.MerchantId=c.MerchantId")
            .Where("c.Enabled", true)
            .Where("c.Name", "benchmark");
    }

    /// <summary>
    /// 测量 Trace 未启用时仅生成执行 SQL 的准备路径。
    /// </summary>
    /// <returns>执行 SQL。</returns>
    [Benchmark]
    public string PrepareWhenTraceIsDisabled() => _builder.ToSql();

    /// <summary>
    /// 测量旧执行准备路径：生成执行 SQL 后无参渲染调试 SQL。
    /// </summary>
    /// <returns>调试 SQL。</returns>
    [Benchmark(Baseline = true)]
    public string PrepareTraceWithSecondRender()
    {
        _builder.ToSql();
        return _builder.ToDebugSql();
    }

    /// <summary>
    /// 测量优化后的执行准备路径：复用已生成 SQL 渲染调试 SQL。
    /// </summary>
    /// <returns>调试 SQL。</returns>
    [Benchmark]
    public string PrepareTraceWithReusedSql()
    {
        var sql = _builder.ToSql();
        return _builder.ToDebugSql(sql);
    }
}