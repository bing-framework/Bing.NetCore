using System.Text;
using System.Text.Json;

namespace Bing.Test.Shared;

/// <summary>
/// Provider 能力证据状态。
/// </summary>
public enum ProviderCapabilityEvidenceState
{
    /// <summary>
    /// 已声明但尚未验证。
    /// </summary>
    Declared,
    /// <summary>
    /// 已通过单元测试验证。
    /// </summary>
    UnitProven,
    /// <summary>
    /// 已通过真实集成测试验证。
    /// </summary>
    RealIntegrationProven,
    /// <summary>
    /// Provider 明确不支持。
    /// </summary>
    Unsupported,
    /// <summary>
    /// 当前实现存在缺口。
    /// </summary>
    ImplementationGap,
    /// <summary>
    /// 尚未执行验证。
    /// </summary>
    NotExecuted
}

/// <summary>
/// Provider 真实集成证据制品类别。
/// </summary>
public enum ProviderCapabilityArtifactKind
{
    /// <summary>
    /// 测试过程生成的证据制品。
    /// </summary>
    TestGenerated,
    /// <summary>
    /// 经过发布验证的证据制品。
    /// </summary>
    ReleaseEvidence
}

/// <summary>
/// Provider 真实集成连接类别。
/// </summary>
public enum ProviderIntegrationConnectionKind
{
    /// <summary>
    /// 本地文件连接。
    /// </summary>
    LocalFile,
    /// <summary>
    /// 容器内数据库连接。
    /// </summary>
    Container,
    /// <summary>
    /// 远程测试数据库连接。
    /// </summary>
    RemoteTestDatabase
}

/// <summary>
/// Provider 真实集成证据的可追溯元数据。
/// </summary>
public sealed class ProviderIntegrationEvidenceMetadata
{
    /// <summary>
    /// 初始化一个 <see cref="ProviderIntegrationEvidenceMetadata"/> 类型的实例。
    /// </summary>
    /// <param name="providerVersion">Provider 版本。</param>
    /// <param name="databaseVersion">数据库版本。</param>
    /// <param name="driverVersion">驱动版本。</param>
    /// <param name="connectionKind">集成测试连接类别。</param>
    /// <param name="testMethod">测试方法名称。</param>
    /// <param name="trxPath">TRX 制品相对路径。</param>
    /// <param name="artifactPath">证据制品相对路径。</param>
    /// <param name="startedAtUtc">测试开始时间。</param>
    /// <param name="completedAtUtc">测试完成时间。</param>
    /// <param name="sourceIdentity">源码身份标识。</param>
    /// <param name="runtimeVersion">运行时版本。</param>
    /// <param name="operatingSystem">操作系统名称。</param>
    /// <param name="sourceState">源码工作区状态。</param>
    /// <param name="evidenceSessionId">证据聚合会话标识。</param>
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

    /// <summary>
    /// 初始化一个 <see cref="ProviderIntegrationEvidenceMetadata"/> 类型的实例。
    /// </summary>
    /// <param name="providerVersion">Provider 版本。</param>
    /// <param name="databaseVersion">数据库版本。</param>
    /// <param name="driverVersion">驱动版本。</param>
    /// <param name="connectionKind">集成测试连接类别。</param>
    /// <param name="testMethod">测试方法名称。</param>
    /// <param name="trxPath">TRX 制品相对路径。</param>
    /// <param name="artifactPath">证据制品相对路径。</param>
    /// <param name="startedAtUtc">测试开始时间。</param>
    /// <param name="completedAtUtc">测试完成时间。</param>
    /// <param name="artifactKind">证据制品类别。</param>
    /// <param name="sourceIdentity">源码身份标识。</param>
    /// <param name="allowReleaseEvidence">是否允许创建发布级证据。</param>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="framework">目标框架。</param>
    /// <param name="runId">CI 运行标识。</param>
    /// <param name="binaryManifestPath">二进制 manifest 相对路径。</param>
    /// <param name="runtimeVersion">运行时版本。</param>
    /// <param name="operatingSystem">操作系统名称。</param>
    /// <param name="sourceState">源码工作区状态。</param>
    /// <param name="evidenceSessionId">证据聚合会话标识。</param>
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

    /// <summary>
    /// 根据已验证运行创建发布级证据元数据。
    /// </summary>
    /// <param name="run">已验证的 Provider 运行。</param>
    /// <param name="testMethod">测试方法名称。</param>
    /// <returns>发布级证据元数据。</returns>
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

    /// <summary>
    /// 获取 Provider 版本。
    /// </summary>
    public string ProviderVersion { get; }

    /// <summary>
    /// 获取数据库版本。
    /// </summary>
    public string DatabaseVersion { get; }

    /// <summary>
    /// 获取驱动版本。
    /// </summary>
    public string DriverVersion { get; }

    /// <summary>
    /// 获取集成测试连接类别。
    /// </summary>
    public ProviderIntegrationConnectionKind ConnectionKind { get; }

    /// <summary>
    /// 获取测试方法名称。
    /// </summary>
    public string TestMethod { get; }

    /// <summary>
    /// 获取 TRX 制品相对路径。
    /// </summary>
    public string TrxPath { get; }

    /// <summary>
    /// 获取证据制品相对路径。
    /// </summary>
    public string ArtifactPath { get; }

    /// <summary>
    /// 获取测试开始时间。
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>
    /// 获取测试完成时间。
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; }

    /// <summary>
    /// 获取证据制品类别。
    /// </summary>
    public ProviderCapabilityArtifactKind ArtifactKind { get; }

    /// <summary>
    /// 获取源码身份标识。
    /// </summary>
    public string SourceIdentity { get; }

    /// <summary>
    /// 获取 Provider 名称。
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// 获取目标框架。
    /// </summary>
    public string Framework { get; }

    /// <summary>
    /// 获取受信 CI 运行标识。
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// 获取二进制 manifest 相对路径。
    /// </summary>
    public string BinaryManifestPath { get; }

    /// <summary>
    /// 获取证据聚合会话标识。
    /// </summary>
    public string EvidenceSessionId { get; }

    /// <summary>
    /// 获取运行时版本。
    /// </summary>
    public string RuntimeVersion { get; }

    /// <summary>
    /// 获取操作系统名称。
    /// </summary>
    public string OperatingSystem { get; }

    /// <summary>
    /// 获取源码工作区状态。
    /// </summary>
    public string SourceState { get; }

    /// <summary>
    /// 获取证据是否为受信发布证据。
    /// </summary>
    internal bool IsTrustedReleaseEvidence { get; }

    /// <summary>
    /// 验证必填文本值并去除首尾空白。
    /// </summary>
    /// <param name="value">待验证的文本值。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的文本值。</returns>
    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value.Trim();

    /// <summary>
    /// 验证证据会话标识格式。
    /// </summary>
    /// <param name="value">证据会话标识。</param>
    /// <returns>规范化后的证据会话标识。</returns>
    private static string RequireEvidenceSessionId(string value)
    {
        var sessionId = Require(value, nameof(value));
        if (!System.Text.RegularExpressions.Regex.IsMatch(sessionId,
                "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$"))
            throw new ArgumentException("证据会话标识格式无效。", nameof(value));
        return sessionId;
    }

    /// <summary>
    /// 验证证据相对路径不包含穿越或连接信息。
    /// </summary>
    /// <param name="value">待验证的相对路径。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的相对路径。</returns>
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
    /// <summary>
    /// 初始化一个 <see cref="ProviderCapabilityEvidence"/> 类型的实例。
    /// </summary>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="capability">能力名称。</param>
    /// <param name="scenario">场景名称。</param>
    /// <param name="state">能力证据状态。</param>
    /// <param name="evidence">证据说明。</param>
    /// <param name="artifact">证据制品路径或标识。</param>
    /// <param name="integrationEvidence">真实集成证据元数据。</param>
    public ProviderCapabilityEvidence(string provider, string capability, string scenario,
        ProviderCapabilityEvidenceState state, string evidence = null, string artifact = null,
        ProviderIntegrationEvidenceMetadata integrationEvidence = null)
        : this(provider, capability, scenario, state, evidence, artifact, integrationEvidence, false)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ProviderCapabilityEvidence"/> 类型的实例。
    /// </summary>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="capability">能力名称。</param>
    /// <param name="scenario">场景名称。</param>
    /// <param name="state">能力证据状态。</param>
    /// <param name="evidence">证据说明。</param>
    /// <param name="artifact">证据制品路径或标识。</param>
    /// <param name="integrationEvidence">真实集成证据元数据。</param>
    /// <param name="allowRealIntegration">是否允许真实集成状态。</param>
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

    /// <summary>
    /// 创建真实集成状态的能力证据。
    /// </summary>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="capability">能力名称。</param>
    /// <param name="scenario">场景名称。</param>
    /// <param name="evidence">证据说明。</param>
    /// <param name="artifact">证据制品路径或标识。</param>
    /// <param name="integrationEvidence">真实集成证据元数据。</param>
    /// <returns>真实集成能力证据。</returns>
    internal static ProviderCapabilityEvidence CreateRealIntegration(string provider, string capability,
        string scenario, string evidence, string artifact,
        ProviderIntegrationEvidenceMetadata integrationEvidence) =>
        new(provider, capability, scenario, ProviderCapabilityEvidenceState.RealIntegrationProven,
            evidence, artifact, integrationEvidence, true);

    /// <summary>
    /// 获取 Provider 名称。
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// 获取能力名称。
    /// </summary>
    public string Capability { get; }

    /// <summary>
    /// 获取场景名称。
    /// </summary>
    public string Scenario { get; }

    /// <summary>
    /// 获取能力证据状态。
    /// </summary>
    public ProviderCapabilityEvidenceState State { get; }

    /// <summary>
    /// 获取证据说明。
    /// </summary>
    public string Evidence { get; }

    /// <summary>
    /// 获取证据制品路径或标识。
    /// </summary>
    public string Artifact { get; }

    /// <summary>
    /// 获取真实集成证据元数据。
    /// </summary>
    public ProviderIntegrationEvidenceMetadata IntegrationEvidence { get; }

    /// <summary>
    /// 验证必填文本值并去除首尾空白。
    /// </summary>
    /// <param name="value">待验证的文本值。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的文本值。</returns>
    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value.Trim();
}

/// <summary>
/// Provider 能力矩阵及其无密 Markdown 输出。
/// </summary>
public sealed class ProviderCapabilityMatrix
{
    /// <summary>
    /// 能力证据条目集合。
    /// </summary>
    private readonly List<ProviderCapabilityEvidence> _entries = new();

    /// <summary>
    /// 获取能力证据条目。
    /// </summary>
    public IReadOnlyList<ProviderCapabilityEvidence> Entries => _entries;

    /// <summary>
    /// 获取发布就绪状态。
    /// </summary>
    public bool IsReleaseReady => false;

    /// <summary>
    /// 获取 Provider 运行就绪状态。
    /// </summary>
    public bool IsProviderRunReady => _entries.Count > 0 && _entries.All(IsReleaseReadyEvidence);

    /// <summary>
    /// 添加能力证据条目。
    /// </summary>
    /// <param name="evidence">待添加的能力证据。</param>
    /// <returns>当前能力矩阵。</returns>
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

    /// <summary>
    /// 生成无密 Markdown 能力矩阵。
    /// </summary>
    /// <returns>Markdown 文本。</returns>
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

    /// <summary>
    /// 生成无密 JSON 能力矩阵。
    /// </summary>
    /// <returns>JSON 文本。</returns>
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

    /// <summary>
    /// 使用 UTF-8 写入无密 JSON 能力矩阵。
    /// </summary>
    /// <param name="path">目标制品路径。</param>
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

    /// <summary>
    /// 转义 Markdown 表格中的特殊字符。
    /// </summary>
    /// <param name="value">待转义的文本。</param>
    /// <returns>转义后的文本。</returns>
    private static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    /// <summary>
    /// 判断能力证据是否满足 Provider 运行就绪条件。
    /// </summary>
    /// <param name="evidence">待判断的能力证据。</param>
    /// <returns>满足条件时返回 true。</returns>
    private static bool IsReleaseReadyEvidence(ProviderCapabilityEvidence evidence) =>
        evidence.State == ProviderCapabilityEvidenceState.Unsupported ||
        evidence.State == ProviderCapabilityEvidenceState.RealIntegrationProven &&
        evidence.IntegrationEvidence?.ArtifactKind == ProviderCapabilityArtifactKind.ReleaseEvidence &&
        evidence.IntegrationEvidence.IsTrustedReleaseEvidence;
}

/// <summary>
/// 单个 Provider 和目标框架的发布就绪运行结果。
/// </summary>
public sealed class ProviderReleaseReadinessRun
{
    /// <summary>
    /// 获取或初始化 Provider 名称。
    /// </summary>
    public string Provider { get; init; }

    /// <summary>
    /// 获取或初始化目标框架。
    /// </summary>
    public string Framework { get; init; }

    /// <summary>
    /// 获取或初始化发布证据有效标志。
    /// </summary>
    public bool ReleaseEvidenceValid { get; init; }

    /// <summary>
    /// 获取或初始化已执行测试数量。
    /// </summary>
    public int Executed { get; init; }

    /// <summary>
    /// 获取或初始化失败测试数量。
    /// </summary>
    public int Failed { get; init; }

    /// <summary>
    /// 获取或初始化核心测试跳过数量。
    /// </summary>
    public int CoreSkipped { get; init; }

    /// <summary>
    /// 获取或初始化证据聚合会话标识。
    /// </summary>
    public string EvidenceSessionId { get; init; }

    /// <summary>
    /// 获取或初始化源码身份标识。
    /// </summary>
    public string SourceIdentity { get; init; }

    /// <summary>
    /// 获取或初始化已通过测试方法集合。
    /// </summary>
    public IReadOnlySet<string> PassedTestMethods { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>
/// 提供 Provider 发布就绪状态检查。
/// </summary>
public static class ProviderReleaseReadiness
{
    /// <summary>
    /// 发布就绪检查要求的核心 Provider 名称。
    /// </summary>
    private static readonly string[] CoreProviders = { "MySql", "PostgreSql", "SqlServer", "SQLite" };
    /// <summary>
    /// 发布就绪检查要求的目标框架。
    /// </summary>
    private static readonly string[] RequiredFrameworks = { "net6.0", "net8.0" };

    /// <summary>
    /// 判断核心 Provider 是否满足发布就绪条件。
    /// </summary>
    /// <param name="runs">各 Provider 和目标框架的运行结果。</param>
    /// <param name="unitTestsPassed">单元测试是否通过。</param>
    /// <param name="formalHostComplete">正式测试宿主是否完成。</param>
    /// <param name="rs0026GatePassed">RS0026 门禁是否通过。</param>
    /// <param name="apiGatePassed">API 门禁是否通过。</param>
    /// <returns>满足全部条件时返回 true。</returns>
    public static bool IsReady(IReadOnlyCollection<ProviderReleaseReadinessRun> runs, bool unitTestsPassed,
        bool formalHostComplete, bool rs0026GatePassed, bool apiGatePassed)
        => IsReady(runs, unitTestsPassed, formalHostComplete, rs0026GatePassed, apiGatePassed,
            ProviderCapabilityCatalog.GetDefinitions());

    /// <summary>
    /// 判断核心 Provider 是否满足发布就绪条件。
    /// </summary>
    /// <param name="runs">各 Provider 和目标框架的运行结果。</param>
    /// <param name="unitTestsPassed">单元测试是否通过。</param>
    /// <param name="formalHostComplete">正式测试宿主是否完成。</param>
    /// <param name="rs0026GatePassed">RS0026 门禁是否通过。</param>
    /// <param name="apiGatePassed">API 门禁是否通过。</param>
    /// <param name="definitions">能力场景定义集合。</param>
    /// <returns>满足全部条件时返回 true。</returns>
    /// <remarks>使用传入的能力场景目录执行就绪校验。</remarks>
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

    /// <summary>
    /// 获取缺失或重复的核心 Provider 和目标框架运行项。
    /// </summary>
    /// <param name="runs">各 Provider 和目标框架的运行结果。</param>
    /// <returns>缺失或重复运行项标识。</returns>
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
