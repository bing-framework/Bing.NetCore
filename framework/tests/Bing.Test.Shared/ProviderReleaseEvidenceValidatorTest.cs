using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Xunit;

namespace Bing.Test.Shared;

/// <summary>
/// Provider 发布证据验证器测试。
/// </summary>
public sealed class ProviderReleaseEvidenceValidatorTest
{
    /// <summary>
    /// 测试目的：当前运行的成功 TRX 和摘要通过验证后，才能创建带当前运行绑定的 ReleaseEvidence。
    /// </summary>
    [Fact]
    public void Write_WhenCurrentSuccessfulArtifactsMatch_ShouldCreateTrustedReleaseEvidence()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();

        // Act
        var matrix = ProviderReleaseEvidenceWriter.Write(fixture.Request);

        // Assert
        var metadata = Assert.Single(matrix.Entries,
            item => item.State == ProviderCapabilityEvidenceState.RealIntegrationProven).IntegrationEvidence;
        Assert.False(matrix.IsReleaseReady);
        Assert.False(matrix.IsProviderRunReady);
        Assert.Equal(ProviderCapabilityArtifactKind.ReleaseEvidence, metadata.ArtifactKind);
        Assert.Equal(fixture.Request.RunId, metadata.RunId);
        Assert.Equal(fixture.Request.Framework, metadata.Framework);
        Assert.Equal(fixture.Request.Provider, metadata.Provider);
        Assert.True(File.Exists(Path.Combine(fixture.Directory, "provider-capability-matrix.json")));
        Assert.True(File.Exists(Path.Combine(fixture.Directory, "provider-capability-matrix.md")));
    }

    /// <summary>
    /// 测试目的：发布证据不得引用当前运行目录以外的伪造 TRX 路径。
    /// </summary>
    [Fact]
    public void Validate_WhenTrxPathIsOutsideCurrentRun_ShouldRejectForgery()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();
        fixture.Request.TrxRelativePath = "artifacts/provider-test-results/other-run/postgresql-net8.0.trx";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("当前 Provider 运行目录", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：历史 TRX 即使内容成功也不得被重新绑定为当前发布运行。
    /// </summary>
    [Fact]
    public void Validate_WhenTrxIsHistoric_ShouldRejectArtifact()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();
        File.SetLastWriteTimeUtc(fixture.TrxPath, DateTime.UtcNow.AddHours(-1));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("时间窗口", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：已绑定同一证据会话的归档制品超过当前运行 freshness 后，仍应通过完整性验证。
    /// </summary>
    [Fact]
    public void ValidateArchived_WhenCompletedMoreThanFreshnessWindowAgo_ShouldAcceptMatchingEvidence()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create(age: TimeSpan.FromHours(1));

        // Act
        var run = ProviderReleaseEvidenceValidator.ValidateArchived(fixture.Request,
            fixture.Request.EvidenceSessionId);

        // Assert
        Assert.Equal(fixture.Request.EvidenceSessionId, run.EvidenceSessionId);
        Assert.Equal(fixture.Request.RunId, run.RunId);
    }

    /// <summary>
    /// 测试目的：归档制品的 EvidenceSessionId 与聚合会话不一致时必须拒绝，避免重放历史制品。
    /// </summary>
    [Fact]
    public void ValidateArchived_WhenEvidenceSessionDoesNotMatch_ShouldRejectArtifact()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.ValidateArchived(
            fixture.Request, "different-evidence-session"));

        // Assert
        Assert.Contains("聚合会话", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：TestGenerated 能力矩阵不能被重新解释为归档 ReleaseEvidence。
    /// </summary>
    [Fact]
    public void ValidateArchived_WhenMatrixUsesTestGenerated_ShouldRejectArtifact()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();
        var matrixPath = Path.Combine(fixture.Directory, "provider-capability-matrix.json");
        var matrix = JsonDocument.Parse(File.ReadAllText(matrixPath, Encoding.UTF8));
        var root = matrix.RootElement;
        var entry = root.GetProperty("Entries")[0];
        var integrationEvidence = entry.GetProperty("IntegrationEvidence");
        var tampered = new
        {
            ReleaseReady = false,
            ProviderRunReady = true,
            Entries = new[]
            {
                new
                {
                    Provider = entry.GetProperty("Provider").GetString(),
                    Capability = entry.GetProperty("Capability").GetString(),
                    Scenario = entry.GetProperty("Scenario").GetString(),
                    State = entry.GetProperty("State").GetString(),
                    Evidence = entry.GetProperty("Evidence").GetString(),
                    Artifact = entry.GetProperty("Artifact").GetString(),
                    IntegrationEvidence = new
                    {
                        ProviderVersion = integrationEvidence.GetProperty("ProviderVersion").GetString(),
                        DatabaseVersion = integrationEvidence.GetProperty("DatabaseVersion").GetString(),
                        DriverVersion = integrationEvidence.GetProperty("DriverVersion").GetString(),
                        ConnectionKind = integrationEvidence.GetProperty("ConnectionKind").GetString(),
                        TestMethod = integrationEvidence.GetProperty("TestMethod").GetString(),
                        TrxPath = integrationEvidence.GetProperty("TrxPath").GetString(),
                        ArtifactPath = integrationEvidence.GetProperty("ArtifactPath").GetString(),
                        StartedAtUtc = integrationEvidence.GetProperty("StartedAtUtc").GetString(),
                        CompletedAtUtc = integrationEvidence.GetProperty("CompletedAtUtc").GetString(),
                        ArtifactKind = nameof(ProviderCapabilityArtifactKind.TestGenerated),
                        SourceIdentity = integrationEvidence.GetProperty("SourceIdentity").GetString(),
                        Provider = integrationEvidence.GetProperty("Provider").GetString(),
                        Framework = integrationEvidence.GetProperty("Framework").GetString(),
                        RunId = integrationEvidence.GetProperty("RunId").GetString(),
                        BinaryManifestPath = integrationEvidence.GetProperty("BinaryManifestPath").GetString(),
                        EvidenceSessionId = integrationEvidence.GetProperty("EvidenceSessionId").GetString(),
                        RuntimeVersion = integrationEvidence.GetProperty("RuntimeVersion").GetString(),
                        OperatingSystem = integrationEvidence.GetProperty("OperatingSystem").GetString(),
                        SourceState = integrationEvidence.GetProperty("SourceState").GetString()
                    }
                }
            }
        };
        File.WriteAllText(matrixPath, JsonSerializer.Serialize(tampered), new UTF8Encoding(false));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.ValidateArchived(
            fixture.Request, fixture.Request.EvidenceSessionId));

        // Assert
        Assert.Contains("TestGenerated", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：摘要 source identity 与当前 runner 不一致时必须拒绝发布证据。
    /// </summary>
    [Fact]
    public void Validate_WhenSourceIdentityDoesNotMatch_ShouldRejectArtifact()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create(sourceIdentity: "git=expected",
            summarySourceIdentity: "git=other");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("SourceIdentity", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：摘要 Provider 或目标框架与当前 runner 不一致时必须拒绝发布证据。
    /// </summary>
    [Theory]
    [InlineData("SqlServer", "net8.0")]
    [InlineData("PostgreSql", "net6.0")]
    public void Validate_WhenProviderOrFrameworkDoesNotMatch_ShouldRejectArtifact(string summaryProvider,
        string summaryFramework)
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create(summaryProvider: summaryProvider,
            summaryFramework: summaryFramework);

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.True(exception.Message.Contains("Provider", StringComparison.Ordinal) ||
                    exception.Message.Contains("Framework", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：包含失败结果的 TRX 不能进入 ReleaseEvidence 创建路径。
    /// </summary>
    [Fact]
    public void Validate_WhenTrxContainsFailure_ShouldRejectArtifact()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create(outcome: "Failed");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("零失败", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：包含核心跳过结果的 TRX 不能进入 ReleaseEvidence 创建路径。
    /// </summary>
    [Fact]
    public void Validate_WhenTrxContainsCoreSkip_ShouldRejectArtifact()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create(outcome: "NotExecuted");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("零核心跳过", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：脏工作树不能通过 Release Evidence 验证，避免将未提交源码误绑定到可发布制品。
    /// </summary>
    [Fact]
    public void Validate_WhenSourceStateIsDirty_ShouldRejectReleaseEvidence()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create(sourceState: "dirty");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("clean source", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：发布证据不得接受 not-recorded 等占位版本，避免把不完整元数据伪装成 RC 证据。
    /// </summary>
    [Fact]
    public void Validate_WhenReleaseMetadataIsPlaceholder_ShouldRejectReleaseEvidence()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create(providerVersion: "not-recorded");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("占位值", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：缺少二进制 manifest 时不得把成功 TRX 绑定为发布级证据。
    /// </summary>
    [Fact]
    public void Validate_WhenBinaryManifestIsMissing_ShouldRejectReleaseEvidence()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();
        File.Delete(fixture.BinaryManifestPath);

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("制品不存在", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：二进制 manifest 中的哈希与实际 DLL 不一致时必须拒绝发布级证据。
    /// </summary>
    [Fact]
    public void Validate_WhenBinaryManifestHashDoesNotMatch_ShouldRejectReleaseEvidence()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();
        var manifest = JsonDocument.Parse(File.ReadAllText(fixture.BinaryManifestPath, Encoding.UTF8));
        var root = manifest.RootElement;
        var entries = root.GetProperty("Assemblies").EnumerateArray().Select((entry, index) => new
        {
            Path = entry.GetProperty("Path").GetString(),
            Sha256 = index == 0 ? new string('0', 64) : entry.GetProperty("Sha256").GetString()
        }).ToArray();
        var tampered = new
        {
            Provider = root.GetProperty("Provider").GetString(),
            Framework = root.GetProperty("Framework").GetString(),
            RunId = root.GetProperty("RunId").GetString(),
            EvidenceSessionId = root.GetProperty("EvidenceSessionId").GetString(),
            SourceIdentity = root.GetProperty("SourceIdentity").GetString(),
            SourceState = root.GetProperty("SourceState").GetString(),
            Assemblies = entries
        };
        File.WriteAllText(fixture.BinaryManifestPath, JsonSerializer.Serialize(tampered), new UTF8Encoding(false));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("哈希与实际文件不一致", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：manifest 生成后程序集发生替换时必须拒绝发布级证据，避免陈旧或替换的测试输出被复用。
    /// </summary>
    [Fact]
    public void Validate_WhenBinaryChangesAfterManifest_ShouldRejectReleaseEvidence()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();
        File.WriteAllText(Path.Combine(fixture.Directory, "Bing.Dapper.PostgreSql.dll"),
            "stale-binary", new UTF8Encoding(false));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("哈希与实际文件不一致", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：二进制 manifest 不得引用运行目录之外的构建输出，确保跨 job 归档后仍可重新验证。
    /// </summary>
    [Fact]
    public void Validate_WhenBinaryManifestReferencesOutsideRun_ShouldRejectReleaseEvidence()
    {
        // Arrange
        using var fixture = ProviderReleaseEvidenceFixture.Create();
        var manifest = JsonDocument.Parse(File.ReadAllText(fixture.BinaryManifestPath, Encoding.UTF8));
        var root = manifest.RootElement;
        var assemblies = root.GetProperty("Assemblies").EnumerateArray().Select(entry => new
        {
            Path = entry.GetProperty("Path").GetString().Replace(
                $"artifacts/provider-test-results/{fixture.Request.RunId}/", "artifacts/provider-test-results/other-run/",
                StringComparison.OrdinalIgnoreCase),
            Sha256 = entry.GetProperty("Sha256").GetString()
        }).ToArray();
        var tampered = new
        {
            Provider = root.GetProperty("Provider").GetString(),
            Framework = root.GetProperty("Framework").GetString(),
            RunId = root.GetProperty("RunId").GetString(),
            EvidenceSessionId = root.GetProperty("EvidenceSessionId").GetString(),
            SourceIdentity = root.GetProperty("SourceIdentity").GetString(),
            SourceState = root.GetProperty("SourceState").GetString(),
            Assemblies = assemblies
        };
        File.WriteAllText(fixture.BinaryManifestPath, JsonSerializer.Serialize(tampered), new UTF8Encoding(false));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ProviderReleaseEvidenceValidator.Validate(fixture.Request));

        // Assert
        Assert.Contains("当前运行目录", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：共享程序集内手工构造的运行令牌未经过验证器登记时，不能伪造 ReleaseEvidence。
    /// </summary>
    [Fact]
    public void CreateReleaseEvidence_WhenRunTokenWasNotValidated_ShouldRejectForgery()
    {
        // Arrange
        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = new ProviderReleaseEvidenceValidator.ProviderValidatedReleaseRun("PostgreSql", "net8.0",
            "forged-run", "git=forged", startedAtUtc, startedAtUtc,
            "artifacts/provider-test-results/forged-run/postgresql-net8.0.trx",
            "artifacts/provider-test-results/forged-run/postgresql-net8.0.json",
            "artifacts/provider-test-results/forged-run/provider-capability-matrix.json",
            "artifacts/provider-test-results/forged-run/provider-capability-matrix.md",
            "matrix.json", "matrix.md", new HashSet<string>(StringComparer.Ordinal));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProviderIntegrationEvidenceMetadata.CreateValidatedReleaseEvidence(run,
                "PostgreSqlQueryTest.ExecuteScalar_ShouldReturnActualCount"));

        // Assert
        Assert.Contains("验证器", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：能力目录应为五类 Provider 的每个 T14 能力提供唯一、可解释的六态条目。
    /// </summary>
    [Fact]
    public void Catalog_WhenBaselineIsCreated_ShouldCoverEveryProviderCapabilityWithUniqueKeys()
    {
        // Arrange
        var expectedCapabilities = new[]
        {
            "Core query", "Parameters and null", "Type mapping", "Streaming and resource release", "Cancellation", "DML / mutation / batch",
            "Transactions", "Procedures / output / input-output", "Multiple result / Returning / Output"
        };

        // Act
        var definitions = ProviderCapabilityCatalog.GetDefinitions();
        var matrix = ProviderCapabilityCatalog.CreateBaselineMatrix();

        // Assert
        Assert.Equal(definitions.Count, matrix.Entries.Count);
        Assert.Equal(definitions.Count, definitions.Select(item => $"{item.Provider}|{item.Capability}|{item.Scenario}")
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var provider in new[] { "MySql", "PostgreSql", "SqlServer", "SQLite", "Oracle", "Doris" })
            Assert.All(expectedCapabilities, capability => Assert.Contains(definitions,
                item => item.Provider == provider && item.Capability == capability));
        Assert.Contains(definitions, item => item.State == ProviderCapabilityEvidenceState.Unsupported);
        Assert.Contains(definitions, item => item.Provider == "PostgreSql" &&
            item.Scenario == "output parameter semantics" &&
            item.State == ProviderCapabilityEvidenceState.Unsupported);
        Assert.Contains(definitions, item => item.State == ProviderCapabilityEvidenceState.ImplementationGap);
        Assert.All(matrix.Entries, item => Assert.True(Enum.IsDefined(item.State)));
        Assert.False(matrix.IsReleaseReady);
    }

    private sealed class ProviderReleaseEvidenceFixture : IDisposable
    {
        private ProviderReleaseEvidenceFixture(string directory, string trxPath, ProviderReleaseEvidenceRequest request)
        {
            Directory = directory;
            TrxPath = trxPath;
            Request = request;
        }

        public string Directory { get; }

        public string TrxPath { get; }

        public string BinaryManifestPath => Path.Combine(Directory, "provider-binary-manifest.json");

        public ProviderReleaseEvidenceRequest Request { get; }

        public static ProviderReleaseEvidenceFixture Create(string outcome = "Passed", string sourceIdentity = "git=expected",
            string summarySourceIdentity = null, string summaryProvider = "PostgreSql", string summaryFramework = "net8.0",
            string providerVersion = "7.0.0", string sourceState = "clean", TimeSpan? age = null)
        {
            var workspaceRoot = FindWorkspaceRoot();
            var runId = "provider-evidence-" + Guid.NewGuid().ToString("N");
            var evidenceSessionId = "evidence-session-" + Guid.NewGuid().ToString("N");
            var relativeDirectory = $"artifacts/provider-test-results/{runId}";
            var directory = Path.Combine(workspaceRoot, "artifacts", "provider-test-results", runId);
            System.IO.Directory.CreateDirectory(directory);
            var trxRelativePath = $"{relativeDirectory}/postgresql-net8.0.trx";
            var summaryRelativePath = $"{relativeDirectory}/postgresql-net8.0.json";
            var trxPath = Path.Combine(directory, "postgresql-net8.0.trx");
            var summaryPath = Path.Combine(directory, "postgresql-net8.0.json");
            var binaryManifestRelativePath = $"{relativeDirectory}/provider-binary-manifest.json";
            var passed = outcome == "Passed" ? 1 : 0;
            var failed = outcome == "Failed" ? 1 : 0;
            var skipped = outcome == "NotExecuted" ? 1 : 0;
            var completedAtUtc = DateTimeOffset.UtcNow - (age ?? TimeSpan.Zero);
            var startedAtUtc = completedAtUtc.AddSeconds(-1);
            var trx = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><TestRun><Results><UnitTestResult testName=\"Bing.Dapper.Tests.SqlQuery.PostgreSqlQueryTest.ExecuteScalar_ShouldReturnActualCount\" outcome=\"{outcome}\" /></Results><ResultSummary duration=\"00:00:01\"><Counters total=\"1\" passed=\"{passed}\" failed=\"{failed}\" /></ResultSummary></TestRun>";
            File.WriteAllText(trxPath, trx, new UTF8Encoding(false));
            var binaryNames = new[]
            {
                "Bing.Dapper.PostgreSql.Tests.Integration.dll",
                "Bing.Dapper.PostgreSql.dll",
                "Bing.Data.Sql.dll"
            };
            var binaryManifest = new
            {
                Provider = "PostgreSql",
                Framework = "net8.0",
                RunId = runId,
                EvidenceSessionId = evidenceSessionId,
                SourceIdentity = sourceIdentity,
                SourceState = sourceState,
                Assemblies = binaryNames.Select(name =>
                {
                    var path = Path.Combine(directory, name);
                    File.WriteAllText(path, name, new UTF8Encoding(false));
                    using var stream = File.OpenRead(path);
                    using var sha256 = SHA256.Create();
                    return new
                    {
                        Path = path[(workspaceRoot.Length + 1)..].Replace('\\', '/'),
                        Sha256 = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant()
                    };
                }).ToArray()
            };
            File.WriteAllText(Path.Combine(directory, "provider-binary-manifest.json"),
                JsonSerializer.Serialize(binaryManifest), new UTF8Encoding(false));
            var summary = new
            {
                Provider = summaryProvider,
                Framework = summaryFramework,
                Discovered = 1,
                Passed = passed,
                Failed = failed,
                Skipped = skipped,
                OptionalSkipped = 0,
                CoreSkipped = skipped,
                Executed = passed + failed,
                RunId = runId,
                EvidenceSessionId = evidenceSessionId,
                StartedAtUtc = startedAtUtc.ToString("O"),
                CompletedAtUtc = completedAtUtc.ToString("O"),
                SourceIdentity = summarySourceIdentity ?? sourceIdentity,
                ProviderVersion = providerVersion,
                DatabaseVersion = "16.4",
                DriverVersion = "6.0.11",
                RuntimeVersion = "8.0.11",
                OperatingSystem = "Windows",
                SourceState = sourceState,
                TrxPath = trxRelativePath,
                ArtifactPath = summaryRelativePath,
                BinaryManifestPath = binaryManifestRelativePath
            };
            File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary), new UTF8Encoding(false));
            var matrix = new
            {
                ReleaseReady = false,
                ProviderRunReady = true,
                Entries = new[]
                {
                    new
                    {
                        Provider = "PostgreSql",
                        Capability = "Core query",
                        Scenario = "scalar/list/single",
                        State = nameof(ProviderCapabilityEvidenceState.RealIntegrationProven),
                        Evidence = "TRX 已通过：Bing.Dapper.Tests.SqlQuery.PostgreSqlQueryTest.ExecuteScalar_ShouldReturnActualCount",
                        Artifact = relativeDirectory + "/provider-capability-matrix.json",
                        IntegrationEvidence = new
                        {
                            ProviderVersion = providerVersion,
                            DatabaseVersion = "16.4",
                            DriverVersion = "6.0.11",
                            ConnectionKind = nameof(ProviderIntegrationConnectionKind.RemoteTestDatabase),
                            TestMethod = "PostgreSqlQueryTest.ExecuteScalar_ShouldReturnActualCount",
                            TrxPath = trxRelativePath,
                            ArtifactPath = relativeDirectory + "/provider-capability-matrix.json",
                            StartedAtUtc = startedAtUtc.ToString("O"),
                            CompletedAtUtc = completedAtUtc.ToString("O"),
                            ArtifactKind = nameof(ProviderCapabilityArtifactKind.ReleaseEvidence),
                            SourceIdentity = sourceIdentity,
                            Provider = "PostgreSql",
                            Framework = "net8.0",
                            RunId = runId,
                            BinaryManifestPath = binaryManifestRelativePath,
                            EvidenceSessionId = evidenceSessionId,
                            RuntimeVersion = "8.0.11",
                            OperatingSystem = "Windows",
                            SourceState = sourceState
                        }
                    }
                }
            };
            File.WriteAllText(Path.Combine(directory, "provider-capability-matrix.json"),
                JsonSerializer.Serialize(matrix), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "provider-capability-matrix.md"), "# Matrix",
                new UTF8Encoding(false));
            return new ProviderReleaseEvidenceFixture(directory, trxPath, new ProviderReleaseEvidenceRequest
            {
                WorkspaceRoot = workspaceRoot,
                Provider = "PostgreSql",
                Framework = "net8.0",
                RunId = runId,
                EvidenceSessionId = evidenceSessionId,
                TrxRelativePath = trxRelativePath,
                SummaryRelativePath = summaryRelativePath,
                MatrixJsonRelativePath = $"{relativeDirectory}/provider-capability-matrix.json",
                MatrixMarkdownRelativePath = $"{relativeDirectory}/provider-capability-matrix.md",
                BinaryManifestRelativePath = binaryManifestRelativePath,
                SourceIdentity = sourceIdentity,
                ProviderVersion = providerVersion,
                DatabaseVersion = "16.4",
                DriverVersion = "6.0.11",
                RuntimeVersion = "8.0.11",
                OperatingSystem = "Windows",
                SourceState = sourceState,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc
            });
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, true);
        }

        private static string FindWorkspaceRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Bing.All.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("未找到工作区根目录。");
        }
    }
}
