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
        DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, string sourceIdentity)
        : this(providerVersion, databaseVersion, driverVersion, connectionKind, testMethod, trxPath, artifactPath,
            startedAtUtc, completedAtUtc, ProviderCapabilityArtifactKind.TestGenerated, sourceIdentity, false)
    {
    }

    private ProviderIntegrationEvidenceMetadata(string providerVersion, string databaseVersion,
        string driverVersion, ProviderIntegrationConnectionKind connectionKind, string testMethod, string trxPath,
        string artifactPath,
        DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, ProviderCapabilityArtifactKind artifactKind,
        string sourceIdentity, bool allowReleaseEvidence)
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
    }

    internal static ProviderIntegrationEvidenceMetadata CreateReleaseEvidence(string providerVersion,
        string databaseVersion, string driverVersion, ProviderIntegrationConnectionKind connectionKind,
        string testMethod, string trxPath, string artifactPath, DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc, string sourceIdentity) => new(providerVersion, databaseVersion,
        driverVersion, connectionKind, testMethod, trxPath, artifactPath, startedAtUtc, completedAtUtc,
        ProviderCapabilityArtifactKind.ReleaseEvidence, sourceIdentity, true);

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

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value.Trim();

    private static string RequireSafePath(string value, string parameterName)
    {
        var path = Require(value, parameterName);
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

    public bool IsReleaseReady => _entries.Count > 0 && _entries.All(IsReleaseReadyEvidence);

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
        builder.AppendLine("| Provider | Capability | Scenario | State | Evidence | Provider Version | Database Version | Driver Version | Connection Kind | Test Method | TRX | Artifact | Started UTC | Completed UTC | Artifact Kind | Source Identity |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
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
                metadata?.SourceIdentity
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
                entry.IntegrationEvidence.SourceIdentity
            }
        });
        return JsonSerializer.Serialize(new { ReleaseReady = IsReleaseReady, Entries = entries },
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

    private static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static bool IsReleaseReadyEvidence(ProviderCapabilityEvidence evidence) =>
        evidence.State == ProviderCapabilityEvidenceState.Unsupported ||
        evidence.State == ProviderCapabilityEvidenceState.RealIntegrationProven &&
        evidence.IntegrationEvidence?.ArtifactKind == ProviderCapabilityArtifactKind.ReleaseEvidence;
}