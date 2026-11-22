using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// Lambda 多类型映射终结器的公开形状与转发契约测试。
/// </summary>
public class SqlLambdaMultiMappingContractTest
{
    /// <summary>
    /// 测试目的：同步与异步多映射终结器应为二至七段提供约定的公开重载。
    /// </summary>
    [Fact]
    public void MappedTerminals_WhenPublicApiInspected_ShouldExposeTwoToSevenInputOverloads()
    {
        var methods = typeof(SqlLambdaQuery).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        for (var inputCount = 2; inputCount <= 7; inputCount++)
        {
            var syncMethods = methods.Where(method => method.Name == "ToList" &&
                IsMappedMethod(method, inputCount)).ToArray();
            Assert.Equal(2, syncMethods.Length);
            Assert.Contains(syncMethods, method => method.GetParameters().Length == 1);
            Assert.Contains(syncMethods, method => HasParameterTypes(method, typeof(int?)));
            Assert.All(syncMethods.SelectMany(method => method.GetParameters()),
                parameter => Assert.False(parameter.IsOptional));

            var asyncMethods = methods.Where(method => method.Name == "ToListAsync" &&
                IsMappedMethod(method, inputCount)).ToArray();

            Assert.Equal(4, asyncMethods.Length);
            Assert.Single(asyncMethods.Where(method => method.GetParameters().Length == 1));
            Assert.Contains(asyncMethods, method => HasParameterTypes(method, typeof(int?)));
            Assert.Contains(asyncMethods, method => HasParameterTypes(method, typeof(CancellationToken)));
            Assert.Contains(asyncMethods, method =>
                HasParameterTypes(method, typeof(int?), typeof(CancellationToken)));
        }

        var splitOn = typeof(SqlLambdaQuery).GetMethod(nameof(SqlLambdaQuery.SplitOn), new[] { typeof(string) });
        Assert.NotNull(splitOn);
        Assert.Equal(typeof(SqlLambdaQuery), splitOn.ReturnType);
    }

    /// <summary>
    /// 测试目的：同步多映射应将委托与超时值交给现有查询终结器并返回其结果。
    /// </summary>
    [Fact]
    public void ToList_WhenMappedResultRequested_ShouldForwardMapAndTimeout()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var expected = new List<MultiMapResult>();
        var forwarded = new List<(Func<MultiMapFirst, MultiMapSecond, MultiMapResult> Map, int? Timeout)>();
        Func<MultiMapFirst, MultiMapSecond, MultiMapResult> map = Map;
        executor.Setup(item => item.ToList<MultiMapFirst, MultiMapSecond, MultiMapResult>(
                It.IsAny<SqlQueryPlan>(), It.IsAny<Func<MultiMapFirst, MultiMapSecond, MultiMapResult>>(),
                It.IsAny<int?>()))
            .Returns((SqlQueryPlan unusedPlan, Func<MultiMapFirst, MultiMapSecond, MultiMapResult> forwardedMap,
                int? timeout) =>
            {
                forwarded.Add((forwardedMap, timeout));
                return expected;
            });

        var query = CreateMappedQuery(executor.Object);

        Assert.Same(expected, query.ToList<MultiMapFirst, MultiMapSecond, MultiMapResult>(map));
        Assert.Same(expected, query.ToList<MultiMapFirst, MultiMapSecond, MultiMapResult>(map, 17));

        Assert.Collection(forwarded,
            item =>
            {
                Assert.Same(map, item.Map);
                Assert.Null(item.Timeout);
            },
            item =>
            {
                Assert.Same(map, item.Map);
                Assert.Equal(17, item.Timeout);
            });
        executor.Verify(item => item.ToList<MultiMapFirst, MultiMapSecond, MultiMapResult>(
                It.IsAny<SqlQueryPlan>(), It.IsAny<Func<MultiMapFirst, MultiMapSecond, MultiMapResult>>(),
                It.IsAny<int?>()),
            Times.Exactly(2));
    }

    /// <summary>
    /// 测试目的：异步多映射的四种参数形式应原样转发委托、超时值和取消令牌。
    /// </summary>
    [Fact]
    public async Task ToListAsync_WhenMapOptionsAreSelected_ShouldForwardTimeoutAndCancellation()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var expected = new List<MultiMapResult>();
        var forwarded = new List<(Func<MultiMapFirst, MultiMapSecond, MultiMapResult> Map,
            int? Timeout, CancellationToken Token)>();
        Func<MultiMapFirst, MultiMapSecond, MultiMapResult> map = Map;
        using var cancellation = new CancellationTokenSource();

        executor.Setup(item => item.ToListAsync<MultiMapFirst, MultiMapSecond, MultiMapResult>(
                It.IsAny<SqlQueryPlan>(), It.IsAny<Func<MultiMapFirst, MultiMapSecond, MultiMapResult>>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns((SqlQueryPlan unusedPlan, Func<MultiMapFirst, MultiMapSecond, MultiMapResult> forwardedMap,
                int? timeout, CancellationToken token) =>
            {
                forwarded.Add((forwardedMap, timeout, token));
                return Task.FromResult(expected);
            });

        Assert.Same(expected, await CreateMappedQuery(executor.Object)
            .ToListAsync<MultiMapFirst, MultiMapSecond, MultiMapResult>(map));
        Assert.Same(expected, await CreateMappedQuery(executor.Object)
            .ToListAsync<MultiMapFirst, MultiMapSecond, MultiMapResult>(map, 17));
        Assert.Same(expected, await CreateMappedQuery(executor.Object)
            .ToListAsync<MultiMapFirst, MultiMapSecond, MultiMapResult>(map, cancellation.Token));
        Assert.Same(expected, await CreateMappedQuery(executor.Object)
            .ToListAsync<MultiMapFirst, MultiMapSecond, MultiMapResult>(map, 23, cancellation.Token));

        Assert.Collection(forwarded,
            item =>
            {
                Assert.Same(map, item.Map);
                Assert.Null(item.Timeout);
                Assert.Equal(default, item.Token);
            },
            item =>
            {
                Assert.Same(map, item.Map);
                Assert.Equal(17, item.Timeout);
                Assert.Equal(default, item.Token);
            },
            item =>
            {
                Assert.Same(map, item.Map);
                Assert.Null(item.Timeout);
                Assert.Equal(cancellation.Token, item.Token);
            },
            item =>
            {
                Assert.Same(map, item.Map);
                Assert.Equal(23, item.Timeout);
                Assert.Equal(cancellation.Token, item.Token);
            });
    }

    /// <summary>
    /// 测试目的：Lambda 查询设置的 SplitOn 应进入被执行的多映射查询计划。
    /// </summary>
    [Fact]
    public async Task SplitOn_WhenMappedQueryIsExecuted_ShouldReachQueryPlan()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        string splitOn = null;
        executor.Setup(item => item.ToListAsync<MultiMapFirst, MultiMapSecond, MultiMapResult>(
                It.IsAny<SqlQueryPlan>(), It.IsAny<Func<MultiMapFirst, MultiMapSecond, MultiMapResult>>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns((SqlQueryPlan plan, Func<MultiMapFirst, MultiMapSecond, MultiMapResult> unusedMap,
                int? unusedTimeout, CancellationToken unusedToken) =>
            {
                splitOn = plan.SplitOn;
                return Task.FromResult(new List<MultiMapResult>());
            });

        var query = CreateMappedQuery(executor.Object).SplitOn("SecondId");

        await query.ToListAsync<MultiMapFirst, MultiMapSecond, MultiMapResult>(Map);

        Assert.Equal("SecondId", splitOn);
    }

    /// <summary>
    /// 判断方法是否符合指定输入数的多映射签名。
    /// </summary>
    /// <param name="method">待检查的反射方法。</param>
    /// <param name="inputCount">多映射方法的输入来源个数。</param>
    /// <returns>方法满足指定输入数的多映射签名时为 true，否则为 false。</returns>
    private static bool IsMappedMethod(MethodInfo method, int inputCount)
    {
        if (!method.IsGenericMethodDefinition || method.GetGenericArguments().Length != inputCount + 1)
            return false;
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
            return false;

        var delegateType = parameters[0].ParameterType;
        var invoke = delegateType.IsGenericType &&
            delegateType.GetGenericTypeDefinition().Name.StartsWith("Func", StringComparison.Ordinal)
                ? delegateType.GetMethod("Invoke")
                : null;
        return invoke?.GetParameters().Length == inputCount;
    }

    /// <summary>
    /// 判断方法是否包含预期的附加参数类型。
    /// </summary>
    /// <param name="method">待检查的反射方法。</param>
    /// <param name="additionalTypes">预期的附加参数类型。</param>
    /// <returns>参数数量及附加类型均匹配时为 true，否则为 false。</returns>
    private static bool HasParameterTypes(MethodInfo method, params Type[] additionalTypes)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != additionalTypes.Length + 1)
            return false;
        return parameters.Skip(1).Select(parameter => parameter.ParameterType).SequenceEqual(additionalTypes);
    }

    /// <summary>
    /// 创建双源多映射测试查询。
    /// </summary>
    /// <param name="executor">执行测试查询或操作的执行器。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery CreateMappedQuery(ISqlQueryPlanExecutor executor) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, new TestSqlBuilder())
            .From<MultiMapFirst>("first")
            .From<MultiMapSecond>("second");

    /// <summary>
    /// 将双源实体标识合并为映射结果。
    /// </summary>
    /// <param name="first">第一个映射或批次来源对象。</param>
    /// <param name="second">第二个映射或批次来源对象。</param>
    /// <returns>由两个来源标识组成的映射结果。</returns>
    private static MultiMapResult Map(MultiMapFirst first, MultiMapSecond second) =>
        new() { FirstId = first.Id, SecondId = second.Id };

    /// <summary>
    /// 提供Lambda 多映射测试使用的查询数据模型。
    /// </summary>
    private sealed class MultiMapFirst
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 提供Lambda 多映射测试使用的查询数据模型。
    /// </summary>
    private sealed class MultiMapSecond
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 承载Lambda 多映射查询结果的投影数据模型。
    /// </summary>
    private sealed class MultiMapResult
    {
        /// <summary>
        /// 获取或设置第一映射实体的标识。
        /// </summary>
        public int FirstId { get; set; }
        /// <summary>
        /// 获取或设置第二映射实体的标识。
        /// </summary>
        public int SecondId { get; set; }
    }
}

