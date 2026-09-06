using Xunit;

namespace Bing.Test.Shared;

/// <summary>
/// Provider 合同 runner 和能力矩阵测试。
/// </summary>
public sealed class ProviderContractRunnerTest
{
    /// <summary>
    /// 测试目的：全局 RC readiness 必须同时具备四类核心 Provider、双 TFM、完整能力方法和所有外部门禁。
    /// </summary>
    [Fact]
    public void ReleaseReadiness_WhenAnyGlobalInputIsMissing_ShouldRemainFalse()
    {
        // Arrange
        var run = new ProviderReleaseReadinessRun
        {
            Provider = "MySql",
            Framework = "net8.0",
            ReleaseEvidenceValid = true,
            Executed = 1,
            PassedTestMethods = new HashSet<string>(StringComparer.Ordinal)
        };

        // Act
        var result = ProviderReleaseReadiness.IsReady(new[] { run }, true, true, true, true);

        // Assert
        Assert.False(result);
        Assert.Contains("PostgreSql/net6.0", ProviderReleaseReadiness.GetMissingRuns(new[] { run }));
        Assert.Contains("SQLite/net8.0", ProviderReleaseReadiness.GetMissingRuns(new[] { run }));
    }

    /// <summary>
    /// 测试目的：全局 RC readiness 不得因单 Provider 局部证据或支持缺口而返回 true。
    /// </summary>
    [Fact]
    public void ReleaseReadiness_WhenImplementationGapOrFormalHostIsIncomplete_ShouldRemainFalse()
    {
        // Arrange
        var runs = new[]
        {
            new ProviderReleaseReadinessRun
            {
                Provider = "MySql",
                Framework = "net6.0",
                ReleaseEvidenceValid = true,
                Executed = 1
            }
        };

        // Act
        var result = ProviderReleaseReadiness.IsReady(runs, true, false, true, true);

        // Assert
        Assert.False(result);
        Assert.False(ProviderReleaseReadiness.IsReady(runs, true, true, true, true));
    }

    /// <summary>
    /// 测试目的：全局发布门禁必须拒绝来自不同聚合会话的 Provider 运行，避免跨构建拼接证据。
    /// </summary>
    [Fact]
    public void ReleaseReadiness_WhenEvidenceSessionsDiffer_ShouldRemainFalse()
    {
        // Arrange
        var runs = new[]
        {
            new ProviderReleaseReadinessRun
            {
                Provider = "MySql",
                Framework = "net6.0",
                EvidenceSessionId = "ci-build-a",
                ReleaseEvidenceValid = true,
                Executed = 1
            },
            new ProviderReleaseReadinessRun
            {
                Provider = "MySql",
                Framework = "net8.0",
                EvidenceSessionId = "ci-build-b",
                ReleaseEvidenceValid = true,
                Executed = 1
            }
        };

        // Act
        var result = ProviderReleaseReadiness.IsReady(runs, true, true, true, true);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// 测试目的：全局发布门禁必须拒绝来自不同源码提交的 Provider 运行，避免跨提交拼接证据。
    /// </summary>
    [Fact]
    public void ReleaseReadiness_WhenSourceIdentitiesDiffer_ShouldRemainFalse()
    {
        // Arrange
        var runs = new[]
        {
            new ProviderReleaseReadinessRun
            {
                Provider = "MySql",
                Framework = "net6.0",
                EvidenceSessionId = "ci-build",
                SourceIdentity = "git-a;source=clean",
                ReleaseEvidenceValid = true,
                Executed = 1
            },
            new ProviderReleaseReadinessRun
            {
                Provider = "MySql",
                Framework = "net8.0",
                EvidenceSessionId = "ci-build",
                SourceIdentity = "git-b;source=clean",
                ReleaseEvidenceValid = true,
                Executed = 1
            }
        };

        // Act
        var result = ProviderReleaseReadiness.IsReady(runs, true, true, true, true);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// 测试目的：当完整能力目录已无未解决实现缺口且八个 Provider/TFM 运行及外部门禁均通过时，全局 readiness 应返回 true。
    /// </summary>
    [Fact]
    public void ReleaseReadiness_WhenCompleteCatalogAndAllCoreRunsMatch_ShouldReturnTrue()
    {
        // Arrange
        const string evidenceSessionId = "ci-build-complete";
        var definitions = ProviderCapabilityCatalog.GetDefinitions()
            .Where(definition => definition.Provider is "MySql" or "PostgreSql" or "SqlServer" or "SQLite")
            .Select(definition => definition.State == ProviderCapabilityEvidenceState.ImplementationGap
                ? new ProviderCapabilityScenarioDefinition(definition.Provider, definition.Capability,
                    definition.Scenario, ProviderCapabilityEvidenceState.RealIntegrationProven,
                    $"{definition.Provider}CapabilityContractTest.ResolveImplementationGap", definition.Evidence)
                : definition)
            .ToArray();
        var runs = definitions.Select(definition => definition.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(provider => new[] { "net6.0", "net8.0" }.Select(framework =>
            new ProviderReleaseReadinessRun
            {
                Provider = provider,
                Framework = framework,
                EvidenceSessionId = evidenceSessionId,
                SourceIdentity = "git-complete;source=clean",
                ReleaseEvidenceValid = true,
                Executed = 1,
                PassedTestMethods = definitions
                    .Where(definition => string.Equals(definition.Provider, provider,
                        StringComparison.OrdinalIgnoreCase) &&
                        definition.State is not (ProviderCapabilityEvidenceState.Unsupported or
                            ProviderCapabilityEvidenceState.UnitProven) &&
                        !string.IsNullOrWhiteSpace(definition.TestMethod))
                    .Select(definition => "Bing.Dapper.Tests.SqlQuery." + definition.TestMethod)
                    .ToHashSet(StringComparer.Ordinal)
            })).ToArray();

        // Act
        var result = ProviderReleaseReadiness.IsReady(runs, true, true, true, true, definitions);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// 测试目的：没有真实集成元数据的执行场景只能保留为单元证据，预分类场景应保留对应六态。
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenScenariosComplete_ShouldKeepSixStateEvidence()
    {
        // Arrange
        var scenarios = new[]
        {
            new ProviderContractScenario("SQLite", "Query", "scalar", _ => Task.CompletedTask),
            new ProviderContractScenario("SQLite", "Procedure", "execute", fixedState:
                ProviderCapabilityEvidenceState.Unsupported, evidence: "SQLite 不支持存储过程。"),
            new ProviderContractScenario("Oracle", "Procedure", "execute", fixedState:
                ProviderCapabilityEvidenceState.ImplementationGap, evidence: "缺少安全 fixture。"),
            new ProviderContractScenario("MySql", "Cancellation", "during-execute", fixedState:
                ProviderCapabilityEvidenceState.NotExecuted, evidence: "未提供授权测试库。")
        };

        // Act
        var evidence = await ProviderContractRunner.RunAsync(scenarios);

        // Assert
        Assert.Equal(4, evidence.Count);
        Assert.Equal(ProviderCapabilityEvidenceState.UnitProven, evidence[0].State);
        Assert.Equal(ProviderCapabilityEvidenceState.Unsupported, evidence[1].State);
        Assert.Equal(ProviderCapabilityEvidenceState.ImplementationGap, evidence[2].State);
        Assert.Equal(ProviderCapabilityEvidenceState.NotExecuted, evidence[3].State);
    }

    /// <summary>
    /// 测试目的：矩阵不得接受重复 Provider 能力场景，输出必须保留状态并转义表格字符。
    /// </summary>
    [Fact]
    public void Matrix_WhenEvidenceIsAdded_ShouldRejectDuplicatesAndRenderSafeMarkdown()
    {
        // Arrange
        var matrix = new ProviderCapabilityMatrix();
        matrix.Add(new ProviderCapabilityEvidence("SQLite", "Query", "scalar",
            ProviderCapabilityEvidenceState.NotExecuted, "pending | current", "round4.trx"));

        // Act
        var markdown = matrix.ToMarkdown();
        var exception = Assert.Throws<ArgumentException>(() => matrix.Add(new ProviderCapabilityEvidence("sqlite",
            "query", "SCALAR", ProviderCapabilityEvidenceState.Declared)));

        // Assert
        Assert.Contains("\\|", markdown, StringComparison.Ordinal);
        Assert.Contains("NotExecuted", markdown, StringComparison.Ordinal);
        Assert.Contains("不能重复", exception.Message, StringComparison.Ordinal);
        Assert.Contains("NotExecuted", matrix.ToJson(), StringComparison.Ordinal);
        Assert.False(matrix.IsReleaseReady);
    }

    /// <summary>
    /// 测试目的：真实执行状态不能由固定声明伪造，避免静态矩阵冒充运行证据。
    /// </summary>
    [Fact]
    public void Scenario_WhenFixedStateIsRealIntegrationProven_ShouldRejectStaticEvidence()
    {
        // Arrange and Act
        var exception = Assert.Throws<ArgumentException>(() => new ProviderContractScenario("SQLite", "Query",
            "scalar", fixedState: ProviderCapabilityEvidenceState.RealIntegrationProven));

        // Assert
        Assert.Contains("真实执行状态", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：执行场景与固定状态不能同时出现，避免忽略执行委托而伪造固定结论。
    /// </summary>
    [Fact]
    public void Scenario_WhenExecuteAndFixedStateAreBothProvided_ShouldRejectAmbiguousEvidence()
    {
        // Arrange and Act
        var exception = Assert.Throws<ArgumentException>(() => new ProviderContractScenario("SQLite", "Query",
            "scalar", _ => Task.CompletedTask, ProviderCapabilityEvidenceState.Unsupported));

        // Assert
        Assert.Contains("同时", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：公开证据模型不得直接构造真实集成状态，真实状态只能由 runner 产生。
    /// </summary>
    [Fact]
    public void Evidence_WhenRealIntegrationStateIsConstructedDirectly_ShouldRejectStaticEvidence()
    {
        // Arrange and Act
        var exception = Assert.Throws<ArgumentException>(() => new ProviderCapabilityEvidence("SQLite", "Query",
            "scalar", ProviderCapabilityEvidenceState.RealIntegrationProven));

        // Assert
        Assert.Contains("真实执行状态", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：完整真实集成元数据才允许执行场景进入真实集成状态，测试制品不能直接放行发布门禁。
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenTrustedMetadataIsProvided_ShouldRecordTraceableIntegrationEvidence()
    {
        // Arrange and Act
        var startedAt = DateTimeOffset.UtcNow;
        var scenarios = new[]
        {
            new ProviderContractScenario("SQLite", "Query", "scalar", _ => Task.CompletedTask,
                realIntegrationEvidenceFactory: () => new ProviderIntegrationEvidenceMetadata(
                    "1.0", "3.0", "6.0", ProviderIntegrationConnectionKind.LocalFile, "test", "test.trx",
                    "test.json", startedAt, DateTimeOffset.UtcNow, "source"))
        };
        var matrix = new ProviderCapabilityMatrix();
        foreach (var item in await ProviderContractRunner.RunAsync(scenarios))
            matrix.Add(item);

        // Assert
        Assert.Equal(ProviderCapabilityEvidenceState.RealIntegrationProven, matrix.Entries[0].State);
        Assert.Equal("6.0", matrix.Entries[0].IntegrationEvidence.DriverVersion);
        Assert.False(matrix.IsReleaseReady);
    }

    /// <summary>
    /// 测试目的：证据路径必须拒绝绝对路径、路径穿越和连接信息，避免制品写出工作区或泄露凭据。
    /// </summary>
    [Theory]
    [InlineData("C:\\temp\\result.trx")]
    [InlineData("artifacts/test-results/../secret.trx")]
    [InlineData("artifacts/test-results/Password=secret.trx")]
    public void EvidenceMetadata_WhenArtifactPathIsUnsafe_ShouldRejectPath(string path)
    {
        // Arrange and Act
        var exception = Assert.Throws<ArgumentException>(() => new ProviderIntegrationEvidenceMetadata(
            "1.0", "3.0", "6.0", ProviderIntegrationConnectionKind.LocalFile, "test", path, "test.json",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "source"));

        // Assert
        Assert.Contains("路径", exception.Message, StringComparison.Ordinal);
    }

}