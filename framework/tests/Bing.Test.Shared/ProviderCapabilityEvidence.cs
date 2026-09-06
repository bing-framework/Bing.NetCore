using System.Text;
using System.Text.Json;

namespace Bing.Test.Shared;

/// <summary>
/// Provider 能力证据状态。
/// </summary>
public enum ProviderCapabilityEvidenceState
{
    Declared,
    UnitProven,
    RealIntegrationProven,
    Unsupported,
    ImplementationGap,
    NotExecuted
}

/// <summary>
/// Provider 真实集成证据制品类别。
/// </summary>
public enum ProviderCapabilityArtifactKind
{
    TestGenerated,
    ReleaseEvidence
}

/// <summary>
/// Provider 真实集成连接类别。
/// </summary>
public enum ProviderIntegrationConnectionKind
{
    LocalFile,
    Container,
    RemoteTestDatabase
}

/// <summary>
/// Provider 真实集成证据的可追溯元数据。
/// </summary>
public sealed class ProviderIntegrationEvidenceMetadata
{
    public ProviderIntegrationEvidenceMetadata(string providerVersion, string databaseVersion,
        string driverVersion, ProviderIntegrationConnectionKind connectionKind, string testMethod, string trxPath,
        string artifactPath,
        DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, string sourceIdentity,
        string runtimeVersion = null, string operatingSystem = null, string sourceState = null,
        string evidenceSessionId = null)
        : this(providerVersion, databaseVersion, driverVersion, connectionKind, testMethod, trxPath, artifactPath,
            startedAtUtc, completedAtUtc, ProviderCapabilityArtifactKind.TestGenerated, sourceIdentity, false,
            runtimeVersion: runtimeVersion, operatingSystem: operatingSystem, sourceState: sourceState,
            evidenceSessionId: evidenceSessionId)
    {
    }

    private ProviderIntegrationEvidenceMetadata(string providerVersion, string databaseVersion,
        string driverVersion, ProviderIntegrationConnectionKind connectionKind, string testMethod, string trxPath,
        string artifactPath,
        DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, ProviderCapabilityArtifactKind artifactKind,
        string sourceIdentity, bool allowReleaseEvidence, string provider = null, string framework = null,
        string runId = null, string binaryManifestPath = null, string runtimeVersion = null,
        string operatingSystem = null, string sourceState = null, string evidenceSessionId = null)
    {
        ProviderVersion = Require(providerVersion, nameof(providerVersion));
        DatabaseVersion = Require(databaseVersion, nameof(databaseVersion));
        DriverVersion = Require(driverVersion, nameof(driverVersion));
        ConnectionKind = connectionKind;
        TestMethod = Require(testMethod, nameof(testMethod));
        TrxPath = RequireSafePath(trxPath, nameof(trxPath));
        ArtifactPath = RequireSafePath(artifactPath, nameof(artifactPath));
        if (startedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("真实集成证据开始时间必须使用 UTC。", nameof(startedAtUtc));
        if (completedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("真实集成证据完成时间必须使用 UTC。", nameof(completedAtUtc));
        if (completedAtUtc < startedAtUtc)
            throw new ArgumentException("真实集成证据完成时间不能早于开始时间。", nameof(completedAtUtc));
        if (artifactKind == ProviderCapabilityArtifactKind.ReleaseEvidence && !allowReleaseEvidence)
            throw new ArgumentException("发布级制品必须由可信验证路径创建。", nameof(artifactKind));
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        ArtifactKind = artifactKind;
        SourceIdentity = Require(sourceIdentity, nameof(sourceIdentity));
        Provider = provider;
        Framework = framework;
        RunId = runId;
        BinaryManifestPath = binaryManifestPath;
        EvidenceSessionId = string.IsNullOrWhiteSpace(evidenceSessionId)
            ? null
            : RequireEvidenceSessionId(evidenceSessionId);
        RuntimeVersion = runtimeVersion;
        OperatingSystem = operatingSystem;
        SourceState = sourceState;
        IsTrustedReleaseEvidence = allowReleaseEvidence;
    }

    internal static ProviderIntegrationEvidenceMetadata CreateValidatedReleaseEvidence(
        ProviderReleaseEvidenceValidator.ProviderValidatedReleaseRun run, string testMethod)
    {
        ProviderReleaseEvidenceValidator.EnsureValidated(run);
        return new ProviderIntegrationEvidenceMetadata(run.ProviderVersion, run.DatabaseVersion, run.DriverVersion,
            run.ConnectionKind, testMethod, run.TrxRelativePath, run.MatrixJsonRelativePath, run.StartedAtUtc,
            run.CompletedAtUtc, ProviderCapabilityArtifactKind.ReleaseEvidence, run.SourceIdentity, true,
            run.Provider, run.Framework, run.RunId, run.BinaryManifestRelativePath, run.RuntimeVersion,
            run.OperatingSystem, run.SourceState, run.EvidenceSessionId);
    }

    public string ProviderVersion { get; }

    public string DatabaseVersion { get; }

    public string DriverVersion { get; }

    public ProviderIntegrationConnectionKind ConnectionKind { get; }

    public string TestMethod { get; }

    public string TrxPath { get; }

    public string ArtifactPath { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public ProviderCapabilityArtifactKind ArtifactKind { get; }

    public string SourceIdentity { get; }

    /// <summary>
    /// Provider 名称，仅在发布级受信证据中存在。
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// 目标框架，仅在发布级受信证据中存在。
    /// </summary>
    public string Framework { get; }

    /// <summary>
    /// 受信 CI 运行标识，仅在发布级受信证据中存在。
    /// </summary>
    public string RunId { get; }

    public string BinaryManifestPath { get; }

    public string EvidenceSessionId { get; }

    public string RuntimeVersion { get; }

    public string OperatingSystem { get; }

    public string SourceState { get; }

    internal bool IsTrustedReleaseEvidence { get; }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value.Trim();

    private static string RequireEvidenceSessionId(string value)
    {
        var sessionId = Require(value, nameof(value));
        if (!System.Text.RegularExpressions.Regex.IsMatch(sessionId,
                "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$"))
            throw new ArgumentException("证据会话标识格式无效。", nameof(value));
        return sessionId;
    }

    private static string RequireSafePath(string value, string parameterName)
    {
        var path = Require(value, parameterName);
        if (Path.IsPathRooted(path) || path.Contains('\0') ||
            path.Split(new[] { '/', '\\' }, StringSplitOptions.None)
                .Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("证据路径必须是无路径穿越的相对路径。", parameterName);
        if (path.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("证据路径不能包含连接信息。", parameterName);
        return path;
    }
}

/// <summary>
/// 单个 Provider 能力场景的证据。
/// </summary>
public sealed class ProviderCapabilityEvidence
{
    public ProviderCapabilityEvidence(string provider, string capability, string scenario,
        ProviderCapabilityEvidenceState state, string evidence = null, string artifact = null,
        ProviderIntegrationEvidenceMetadata integrationEvidence = null)
        : this(provider, capability, scenario, state, evidence, artifact, integrationEvidence, false)
    {
    }

    private ProviderCapabilityEvidence(string provider, string capability, string scenario,
        ProviderCapabilityEvidenceState state, string evidence, string artifact,
        ProviderIntegrationEvidenceMetadata integrationEvidence, bool allowRealIntegration)
    {
        if (state == ProviderCapabilityEvidenceState.RealIntegrationProven && !allowRealIntegration)
            throw new ArgumentException("真实执行状态必须由场景执行结果产生。", nameof(state));
        if (state == ProviderCapabilityEvidenceState.RealIntegrationProven && integrationEvidence == null)
            throw new ArgumentException("真实执行状态必须携带完整证据元数据。", nameof(integrationEvidence));
        if (state != ProviderCapabilityEvidenceState.RealIntegrationProven && integrationEvidence != null)
            throw new ArgumentException("非真实执行状态不能携带真实集成证据元数据。", nameof(integrationEvidence));
        Provider = Require(provider, nameof(provider));
        Capability = Require(capability, nameof(capability));
        Scenario = Require(scenario, nameof(scenario));
        State = state;
        Evidence = evidence ?? string.Empty;
        IntegrationEvidence = integrationEvidence;
        Artifact = artifact ?? integrationEvidence?.ArtifactPath ?? string.Empty;
    }

    internal static ProviderCapabilityEvidence CreateRealIntegration(string provider, string capability,
        string scenario, string evidence, string artifact,
        ProviderIntegrationEvidenceMetadata integrationEvidence) =>
        new(provider, capability, scenario, ProviderCapabilityEvidenceState.RealIntegrationProven,
            evidence, artifact, integrationEvidence, true);

    public string Provider { get; }

    public string Capability { get; }

    public string Scenario { get; }

    public ProviderCapabilityEvidenceState State { get; }

    public string Evidence { get; }

    public string Artifact { get; }

    public ProviderIntegrationEvidenceMetadata IntegrationEvidence { get; }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value.Trim();
}

/// <summary>
/// Provider 能力矩阵及其无密 Markdown 输出。
/// </summary>
public sealed class ProviderCapabilityMatrix
{
    private readonly List<ProviderCapabilityEvidence> _entries = new();

    public IReadOnlyList<ProviderCapabilityEvidence> Entries => _entries;

    public bool IsReleaseReady => false;

    public bool IsProviderRunReady => _entries.Count > 0 && _entries.All(IsReleaseReadyEvidence);

    public ProviderCapabilityMatrix Add(ProviderCapabilityEvidence evidence)
    {
        if (evidence == null)
            throw new ArgumentNullException(nameof(evidence));
        if (_entries.Any(item => string.Equals(item.Provider, evidence.Provider, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Capability, evidence.Capability, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Scenario, evidence.Scenario, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Provider 能力场景不能重复。", nameof(evidence));
        _entries.Add(evidence);
        return this;
    }

    public string ToMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("| Provider | Capability | Scenario | State | Evidence | Provider Version | Database Version | Driver Version | Connection Kind | Test Method | TRX | Artifact | Started UTC | Completed UTC | Artifact Kind | Source Identity | Framework | Run Id | Binary Manifest | Evidence Session | Runtime Version | Operating System | Source State |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var entry in _entries)
        {
            var metadata = entry.IntegrationEvidence;
            var values = new[]
            {
                entry.Provider,
                entry.Capability,
                entry.Scenario,
                entry.State.ToString(),
                entry.Evidence,
                metadata?.ProviderVersion,
                metadata?.DatabaseVersion,
                metadata?.DriverVersion,
                metadata?.ConnectionKind.ToString(),
                metadata?.TestMethod,
                metadata?.TrxPath,
                metadata?.ArtifactPath ?? entry.Artifact,
                metadata?.StartedAtUtc.ToString("O"),
                metadata?.CompletedAtUtc.ToString("O"),
                metadata?.ArtifactKind.ToString(),
                metadata?.SourceIdentity,
                metadata?.Framework,
                metadata?.RunId,
                metadata?.BinaryManifestPath,
                metadata?.EvidenceSessionId,
                metadata?.RuntimeVersion,
                metadata?.OperatingSystem,
                metadata?.SourceState
            };
            builder.Append('|').Append(string.Join(" | ", values.Select(value => Escape(value ?? string.Empty))))
                .AppendLine(" |");
        }
        return builder.ToString();
    }

    public string ToJson()
    {
        var entries = _entries.Select(entry => new
        {
            entry.Provider,
            entry.Capability,
            entry.Scenario,
            State = entry.State.ToString(),
            entry.Evidence,
            entry.Artifact,
            IntegrationEvidence = entry.IntegrationEvidence == null ? null : new
            {
                entry.IntegrationEvidence.ProviderVersion,
                entry.IntegrationEvidence.DatabaseVersion,
                entry.IntegrationEvidence.DriverVersion,
                ConnectionKind = entry.IntegrationEvidence.ConnectionKind.ToString(),
                entry.IntegrationEvidence.TestMethod,
                entry.IntegrationEvidence.TrxPath,
                entry.IntegrationEvidence.ArtifactPath,
                StartedAtUtc = entry.IntegrationEvidence.StartedAtUtc.ToString("O"),
                CompletedAtUtc = entry.IntegrationEvidence.CompletedAtUtc.ToString("O"),
                ArtifactKind = entry.IntegrationEvidence.ArtifactKind.ToString(),
                entry.IntegrationEvidence.SourceIdentity,
                entry.IntegrationEvidence.Provider,
                entry.IntegrationEvidence.Framework,
                entry.IntegrationEvidence.RunId,
                entry.IntegrationEvidence.BinaryManifestPath,
                entry.IntegrationEvidence.EvidenceSessionId,
                entry.IntegrationEvidence.RuntimeVersion,
                entry.IntegrationEvidence.OperatingSystem,
                entry.IntegrationEvidence.SourceState
            }
        });
        return JsonSerializer.Serialize(new
        {
            ReleaseReady = IsReleaseReady,
            ProviderRunReady = IsProviderRunReady,
            Entries = entries
        },
            new JsonSerializerOptions { WriteIndented = true });
    }

    public void WriteJson(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("制品路径不能为空。", nameof(path));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
    }

    /// <summary>
    /// 使用 UTF-8 写入无密 Markdown 能力矩阵。
    /// </summary>
    /// <param name="path">目标制品路径。</param>
    public void WriteMarkdown(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("制品路径不能为空。", nameof(path));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, ToMarkdown(), new UTF8Encoding(false));
    }

    private static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static bool IsReleaseReadyEvidence(ProviderCapabilityEvidence evidence) =>
        evidence.State == ProviderCapabilityEvidenceState.Unsupported ||
        evidence.State == ProviderCapabilityEvidenceState.RealIntegrationProven &&
        evidence.IntegrationEvidence?.ArtifactKind == ProviderCapabilityArtifactKind.ReleaseEvidence &&
        evidence.IntegrationEvidence.IsTrustedReleaseEvidence;
}

public sealed class ProviderReleaseReadinessRun
{
    public string Provider { get; init; }

    public string Framework { get; init; }

    public bool ReleaseEvidenceValid { get; init; }

    public int Executed { get; init; }

    public int Failed { get; init; }

    public int CoreSkipped { get; init; }

    public string EvidenceSessionId { get; init; }

    public string SourceIdentity { get; init; }

    public IReadOnlySet<string> PassedTestMethods { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);
}

public static class ProviderReleaseReadiness
{
    private static readonly string[] CoreProviders = { "MySql", "PostgreSql", "SqlServer", "SQLite" };
    private static readonly string[] RequiredFrameworks = { "net6.0", "net8.0" };

    public static bool IsReady(IReadOnlyCollection<ProviderReleaseReadinessRun> runs, bool unitTestsPassed,
        bool formalHostComplete, bool rs0026GatePassed, bool apiGatePassed)
        => IsReady(runs, unitTestsPassed, formalHostComplete, rs0026GatePassed, apiGatePassed,
            ProviderCapabilityCatalog.GetDefinitions());

    internal static bool IsReady(IReadOnlyCollection<ProviderReleaseReadinessRun> runs, bool unitTestsPassed,
        bool formalHostComplete, bool rs0026GatePassed, bool apiGatePassed,
        IReadOnlyCollection<ProviderCapabilityScenarioDefinition> definitions)
    {
        if (runs == null || runs.Any(run => run == null) || !unitTestsPassed || !formalHostComplete ||
            !rs0026GatePassed || !apiGatePassed || definitions == null)
            return false;
        var sessionIds = runs.Select(run => run.EvidenceSessionId).ToArray();
        if (sessionIds.Any(string.IsNullOrWhiteSpace) ||
            sessionIds.Distinct(StringComparer.Ordinal).Count() != 1)
            return false;
        var sourceIdentities = runs.Select(run => run.SourceIdentity).ToArray();
        if (sourceIdentities.Any(string.IsNullOrWhiteSpace) ||
            sourceIdentities.Distinct(StringComparer.Ordinal).Count() != 1)
            return false;
        foreach (var provider in CoreProviders)
        foreach (var framework in RequiredFrameworks)
        {
            var matches = runs.Where(item => string.Equals(item.Provider, provider,
                StringComparison.OrdinalIgnoreCase) && string.Equals(item.Framework, framework,
                StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
                return false;
            var run = matches[0];
            if (!run.ReleaseEvidenceValid || run.Executed <= 0 || run.Failed != 0 || run.CoreSkipped != 0 ||
                run.PassedTestMethods == null)
                return false;
            foreach (var definition in definitions.Where(item =>
                         string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase)))
            {
                if (definition.State is ProviderCapabilityEvidenceState.Unsupported or
                    ProviderCapabilityEvidenceState.UnitProven)
                    continue;
                if (definition.State == ProviderCapabilityEvidenceState.ImplementationGap ||
                    string.IsNullOrWhiteSpace(definition.TestMethod) ||
                    !run.PassedTestMethods.Any(method => method.EndsWith(definition.TestMethod,
                        StringComparison.Ordinal)))
                    return false;
            }
        }
        return true;
    }

    public static IReadOnlyList<string> GetMissingRuns(IReadOnlyCollection<ProviderReleaseReadinessRun> runs)
    {
        var result = new List<string>();
        foreach (var provider in CoreProviders)
        foreach (var framework in RequiredFrameworks)
        {
            var count = runs?.Count(item => item != null && string.Equals(item.Provider, provider,
                StringComparison.OrdinalIgnoreCase) && string.Equals(item.Framework, framework,
                StringComparison.OrdinalIgnoreCase)) ?? 0;
            if (count != 1)
                result.Add($"{provider}/{framework}");
        }
        return result;
    }
}