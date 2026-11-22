using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 多来源 Lambda 投影构建、克隆与重复渲染性能基线。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlProjectionBenchmarks
{
    /// <summary>
    /// 保存基准使用的查询计划执行器。
    /// </summary>
    private ISqlQueryPlanExecutor _executor;
    /// <summary>
    /// 保存基准使用的原始查询。
    /// </summary>
    private SqlLambdaQuery _sourceQuery;
    /// <summary>
    /// 保存基准使用的投影查询。
    /// </summary>
    private SqlLambdaQuery _selectedQuery;
    /// <summary>
    /// 保存基准使用的缓存渲染查询。
    /// </summary>
    private SqlLambdaQuery _cachedRenderQuery;
    /// <summary>
    /// 保存基准使用的禁用别名原始查询。
    /// </summary>
    private SqlLambdaQuery _disabledAliasSourceQuery;
    /// <summary>
    /// 保存基准使用的禁用别名缓存渲染查询。
    /// </summary>
    private SqlLambdaQuery _disabledAliasCachedRenderQuery;
    /// <summary>
    /// 保存连续连接使用的参数表达式。
    /// </summary>
    private ParameterExpression[] _continuousJoinParameters;
    /// <summary>
    /// 保存连续连接使用的谓词表达式。
    /// </summary>
    private Expression<Func<ProjectionOrder, ProjectionOrder, bool>>[] _continuousJoinPredicates;
    /// <summary>
    /// 保存连续连接使用的投影表达式。
    /// </summary>
    private Expression<Func<ProjectionOrder, object>> _continuousJoinProjection;
    /// <summary>
    /// 保存基准使用的异构源查询。
    /// </summary>
    private SqlLambdaQuery _heterogeneousSourceQuery;
    /// <summary>
    /// 保存基准使用的多映射上下文查询。
    /// </summary>
    private SqlLambdaQuery[] _mappingContextQueries;
    /// <summary>
    /// 记录下一个映射上下文的索引。
    /// </summary>
    private int _nextMappingContext;

    /// <summary>
    /// 获取或设置类型化来源数量。
    /// </summary>
    [Params(1, 5, 20)]
    public int SourceCount { get; set; }

    /// <summary>
    /// 初始化当前参数对应的来源及稳定投影状态。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _executor = DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();
        _sourceQuery = CreateSourceQuery();
        _selectedQuery = CreateSourceQuery()
            .Select<ProjectionOrder>(row => row.DisplayName, "r1");
        _cachedRenderQuery = CreateSourceQuery()
            .Select<ProjectionOrder>(row => row.Id, "r1");
        _cachedRenderQuery.ToSql();

        _continuousJoinParameters = Enumerable.Range(1, 20)
            .Select(index => Expression.Parameter(typeof(ProjectionOrder), $"r{index}"))
            .ToArray();
        _continuousJoinPredicates = Enumerable.Range(0, _continuousJoinParameters.Length - 1)
            .Select(index => Expression.Lambda<Func<ProjectionOrder, ProjectionOrder, bool>>(
                Expression.Equal(
                    Expression.Property(_continuousJoinParameters[index], nameof(ProjectionOrder.CustomerId)),
                    Expression.Property(_continuousJoinParameters[index + 1], nameof(ProjectionOrder.Id))),
                _continuousJoinParameters[index], _continuousJoinParameters[index + 1]))
            .ToArray();
        _continuousJoinProjection = Expression.Lambda<Func<ProjectionOrder, object>>(
            Expression.Convert(
                Expression.Property(_continuousJoinParameters[0], nameof(ProjectionOrder.Id)), typeof(object)),
            _continuousJoinParameters[0]);
        var continuousJoinSql = RenderContinuousSameTypeJoinProjection();
        if (!string.Equals(continuousJoinSql, BuildExpectedContinuousJoinSql(), StringComparison.Ordinal))
            throw new InvalidOperationException("连续同类型 Join 基准未生成完整预期 SQL。");

        var legacyAggregateSql = RenderAggregateProjection();
        var directMemberAggregateSql = RenderAggregateProjectionWithDirectMembers();
        var sqlFnAggregateSql = RenderAggregateProjectionWithSqlFn();
        var expectedAggregateSql = BuildExpectedAggregateSql();
        if (!string.Equals(legacyAggregateSql, expectedAggregateSql, StringComparison.Ordinal) ||
            !string.Equals(directMemberAggregateSql, expectedAggregateSql, StringComparison.Ordinal) ||
            !string.Equals(sqlFnAggregateSql, expectedAggregateSql, StringComparison.Ordinal))
            throw new InvalidOperationException("三种 Count/Sum 表达式基准未生成完全一致的预期 SQL。");

        _disabledAliasSourceQuery = CreateSourceQuery(autoGenerateTableAlias: false);
        var disabledAliasCloneSql = CloneAndChangeProjectionWithAliasGenerationDisabled();
        _disabledAliasCachedRenderQuery = _disabledAliasSourceQuery.Clone()
            .Select<ProjectionOrder>(r1 => r1.Id);
        var expectedDisabledAliasProjectionSql = BuildExpectedAliasDisabledSql("`r1`.`DisplayName`");
        var expectedDisabledAliasCachedSql = BuildExpectedAliasDisabledSql("`r1`.`Id`");
        var firstDisabledAliasRender = _disabledAliasCachedRenderQuery.ToSql();
        var secondDisabledAliasRender = RenderCachedSqlWithAliasGenerationDisabled();
        if (!string.Equals(disabledAliasCloneSql, expectedDisabledAliasProjectionSql, StringComparison.Ordinal) ||
            !string.Equals(firstDisabledAliasRender, expectedDisabledAliasCachedSql, StringComparison.Ordinal) ||
            !string.Equals(secondDisabledAliasRender, expectedDisabledAliasCachedSql, StringComparison.Ordinal))
            throw new InvalidOperationException("关闭自动别名后的显式来源、克隆投影或重复渲染未生成预期完整 SQL。");

        _heterogeneousSourceQuery = CreateHeterogeneousSourceQuery();
        var heterogeneousFromSql = _heterogeneousSourceQuery.ToSql();
        var heterogeneousProjectionSql = AddHeterogeneousProjection(_heterogeneousSourceQuery.Clone()).ToSql();
        if (!string.Equals(heterogeneousFromSql, BuildExpectedHeterogeneousSql(false), StringComparison.Ordinal) ||
            !string.Equals(heterogeneousProjectionSql, BuildExpectedHeterogeneousSql(true), StringComparison.Ordinal))
            throw new InvalidOperationException("异构来源基准未按不同实体类型唯一绑定并生成完整预期 SQL。");

        _mappingContextQueries = new[]
        {
            CreateMappedSourceQuery("projection-a", "projection_orders_a"),
            CreateMappedSourceQuery("projection-b", "projection_orders_b")
        };
        _nextMappingContext = 0;
        if (!_mappingContextQueries[0].ToSql().Contains("projection_orders_a", StringComparison.Ordinal) ||
            !_mappingContextQueries[1].ToSql().Contains("projection_orders_b", StringComparison.Ordinal))
            throw new InvalidOperationException("映射上下文基准未使用预期的表映射。");
        if (!ValidateUnmatchedParameterBindingWithoutStateChange())
            throw new InvalidOperationException("参数名未命中时的唯一来源回退或多来源状态回滚不符合预期。");

        var mappedCloneA = CloneAndChangeProjectionAcrossMappingProfiles();
        var mappedCloneB = CloneAndChangeProjectionAcrossMappingProfiles();
        if (!mappedCloneA.Contains("projection_orders_a", StringComparison.Ordinal) ||
            !mappedCloneB.Contains("projection_orders_b", StringComparison.Ordinal))
            throw new InvalidOperationException("映射上下文基准克隆未保留对应的 profile 映射。");
        _nextMappingContext = 0;
    }

    /// <summary>
    /// 测量 SelectAll 排除少量列后的 SQL 渲染成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderSelectAllWithSmallExclusions()
    {
        var query = _sourceQuery.Clone()
            .SelectAll()
            .ExcludeSelect<ProjectionOrder>(row => row.Notes, "r1")
            .ExcludeSelect<ProjectionOrder>(row => row.InternalCode, "r1");
        return query.ToSql();
    }

    /// <summary>
    /// 测量连续 Select 累计不同列后的构建和渲染成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderCumulativeProjection()
    {
        var query = _sourceQuery.Clone()
            .Select<ProjectionOrder>(row => row.DisplayName, "r1")
            .Select<ProjectionOrder>(row => row.Quantity, "r1");
        return query.ToSql();
    }

    /// <summary>
    /// 测量连续 Select 重复选择同一列时的去重和渲染成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderDeduplicatedProjection()
    {
        var query = _sourceQuery.Clone()
            .Select<ProjectionOrder>(row => row.DisplayName, "r1")
            .Select<ProjectionOrder>(row => row.DisplayName, "r1")
            .Select<ProjectionOrder>(row => row.Quantity, "r1");
        return query.ToSql();
    }

    /// <summary>
    /// 测量连续 Select 后用 ReplaceSelect 缩减投影的构建和渲染成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderReplacedProjection()
    {
        var query = _sourceQuery.Clone()
            .Select<ProjectionOrder>(row => row.DisplayName, "r1")
            .Select<ProjectionOrder>(row => row.Quantity, "r1")
            .ReplaceSelect<ProjectionOrder>(row => row.Id, "r1");
        return query.ToSql();
    }

    /// <summary>
    /// 测量克隆已投影查询后独立追加列并渲染的成本。
    /// </summary>
    /// <returns>克隆查询生成的 SQL 文本。</returns>
    [Benchmark]
    public string CloneAndChangeProjection()
    {
        var clone = _selectedQuery.Clone()
            .Select<ProjectionOrder>(row => row.Quantity, "r1");
        return clone.ToSql();
    }

    /// <summary>
    /// 测量重复调用 ToSql 的已缓存渲染路径。
    /// </summary>
    /// <returns>已缓存的 SQL 文本。</returns>
    [Benchmark]
    public string RenderCachedSql() => _cachedRenderQuery.ToSql();

    /// <summary>
    /// 测量不配置排除规则时的自动全列投影渲染成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderSelectAllWithoutExclusions() => _sourceQuery.Clone().SelectAll().ToSql();

    /// <summary>
    /// 测量按来源为所有列配置多项排除规则后的渲染成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderSelectAllWithManyExclusions()
    {
        var query = _sourceQuery.Clone().SelectAll();
        for (var index = 1; index <= SourceCount; index++)
        {
            var alias = $"r{index}";
            query.ExcludeSelect<ProjectionOrder>(row => row.CustomerId, alias)
                .ExcludeSelect<ProjectionOrder>(row => row.Quantity, alias)
                .ExcludeSelect<ProjectionOrder>(row => row.CreatedAt, alias)
                .ExcludeSelect<ProjectionOrder>(row => row.Notes, alias)
                .ExcludeSelect<ProjectionOrder>(row => row.InternalCode, alias);
        }
        return query.ToSql();
    }

    /// <summary>
    /// 测量 Lambda 参数名命中显式来源别名时的投影解析与渲染成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string ResolveParameterAliasMatch() => _sourceQuery.Clone()
        .Select<ProjectionOrder>(r1 => r1.DisplayName)
        .ToSql();

    /// <summary>
    /// 测量参数名未命中时的唯一类型回退或多来源歧义校验与状态保持。
    /// </summary>
    /// <returns>参数名未命中后的预期绑定行为是否成立。</returns>
    [Benchmark]
    public bool ValidateUnmatchedParameterBindingWithoutStateChange()
    {
        var query = _sourceQuery.Clone();
        var before = query.ToSql();
        try
        {
            query.Select<ProjectionOrder>(missing => missing.DisplayName);
            return SourceCount == 1 && !string.Equals(before, query.ToSql(), StringComparison.Ordinal);
        }
        catch (InvalidOperationException error) when (SourceCount > 1 &&
                                                       error.Message.Contains("查询来源不唯一", StringComparison.Ordinal))
        {
            return string.Equals(before, query.ToSql(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 测量不传别名的连续同类型 Join 来源绑定和 SQL 渲染成本。
    /// </summary>
    /// <returns>从 r1 到最后一个来源的完整 Join SQL。</returns>
    [Benchmark]
    public string RenderContinuousSameTypeJoinProjection()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new MySqlBuilder())
            .From<ProjectionOrder>();
        for (var index = 0; index < SourceCount - 1; index++)
            query.Join<ProjectionOrder, ProjectionOrder>(_continuousJoinPredicates[index]);
        return query.Select<ProjectionOrder>(_continuousJoinProjection).ToSql();
    }

    /// <summary>
    /// 测量旧式 selector Count/Sum 匿名汇总投影的构建和渲染成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderAggregateProjection() => _sourceQuery.Clone()
        .Select<ProjectionOrder>((r1, functions) => new
        {
            r1.CustomerId,
            OrderCount = functions.Count(row => row.Id),
            TotalQuantity = functions.Sum(row => row.Quantity)
        })
        .GroupBy<ProjectionOrder>(r1 => r1.CustomerId)
        .ToSql();

    /// <summary>
    /// 测量聚合上下文直接引用外层 Lambda 成员的 Count/Sum 投影成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderAggregateProjectionWithDirectMembers() => _sourceQuery.Clone()
        .Select<ProjectionOrder>((r1, functions) => new
        {
            r1.CustomerId,
            OrderCount = functions.Count(r1.Id),
            TotalQuantity = functions.Sum(r1.Quantity)
        })
        .GroupBy<ProjectionOrder>(r1 => r1.CustomerId)
        .ToSql();

    /// <summary>
    /// 测量静态 SqlFn 成员引用的 Count/Sum 匿名汇总投影成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string RenderAggregateProjectionWithSqlFn() => _sourceQuery.Clone()
        .Select<ProjectionOrder>((r1, functions) => new
        {
            r1.CustomerId,
            OrderCount = SqlFn.Count(r1.Id),
            TotalQuantity = SqlFn.Sum(r1.Quantity)
        })
        .GroupBy<ProjectionOrder>(r1 => r1.CustomerId)
        .ToSql();

    /// <summary>
    /// 测量关闭自动别名后克隆多来源查询、使用显式来源别名追加投影并渲染的成本。
    /// </summary>
    /// <returns>克隆查询生成的 SQL 文本。</returns>
    [Benchmark]
    public string CloneAndChangeProjectionWithAliasGenerationDisabled() => _disabledAliasSourceQuery.Clone()
        .Select<ProjectionOrder>(r1 => r1.DisplayName)
        .ToSql();

    /// <summary>
    /// 测量关闭自动别名后重复调用 ToSql 的已缓存渲染路径。
    /// </summary>
    /// <returns>已缓存的 SQL 文本。</returns>
    [Benchmark]
    public string RenderCachedSqlWithAliasGenerationDisabled() => _disabledAliasCachedRenderQuery.ToSql();

    /// <summary>
    /// 测量参数名不匹配时，1/5/20 个不同实体类型来源按类型唯一绑定的投影成本。
    /// </summary>
    /// <returns>每个唯一实体来源的 Id 投影 SQL。</returns>
    [Benchmark]
    public string RenderHeterogeneousSourceProjection() =>
        AddHeterogeneousProjection(_heterogeneousSourceQuery.Clone()).ToSql();

    /// <summary>
    /// 测量不同映射 profile 下克隆并追加投影的上下文隔离成本。
    /// </summary>
    /// <returns>当前映射上下文生成的 SQL 文本。</returns>
    [Benchmark]
    public string CloneAndChangeProjectionAcrossMappingProfiles()
    {
        var query = _mappingContextQueries[_nextMappingContext];
        _nextMappingContext = (_nextMappingContext + 1) % _mappingContextQueries.Length;
        return query.Clone()
            .Select<ProjectionOrder>(r1 => r1.Notes, "r1")
            .ToSql();
    }

    /// <summary>
    /// 创建投影基准所需的多源查询。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private SqlLambdaQuery CreateSourceQuery()
        => CreateSourceQuery(new MySqlBuilder());

    /// <summary>
    /// 创建投影基准所需的多源查询。
    /// </summary>
    /// <param name="autoGenerateTableAlias">是否自动生成表别名。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private SqlLambdaQuery CreateSourceQuery(bool autoGenerateTableAlias)
        => CreateSourceQuery(new MySqlBuilder(new SqlBuilderServices(options: new SqlOptions
        {
            AutoGenerateTableAlias = autoGenerateTableAlias
        })));

    /// <summary>
    /// 创建使用指定映射配置和表名的多源查询。
    /// </summary>
    /// <param name="mappingProfile">实体映射配置档名称。</param>
    /// <param name="tableName">目标物理表名。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private SqlLambdaQuery CreateMappedSourceQuery(string mappingProfile, string tableName)
    {
        var metadataOptions = new SqlMetadataOptions
        {
            DefaultDatabaseContext = new DatabaseContext
            {
                DbKey = "projection-benchmark",
                MappingProfile = mappingProfile,
                DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.MySql }
            }
        };
        metadataOptions.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(ProjectionOrder),
            DbKey = "projection-benchmark",
            DatabaseType = DatabaseType.MySql,
            MappingProfile = mappingProfile,
            TableName = tableName
        });
        return CreateSourceQuery(new MySqlBuilder(new SqlBuilderServices(metadataOptions: metadataOptions)));
    }

    /// <summary>
    /// 创建投影基准所需的多源查询。
    /// </summary>
    /// <param name="builder">构造测试 SQL 的构建器。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private SqlLambdaQuery CreateSourceQuery(ISqlBuilder builder)
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, builder);
        for (var index = 1; index <= SourceCount; index++)
            query.From<ProjectionOrder>($"r{index}");
        return query;
    }

    /// <summary>
    /// 创建异构实体的多源查询。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private SqlLambdaQuery CreateHeterogeneousSourceQuery()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new MySqlBuilder());
        if (SourceCount >= 1) query.From<HeterogeneousProjection01>("h1");
        if (SourceCount >= 2) query.From<HeterogeneousProjection02>("h2");
        if (SourceCount >= 3) query.From<HeterogeneousProjection03>("h3");
        if (SourceCount >= 4) query.From<HeterogeneousProjection04>("h4");
        if (SourceCount >= 5) query.From<HeterogeneousProjection05>("h5");
        if (SourceCount >= 6) query.From<HeterogeneousProjection06>("h6");
        if (SourceCount >= 7) query.From<HeterogeneousProjection07>("h7");
        if (SourceCount >= 8) query.From<HeterogeneousProjection08>("h8");
        if (SourceCount >= 9) query.From<HeterogeneousProjection09>("h9");
        if (SourceCount >= 10) query.From<HeterogeneousProjection10>("h10");
        if (SourceCount >= 11) query.From<HeterogeneousProjection11>("h11");
        if (SourceCount >= 12) query.From<HeterogeneousProjection12>("h12");
        if (SourceCount >= 13) query.From<HeterogeneousProjection13>("h13");
        if (SourceCount >= 14) query.From<HeterogeneousProjection14>("h14");
        if (SourceCount >= 15) query.From<HeterogeneousProjection15>("h15");
        if (SourceCount >= 16) query.From<HeterogeneousProjection16>("h16");
        if (SourceCount >= 17) query.From<HeterogeneousProjection17>("h17");
        if (SourceCount >= 18) query.From<HeterogeneousProjection18>("h18");
        if (SourceCount >= 19) query.From<HeterogeneousProjection19>("h19");
        if (SourceCount >= 20) query.From<HeterogeneousProjection20>("h20");
        return query;
    }

    /// <summary>
    /// 为异构多源查询添加投影。
    /// </summary>
    /// <param name="query">待执行或构造的测试查询。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private SqlLambdaQuery AddHeterogeneousProjection(SqlLambdaQuery query)
    {
        if (SourceCount >= 1) query.Select<HeterogeneousProjection01>(missing => missing.Id);
        if (SourceCount >= 2) query.Select<HeterogeneousProjection02>(missing => missing.Id);
        if (SourceCount >= 3) query.Select<HeterogeneousProjection03>(missing => missing.Id);
        if (SourceCount >= 4) query.Select<HeterogeneousProjection04>(missing => missing.Id);
        if (SourceCount >= 5) query.Select<HeterogeneousProjection05>(missing => missing.Id);
        if (SourceCount >= 6) query.Select<HeterogeneousProjection06>(missing => missing.Id);
        if (SourceCount >= 7) query.Select<HeterogeneousProjection07>(missing => missing.Id);
        if (SourceCount >= 8) query.Select<HeterogeneousProjection08>(missing => missing.Id);
        if (SourceCount >= 9) query.Select<HeterogeneousProjection09>(missing => missing.Id);
        if (SourceCount >= 10) query.Select<HeterogeneousProjection10>(missing => missing.Id);
        if (SourceCount >= 11) query.Select<HeterogeneousProjection11>(missing => missing.Id);
        if (SourceCount >= 12) query.Select<HeterogeneousProjection12>(missing => missing.Id);
        if (SourceCount >= 13) query.Select<HeterogeneousProjection13>(missing => missing.Id);
        if (SourceCount >= 14) query.Select<HeterogeneousProjection14>(missing => missing.Id);
        if (SourceCount >= 15) query.Select<HeterogeneousProjection15>(missing => missing.Id);
        if (SourceCount >= 16) query.Select<HeterogeneousProjection16>(missing => missing.Id);
        if (SourceCount >= 17) query.Select<HeterogeneousProjection17>(missing => missing.Id);
        if (SourceCount >= 18) query.Select<HeterogeneousProjection18>(missing => missing.Id);
        if (SourceCount >= 19) query.Select<HeterogeneousProjection19>(missing => missing.Id);
        if (SourceCount >= 20) query.Select<HeterogeneousProjection20>(missing => missing.Id);
        return query;
    }

    /// <summary>
    /// 构造异构多源查询的预期 SQL。
    /// </summary>
    /// <param name="includeProjection">是否在预期 SQL 中包含投影。</param>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    private string BuildExpectedHeterogeneousSql(bool includeProjection)
    {
        var tables = string.Join(", ", Enumerable.Range(1, SourceCount)
            .Select(index => $"`heterogeneous_projection_{index:00}` As `h{index}`"));
        if (!includeProjection)
            return $"Select * \r\nFrom {tables}";

        var columns = string.Join(",", Enumerable.Range(1, SourceCount)
            .Select(index => $"`h{index}`.`Id`"));
        return $"Select {columns} \r\nFrom {tables}";
    }

    /// <summary>
    /// 构造聚合投影的预期 SQL。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    private string BuildExpectedAggregateSql()
    {
        var sources = string.Join(", ", Enumerable.Range(1, SourceCount)
            .Select(index => $"`ProjectionOrder` As `r{index}`"));
        return $"Select `r1`.`CustomerId` As `CustomerId`,Count(`r1`.`Id`) As `OrderCount`,Sum(`r1`.`Quantity`) As `TotalQuantity` " +
               $"\r\nFrom {sources} \r\nGroup By `r1`.`CustomerId`";
    }

    /// <summary>
    /// 构造禁用自动表别名时的预期 SQL。
    /// </summary>
    /// <param name="projection">预期 SQL 的投影片段。</param>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    private string BuildExpectedAliasDisabledSql(string projection)
    {
        var sources = string.Join(", ", Enumerable.Range(1, SourceCount)
            .Select(index => $"`ProjectionOrder` As `r{index}`"));
        return $"Select {projection} \r\nFrom {sources}";
    }

    /// <summary>
    /// 构造连续连接查询的预期 SQL。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    private string BuildExpectedContinuousJoinSql()
    {
        var joins = Enumerable.Range(2, SourceCount - 1)
            .Select(index => $" \r\nJoin `ProjectionOrder` As `r{index}` On `r{index - 1}`.`CustomerId`=`r{index}`.`Id`");
        return "Select `r1`.`Id` \r\nFrom `ProjectionOrder` As `r1`" + string.Concat(joins);
    }

    /// <summary>
    /// 实现不执行目标方法的动态代理测试替身。
    /// </summary>
    private class NoOpExecutor : DispatchProxy
    {
        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    /// <summary>
    /// 表示 SQL 投影基准使用的订单数据。
    /// </summary>
    private sealed class ProjectionOrder
    {
        /// <summary>
        /// 获取或设置订单标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置客户标识。
        /// </summary>
        public int CustomerId { get; set; }

        /// <summary>
        /// 获取或设置展示名称。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 获取或设置数量。
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// 获取或设置创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 获取或设置说明文本。
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// 获取或设置内部代码。
        /// </summary>
        public string InternalCode { get; set; }
    }

    /// <summary>
    /// 标记异构投影基准中的结果类型。
    /// </summary>
    ///
    /// <typeparam name="TMarker">用于区分异构投影基准场景的标记类型。</typeparam>
    private abstract class HeterogeneousProjectionMarker<TMarker>
    {
        /// <summary>
        /// 获取或设置异构投影结果的标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker01 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker02 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker03 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker04 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker05 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker06 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker07 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker08 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker09 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker10 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker11 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker12 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker13 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker14 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker15 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker16 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker17 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker18 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker19 { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的来源类型。
    /// </summary>
    private sealed class Marker20 { }

    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_01")]
    private sealed class HeterogeneousProjection01 : HeterogeneousProjectionMarker<Marker01> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_02")]
    private sealed class HeterogeneousProjection02 : HeterogeneousProjectionMarker<Marker02> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_03")]
    private sealed class HeterogeneousProjection03 : HeterogeneousProjectionMarker<Marker03> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_04")]
    private sealed class HeterogeneousProjection04 : HeterogeneousProjectionMarker<Marker04> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_05")]
    private sealed class HeterogeneousProjection05 : HeterogeneousProjectionMarker<Marker05> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_06")]
    private sealed class HeterogeneousProjection06 : HeterogeneousProjectionMarker<Marker06> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_07")]
    private sealed class HeterogeneousProjection07 : HeterogeneousProjectionMarker<Marker07> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_08")]
    private sealed class HeterogeneousProjection08 : HeterogeneousProjectionMarker<Marker08> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_09")]
    private sealed class HeterogeneousProjection09 : HeterogeneousProjectionMarker<Marker09> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_10")]
    private sealed class HeterogeneousProjection10 : HeterogeneousProjectionMarker<Marker10> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_11")]
    private sealed class HeterogeneousProjection11 : HeterogeneousProjectionMarker<Marker11> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_12")]
    private sealed class HeterogeneousProjection12 : HeterogeneousProjectionMarker<Marker12> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_13")]
    private sealed class HeterogeneousProjection13 : HeterogeneousProjectionMarker<Marker13> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_14")]
    private sealed class HeterogeneousProjection14 : HeterogeneousProjectionMarker<Marker14> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_15")]
    private sealed class HeterogeneousProjection15 : HeterogeneousProjectionMarker<Marker15> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_16")]
    private sealed class HeterogeneousProjection16 : HeterogeneousProjectionMarker<Marker16> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_17")]
    private sealed class HeterogeneousProjection17 : HeterogeneousProjectionMarker<Marker17> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_18")]
    private sealed class HeterogeneousProjection18 : HeterogeneousProjectionMarker<Marker18> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_19")]
    private sealed class HeterogeneousProjection19 : HeterogeneousProjectionMarker<Marker19> { }
    /// <summary>
    /// 标记异构 SQL 投影基准测试中的对应列类型。
    /// </summary>
    [Table("heterogeneous_projection_20")]
    private sealed class HeterogeneousProjection20 : HeterogeneousProjectionMarker<Marker20> { }
}
