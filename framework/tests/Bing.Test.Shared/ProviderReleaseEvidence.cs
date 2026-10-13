using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Bing.Test.Shared;

/// <summary>
/// 受保护 Provider 运行生成发布证据时的输入。
/// </summary>
public sealed class ProviderReleaseEvidenceRequest
{
    /// <summary>
    /// 获取或设置工作区根目录。
    /// </summary>
    public string WorkspaceRoot { get; set; }

    /// <summary>
    /// 获取或设置 Provider 名称。
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// 获取或设置目标框架。
    /// </summary>
    public string Framework { get; set; }

    /// <summary>
    /// 获取或设置当前运行标识。
    /// </summary>
    public string RunId { get; set; }

    /// <summary>
    /// 获取或设置 TRX 制品相对路径。
    /// </summary>
    public string TrxRelativePath { get; set; }

    /// <summary>
    /// 获取或设置摘要制品相对路径。
    /// </summary>
    public string SummaryRelativePath { get; set; }

    /// <summary>
    /// 获取或设置 JSON 矩阵制品相对路径。
    /// </summary>
    public string MatrixJsonRelativePath { get; set; }

    /// <summary>
    /// 获取或设置 Markdown 矩阵制品相对路径。
    /// </summary>
    public string MatrixMarkdownRelativePath { get; set; }

    /// <summary>
    /// 获取或设置二进制 manifest 相对路径。
    /// </summary>
    public string BinaryManifestRelativePath { get; set; }

    /// <summary>
    /// 获取或设置证据聚合会话标识。
    /// </summary>
    public string EvidenceSessionId { get; set; }

    /// <summary>
    /// 获取或设置源码身份标识。
    /// </summary>
    public string SourceIdentity { get; set; }

    /// <summary>
    /// 获取或设置 Provider 版本。
    /// </summary>
    public string ProviderVersion { get; set; }

    /// <summary>
    /// 获取或设置数据库版本。
    /// </summary>
    public string DatabaseVersion { get; set; }

    /// <summary>
    /// 获取或设置驱动版本。
    /// </summary>
    public string DriverVersion { get; set; }

    /// <summary>
    /// 获取或设置运行时版本。
    /// </summary>
    public string RuntimeVersion { get; set; }

    /// <summary>
    /// 获取或设置操作系统名称。
    /// </summary>
    public string OperatingSystem { get; set; }

    /// <summary>
    /// 获取或设置源码工作区状态。
    /// </summary>
    public string SourceState { get; set; }

    /// <summary>
    /// 获取或设置运行开始时间。
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>
    /// 获取或设置运行完成时间。
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; set; }
}

/// <summary>
/// 受保护 Provider CI 的发布证据验证入口。
/// </summary>
public static class ProviderReleaseEvidenceValidator
{
    /// <summary>
    /// 允许制品时间戳偏离运行窗口的最大时长。
    /// </summary>
    private static readonly TimeSpan FileTimestampTolerance = TimeSpan.FromMinutes(2);
    /// <summary>
    /// 当前运行完成后的最大有效时长。
    /// </summary>
    private static readonly TimeSpan MaximumCompletedRunAge = TimeSpan.FromMinutes(15);
    /// <summary>
    /// 已验证运行令牌及其校验标记。
    /// </summary>
    private static readonly ConditionalWeakTable<ProviderValidatedReleaseRun, object> ValidatedRuns = new();
    /// <summary>
    /// 当前验证器使用的运行令牌标记对象。
    /// </summary>
    private static readonly object ValidationMarker = new();

    /// <summary>
    /// 验证当前运行的 TRX、摘要和制品路径，并返回可创建发布证据的受信运行对象。
    /// </summary>
    /// <param name="request">由 CI runner 传入的当前运行绑定信息。</param>
    /// <returns>可创建发布证据的受信运行对象。</returns>
    public static ProviderValidatedReleaseRun Validate(ProviderReleaseEvidenceRequest request) =>
        ValidateCore(request, true, null);

    /// <summary>
    /// 验证已绑定证据会话的归档 Provider 运行，不重复执行当前运行时效门禁。
    /// </summary>
    /// <param name="request">归档运行的制品绑定信息。</param>
    /// <param name="expectedEvidenceSessionId">要求匹配的证据聚合会话标识。</param>
    /// <returns>可创建发布证据的受信运行对象。</returns>
    public static ProviderValidatedReleaseRun ValidateArchived(ProviderReleaseEvidenceRequest request,
        string expectedEvidenceSessionId) => ValidateCore(request, false,
        RequireEvidenceSessionId(expectedEvidenceSessionId));

    /// <summary>
    /// 执行当前或归档运行的共同证据校验流程。
    /// </summary>
    /// <param name="request">运行制品绑定信息。</param>
    /// <param name="requireFreshness">是否校验当前运行时效。</param>
    /// <param name="expectedEvidenceSessionId">要求匹配的证据会话标识。</param>
    /// <returns>已验证的 Provider 运行对象。</returns>
    private static ProviderValidatedReleaseRun ValidateCore(ProviderReleaseEvidenceRequest request,
        bool requireFreshness, string expectedEvidenceSessionId)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var workspaceRoot = RequireFullPath(request.WorkspaceRoot, nameof(request.WorkspaceRoot));
        var provider = RequireToken(request.Provider, nameof(request.Provider));
        var framework = RequireFramework(request.Framework);
        var runId = RequireRunId(request.RunId);
        var evidenceSessionId = RequireEvidenceSessionId(request.EvidenceSessionId);
        if (expectedEvidenceSessionId != null &&
            !string.Equals(evidenceSessionId, RequireEvidenceSessionId(expectedEvidenceSessionId),
                StringComparison.Ordinal))
            throw new ArgumentException("发布证据不属于当前聚合会话。", nameof(request.EvidenceSessionId));
        var sourceIdentity = RequireSafeText(request.SourceIdentity, nameof(request.SourceIdentity));
        var providerVersion = RequireReleaseMetadata(request.ProviderVersion, nameof(request.ProviderVersion));
        var databaseVersion = RequireReleaseMetadata(request.DatabaseVersion, nameof(request.DatabaseVersion));
        var driverVersion = RequireReleaseMetadata(request.DriverVersion, nameof(request.DriverVersion));
        var runtimeVersion = RequireReleaseMetadata(request.RuntimeVersion, nameof(request.RuntimeVersion));
        var operatingSystem = RequireReleaseMetadata(request.OperatingSystem, nameof(request.OperatingSystem));
        var sourceState = RequireReleaseMetadata(request.SourceState, nameof(request.SourceState));
        if (!string.Equals(sourceState, "clean", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("发布级证据必须来自 clean source。", nameof(request.SourceState));
        EnsureUtcWindow(request.StartedAtUtc, request.CompletedAtUtc, requireFreshness);

        var runRelativeDirectory = $"artifacts/provider-test-results/{runId}";
        var runDirectory = CombineRelativePath(workspaceRoot, runRelativeDirectory, nameof(request.RunId));
        if (!Directory.Exists(runDirectory))
            throw new ArgumentException("当前运行目录不存在。", nameof(request.RunId));

        var trxRelativePath = RequirePathInRunDirectory(request.TrxRelativePath, runRelativeDirectory,
            ".trx", nameof(request.TrxRelativePath));
        var summaryRelativePath = RequirePathInRunDirectory(request.SummaryRelativePath, runRelativeDirectory,
            ".json", nameof(request.SummaryRelativePath));
        var matrixJsonRelativePath = RequireExpectedMatrixPath(request.MatrixJsonRelativePath, runRelativeDirectory,
            "provider-capability-matrix.json", nameof(request.MatrixJsonRelativePath));
        var matrixMarkdownRelativePath = RequireExpectedMatrixPath(request.MatrixMarkdownRelativePath,
            runRelativeDirectory, "provider-capability-matrix.md", nameof(request.MatrixMarkdownRelativePath));
        var binaryManifestRelativePath = RequireExpectedMatrixPath(request.BinaryManifestRelativePath,
            runRelativeDirectory, "provider-binary-manifest.json", nameof(request.BinaryManifestRelativePath));
        var trxPath = CombineRelativePath(workspaceRoot, trxRelativePath, nameof(request.TrxRelativePath));
        var summaryPath = CombineRelativePath(workspaceRoot, summaryRelativePath, nameof(request.SummaryRelativePath));
        var matrixJsonPath = CombineRelativePath(workspaceRoot, matrixJsonRelativePath,
            nameof(request.MatrixJsonRelativePath));
        var matrixMarkdownPath = CombineRelativePath(workspaceRoot, matrixMarkdownRelativePath,
            nameof(request.MatrixMarkdownRelativePath));
        var binaryManifestPath = CombineRelativePath(workspaceRoot, binaryManifestRelativePath,
            nameof(request.BinaryManifestRelativePath));
        EnsureFileExists(trxPath, nameof(request.TrxRelativePath));
        EnsureFileExists(summaryPath, nameof(request.SummaryRelativePath));
        EnsureFileExists(binaryManifestPath, nameof(request.BinaryManifestRelativePath));
        if (requireFreshness)
        {
            EnsureCurrentRunFile(trxPath, request.StartedAtUtc, request.CompletedAtUtc,
                nameof(request.TrxRelativePath));
            EnsureCurrentRunFile(summaryPath, request.StartedAtUtc, request.CompletedAtUtc,
                nameof(request.SummaryRelativePath));
            EnsureCurrentRunFile(binaryManifestPath, request.StartedAtUtc, request.CompletedAtUtc,
                nameof(request.BinaryManifestRelativePath));
        }
        if (!requireFreshness)
        {
            EnsureFileExists(matrixJsonPath, nameof(request.MatrixJsonRelativePath));
            EnsureFileExists(matrixMarkdownPath, nameof(request.MatrixMarkdownRelativePath));
            ValidateArchivedMatrix(matrixJsonPath, provider, framework, runId, evidenceSessionId, sourceIdentity,
                matrixJsonRelativePath, trxRelativePath, binaryManifestRelativePath);
        }

        ValidateBinaryManifest(binaryManifestPath, workspaceRoot, runRelativeDirectory, provider, framework, runId,
            evidenceSessionId, sourceIdentity, sourceState, request.BinaryManifestRelativePath);

        var summaryText = File.ReadAllText(summaryPath, Encoding.UTF8);
        EnsureNoSensitiveText(summaryText, nameof(request.SummaryRelativePath));
        using var summary = JsonDocument.Parse(summaryText);
        var root = summary.RootElement;
        EnsureSummaryIdentity(root, provider, framework, runId, evidenceSessionId, sourceIdentity, trxRelativePath,
            summaryRelativePath, request.StartedAtUtc, request.CompletedAtUtc, providerVersion,
            databaseVersion, driverVersion, runtimeVersion, operatingSystem, sourceState,
            binaryManifestRelativePath);

        var trx = XDocument.Load(trxPath, LoadOptions.None);
        var counters = trx.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "ResultSummary")
            ?.Elements().FirstOrDefault(element => element.Name.LocalName == "Counters");
        if (counters == null)
            throw new ArgumentException("TRX 缺少结果计数器。", nameof(request.TrxRelativePath));
        var total = ParseCounter(counters, "total", request.TrxRelativePath);
        var passed = ParseCounter(counters, "passed", request.TrxRelativePath);
        var failed = ParseCounter(counters, "failed", request.TrxRelativePath);
        var results = trx.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "Results")
            ?.Elements().Where(element => element.Name.LocalName == "UnitTestResult").ToArray() ?? Array.Empty<XElement>();
        var skipped = results.Count(result => string.Equals((string)result.Attribute("outcome"), "NotExecuted",
            StringComparison.OrdinalIgnoreCase));
        var passedTestMethods = results.Where(result => string.Equals((string)result.Attribute("outcome"), "Passed",
                StringComparison.OrdinalIgnoreCase))
            .Select(result => (string)result.Attribute("testName"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        var optionalSkipped = GetSummaryInt(root, "OptionalSkipped", request.SummaryRelativePath);
        var coreSkipped = GetSummaryInt(root, "CoreSkipped", request.SummaryRelativePath);
        if (total <= 0 || passed <= 0 || failed != 0 || coreSkipped != 0 || skipped != optionalSkipped ||
            passed + optionalSkipped != total || passedTestMethods.Count == 0)
            throw new ArgumentException("TRX 必须为当前 Provider 的零失败、零核心跳过成功运行。",
                nameof(request.TrxRelativePath));
        EnsureSummaryCounts(root, total, passed, failed, skipped, request.SummaryRelativePath);

        var run = new ProviderValidatedReleaseRun(provider, framework, runId, evidenceSessionId, sourceIdentity,
            request.StartedAtUtc,
            request.CompletedAtUtc, trxRelativePath, summaryRelativePath, matrixJsonRelativePath,
            matrixMarkdownRelativePath, matrixJsonPath, matrixMarkdownPath, binaryManifestRelativePath,
            passedTestMethods, providerVersion,
            databaseVersion, driverVersion, runtimeVersion, operatingSystem, sourceState);
        ValidatedRuns.Add(run, ValidationMarker);
        return run;
    }

    /// <summary>
    /// 确认运行令牌由当前验证器创建。
    /// </summary>
    /// <param name="run">待确认的运行令牌。</param>
    internal static void EnsureValidated(ProviderValidatedReleaseRun run)
    {
        if (run == null || !ValidatedRuns.TryGetValue(run, out var marker) ||
            !ReferenceEquals(marker, ValidationMarker))
            throw new InvalidOperationException("发布级证据必须使用当前验证器产生的运行令牌。");
    }

    /// <summary>
    /// 已由受保护 CI 制品验证的 Provider 运行令牌。
    /// </summary>
    public sealed class ProviderValidatedReleaseRun
    {
        /// <summary>
        /// 初始化一个 <see cref="ProviderValidatedReleaseRun"/> 类型的实例。
        /// </summary>
        /// <param name="provider">Provider 名称。</param>
        /// <param name="framework">目标框架。</param>
        /// <param name="runId">运行标识。</param>
        /// <param name="evidenceSessionId">证据聚合会话标识。</param>
        /// <param name="sourceIdentity">源码身份标识。</param>
        /// <param name="startedAtUtc">运行开始时间。</param>
        /// <param name="completedAtUtc">运行完成时间。</param>
        /// <param name="trxRelativePath">TRX 制品相对路径。</param>
        /// <param name="summaryRelativePath">摘要制品相对路径。</param>
        /// <param name="matrixJsonRelativePath">JSON 矩阵相对路径。</param>
        /// <param name="matrixMarkdownRelativePath">Markdown 矩阵相对路径。</param>
        /// <param name="matrixJsonPath">JSON 矩阵绝对路径。</param>
        /// <param name="matrixMarkdownPath">Markdown 矩阵绝对路径。</param>
        /// <param name="binaryManifestRelativePath">二进制 manifest 相对路径。</param>
        /// <param name="passedTestMethods">已通过测试方法集合。</param>
        /// <param name="providerVersion">Provider 版本。</param>
        /// <param name="databaseVersion">数据库版本。</param>
        /// <param name="driverVersion">驱动版本。</param>
        /// <param name="runtimeVersion">运行时版本。</param>
        /// <param name="operatingSystem">操作系统名称。</param>
        /// <param name="sourceState">源码工作区状态。</param>
        internal ProviderValidatedReleaseRun(string provider, string framework, string runId, string evidenceSessionId,
            string sourceIdentity,
            DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, string trxRelativePath,
            string summaryRelativePath, string matrixJsonRelativePath, string matrixMarkdownRelativePath,
            string matrixJsonPath, string matrixMarkdownPath, string binaryManifestRelativePath,
            IReadOnlySet<string> passedTestMethods,
            string providerVersion = "not-recorded", string databaseVersion = "not-recorded",
            string driverVersion = "not-recorded", string runtimeVersion = "not-recorded",
            string operatingSystem = "not-recorded", string sourceState = "unknown")
        {
            Provider = provider;
            Framework = framework;
            RunId = runId;
            EvidenceSessionId = evidenceSessionId;
            SourceIdentity = sourceIdentity;
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
            TrxRelativePath = trxRelativePath;
            SummaryRelativePath = summaryRelativePath;
            MatrixJsonRelativePath = matrixJsonRelativePath;
            MatrixMarkdownRelativePath = matrixMarkdownRelativePath;
            MatrixJsonPath = matrixJsonPath;
            MatrixMarkdownPath = matrixMarkdownPath;
            BinaryManifestRelativePath = binaryManifestRelativePath;
            PassedTestMethods = passedTestMethods;
            ProviderVersion = providerVersion;
            DatabaseVersion = databaseVersion;
            DriverVersion = driverVersion;
            RuntimeVersion = runtimeVersion;
            OperatingSystem = operatingSystem;
            SourceState = sourceState;
        }

        /// <summary>
        /// 初始化一个 <see cref="ProviderValidatedReleaseRun"/> 类型的实例。
        /// </summary>
        /// <param name="provider">Provider 名称。</param>
        /// <param name="framework">目标框架。</param>
        /// <param name="runId">运行标识。</param>
        /// <param name="sourceIdentity">源码身份标识。</param>
        /// <param name="startedAtUtc">运行开始时间。</param>
        /// <param name="completedAtUtc">运行完成时间。</param>
        /// <param name="trxRelativePath">TRX 制品相对路径。</param>
        /// <param name="summaryRelativePath">摘要制品相对路径。</param>
        /// <param name="matrixJsonRelativePath">JSON 矩阵相对路径。</param>
        /// <param name="matrixMarkdownRelativePath">Markdown 矩阵相对路径。</param>
        /// <param name="matrixJsonPath">JSON 矩阵绝对路径。</param>
        /// <param name="matrixMarkdownPath">Markdown 矩阵绝对路径。</param>
        /// <param name="passedTestMethods">已通过测试方法集合。</param>
        internal ProviderValidatedReleaseRun(string provider, string framework, string runId, string sourceIdentity,
            DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, string trxRelativePath,
            string summaryRelativePath, string matrixJsonRelativePath, string matrixMarkdownRelativePath,
            string matrixJsonPath, string matrixMarkdownPath, IReadOnlySet<string> passedTestMethods)
            : this(provider, framework, runId, "not-recorded", sourceIdentity, startedAtUtc, completedAtUtc, trxRelativePath,
                summaryRelativePath, matrixJsonRelativePath, matrixMarkdownRelativePath, matrixJsonPath,
                matrixMarkdownPath, string.Empty, passedTestMethods)
        {
        }

        /// <summary>
        /// 获取 Provider 名称。
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// 获取目标框架。
        /// </summary>
        public string Framework { get; }

        /// <summary>
        /// 获取运行标识。
        /// </summary>
        public string RunId { get; }

        /// <summary>
        /// 获取证据聚合会话标识。
        /// </summary>
        public string EvidenceSessionId { get; }

        /// <summary>
        /// 获取源码身份标识。
        /// </summary>
        public string SourceIdentity { get; }

        /// <summary>
        /// 获取运行开始时间。
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; }

        /// <summary>
        /// 获取运行完成时间。
        /// </summary>
        public DateTimeOffset CompletedAtUtc { get; }

        /// <summary>
        /// 获取 TRX 制品相对路径。
        /// </summary>
        public string TrxRelativePath { get; }

        /// <summary>
        /// 获取摘要制品相对路径。
        /// </summary>
        public string SummaryRelativePath { get; }

        /// <summary>
        /// 获取 JSON 矩阵相对路径。
        /// </summary>
        public string MatrixJsonRelativePath { get; }

        /// <summary>
        /// 获取 Markdown 矩阵相对路径。
        /// </summary>
        public string MatrixMarkdownRelativePath { get; }

        /// <summary>
        /// 获取 JSON 矩阵绝对路径。
        /// </summary>
        public string MatrixJsonPath { get; }

        /// <summary>
        /// 获取 Markdown 矩阵绝对路径。
        /// </summary>
        public string MatrixMarkdownPath { get; }

        /// <summary>
        /// 获取二进制 manifest 相对路径。
        /// </summary>
        public string BinaryManifestRelativePath { get; }

        /// <summary>
        /// 获取已通过测试方法集合。
        /// </summary>
        public IReadOnlySet<string> PassedTestMethods { get; }

        /// <summary>
        /// 获取 Provider 版本。
        /// </summary>
        internal string ProviderVersion { get; }

        /// <summary>
        /// 获取数据库版本。
        /// </summary>
        internal string DatabaseVersion { get; }

        /// <summary>
        /// 获取驱动版本。
        /// </summary>
        internal string DriverVersion { get; }

        /// <summary>
        /// 获取运行时版本。
        /// </summary>
        internal string RuntimeVersion { get; }

        /// <summary>
        /// 获取操作系统名称。
        /// </summary>
        internal string OperatingSystem { get; }

        /// <summary>
        /// 获取源码工作区状态。
        /// </summary>
        internal string SourceState { get; }

        /// <summary>
        /// 获取集成测试连接类别。
        /// </summary>
        internal ProviderIntegrationConnectionKind ConnectionKind => ProviderIntegrationConnectionKind.RemoteTestDatabase;
    }

    /// <summary>
    /// 校验摘要中的运行身份和制品元数据。
    /// </summary>
    /// <param name="summary">运行摘要 JSON。</param>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="framework">目标框架。</param>
    /// <param name="runId">运行标识。</param>
    /// <param name="evidenceSessionId">证据聚合会话标识。</param>
    /// <param name="sourceIdentity">源码身份标识。</param>
    /// <param name="trxRelativePath">TRX 制品相对路径。</param>
    /// <param name="summaryRelativePath">摘要制品相对路径。</param>
    /// <param name="startedAtUtc">运行开始时间。</param>
    /// <param name="completedAtUtc">运行完成时间。</param>
    /// <param name="providerVersion">Provider 版本。</param>
    /// <param name="databaseVersion">数据库版本。</param>
    /// <param name="driverVersion">驱动版本。</param>
    /// <param name="runtimeVersion">运行时版本。</param>
    /// <param name="operatingSystem">操作系统名称。</param>
    /// <param name="sourceState">源码工作区状态。</param>
    /// <param name="binaryManifestRelativePath">二进制 manifest 相对路径。</param>
    private static void EnsureSummaryIdentity(JsonElement summary, string provider, string framework, string runId,
        string evidenceSessionId, string sourceIdentity, string trxRelativePath, string summaryRelativePath,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc, string providerVersion, string databaseVersion, string driverVersion,
        string runtimeVersion, string operatingSystem, string sourceState, string binaryManifestRelativePath)
    {
        EnsureSummaryValue(summary, "Provider", provider);
        EnsureSummaryValue(summary, "Framework", framework);
        EnsureSummaryValue(summary, "RunId", runId);
        EnsureSummaryValue(summary, "EvidenceSessionId", evidenceSessionId);
        EnsureSummaryValue(summary, "SourceIdentity", sourceIdentity);
        EnsureSummaryValue(summary, "TrxPath", trxRelativePath);
        EnsureSummaryValue(summary, "ArtifactPath", summaryRelativePath);
        EnsureSummaryValue(summary, "BinaryManifestPath", binaryManifestRelativePath);
        EnsureSummaryTimestamp(summary, "StartedAtUtc", startedAtUtc);
        EnsureSummaryTimestamp(summary, "CompletedAtUtc", completedAtUtc);
        EnsureSummaryValue(summary, "ProviderVersion", providerVersion);
        EnsureSummaryValue(summary, "DatabaseVersion", databaseVersion);
        EnsureSummaryValue(summary, "DriverVersion", driverVersion);
        EnsureSummaryValue(summary, "RuntimeVersion", runtimeVersion);
        EnsureSummaryValue(summary, "OperatingSystem", operatingSystem);
        EnsureSummaryValue(summary, "SourceState", sourceState);
    }

    /// <summary>
    /// 校验摘要中的测试计数与 TRX 计数一致。
    /// </summary>
    /// <param name="summary">运行摘要 JSON。</param>
    /// <param name="total">TRX 总测试数。</param>
    /// <param name="passed">TRX 通过测试数。</param>
    /// <param name="failed">TRX 失败测试数。</param>
    /// <param name="skipped">TRX 跳过测试数。</param>
    /// <param name="parameterName">错误参数名称。</param>
    private static void EnsureSummaryCounts(JsonElement summary, int total, int passed, int failed, int skipped,
        string parameterName)
    {
        var optionalSkipped = GetSummaryInt(summary, "OptionalSkipped", parameterName);
        var coreSkipped = GetSummaryInt(summary, "CoreSkipped", parameterName);
        if (GetSummaryInt(summary, "Discovered", parameterName) != total ||
            GetSummaryInt(summary, "Passed", parameterName) != passed ||
            GetSummaryInt(summary, "Failed", parameterName) != failed ||
            GetSummaryInt(summary, "Skipped", parameterName) != skipped ||
            coreSkipped != 0 || optionalSkipped != skipped ||
            GetSummaryInt(summary, "Executed", parameterName) != passed + failed)
            throw new ArgumentException("摘要计数与 TRX 或发布证据要求不一致。", parameterName);
    }

    /// <summary>
    /// 校验摘要字段的字符串值。
    /// </summary>
    /// <param name="summary">摘要 JSON。</param>
    /// <param name="name">字段名称。</param>
    /// <param name="expected">期望值。</param>
    private static void EnsureSummaryValue(JsonElement summary, string name, string expected)
    {
        if (!summary.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
            throw new ArgumentException($"摘要字段 {name} 与当前运行不一致。", nameof(summary));
    }

    /// <summary>
    /// 校验摘要字段的 UTC 时间值。
    /// </summary>
    /// <param name="summary">摘要 JSON。</param>
    /// <param name="name">字段名称。</param>
    /// <param name="expected">期望时间。</param>
    private static void EnsureSummaryTimestamp(JsonElement summary, string name, DateTimeOffset expected)
    {
        if (!summary.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(value.GetString(), out var actual) || actual.Offset != TimeSpan.Zero ||
            actual != expected)
            throw new ArgumentException($"摘要字段 {name} 与当前运行时间不一致。", nameof(summary));
    }

    /// <summary>
    /// 读取并校验摘要中的非负整数。
    /// </summary>
    /// <param name="summary">摘要 JSON。</param>
    /// <param name="name">字段名称。</param>
    /// <param name="parameterName">错误参数名称。</param>
    /// <returns>摘要中的整数值。</returns>
    private static int GetSummaryInt(JsonElement summary, string name, string parameterName)
    {
        if (!summary.TryGetProperty(name, out var value) || !value.TryGetInt32(out var result) || result < 0)
            throw new ArgumentException($"摘要字段 {name} 无效。", parameterName);
        return result;
    }

    /// <summary>
    /// 读取并校验 TRX 计数器。
    /// </summary>
    /// <param name="counters">TRX 计数器元素。</param>
    /// <param name="name">计数器名称。</param>
    /// <param name="parameterName">错误参数名称。</param>
    /// <returns>计数器数值。</returns>
    private static int ParseCounter(XElement counters, string name, string parameterName)
    {
        if (!int.TryParse((string)counters.Attribute(name), out var value) || value < 0)
            throw new ArgumentException($"TRX 计数器 {name} 无效。", parameterName);
        return value;
    }

    /// <summary>
    /// 校验制品文件时间戳位于当前运行窗口内。
    /// </summary>
    /// <param name="path">制品文件路径。</param>
    /// <param name="startedAtUtc">运行开始时间。</param>
    /// <param name="completedAtUtc">运行完成时间。</param>
    /// <param name="parameterName">错误参数名称。</param>
    private static void EnsureCurrentRunFile(string path, DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc,
        string parameterName)
    {
        var writtenAtUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        if (writtenAtUtc < startedAtUtc - FileTimestampTolerance ||
            writtenAtUtc > completedAtUtc + FileTimestampTolerance)
            throw new ArgumentException("制品文件不在当前运行时间窗口内。", parameterName);
    }

    /// <summary>
    /// 校验运行时间窗口及当前运行的新鲜度。
    /// </summary>
    /// <param name="startedAtUtc">运行开始时间。</param>
    /// <param name="completedAtUtc">运行完成时间。</param>
    /// <param name="requireFreshness">是否要求运行仍在有效期内。</param>
    private static void EnsureUtcWindow(DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc,
        bool requireFreshness)
    {
        if (startedAtUtc.Offset != TimeSpan.Zero || completedAtUtc.Offset != TimeSpan.Zero ||
            completedAtUtc < startedAtUtc || completedAtUtc > DateTimeOffset.UtcNow + FileTimestampTolerance ||
            requireFreshness && DateTimeOffset.UtcNow - completedAtUtc > MaximumCompletedRunAge)
            throw new ArgumentException("当前运行时间窗口无效或已过期。", nameof(completedAtUtc));
    }

    /// <summary>
    /// 校验固定矩阵制品路径。
    /// </summary>
    /// <param name="value">待验证的相对路径。</param>
    /// <param name="runRelativeDirectory">运行目录相对路径。</param>
    /// <param name="fileName">固定文件名。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的矩阵相对路径。</returns>
    private static string RequireExpectedMatrixPath(string value, string runRelativeDirectory, string fileName,
        string parameterName)
    {
        var expected = $"{runRelativeDirectory}/{fileName}";
        var normalized = NormalizeRelativePath(value, parameterName);
        if (!string.Equals(normalized, expected, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("发布矩阵必须写入当前运行目录的固定制品名称。", parameterName);
        return normalized;
    }

    /// <summary>
    /// 校验制品路径位于当前运行目录并具有指定扩展名。
    /// </summary>
    /// <param name="value">待验证的相对路径。</param>
    /// <param name="runRelativeDirectory">运行目录相对路径。</param>
    /// <param name="extension">要求的文件扩展名。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的制品相对路径。</returns>
    private static string RequirePathInRunDirectory(string value, string runRelativeDirectory, string extension,
        string parameterName)
    {
        var normalized = NormalizeRelativePath(value, parameterName);
        if (!normalized.StartsWith(runRelativeDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
            !normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("制品路径必须位于当前 Provider 运行目录且具有预期扩展名。", parameterName);
        return normalized;
    }

    /// <summary>
    /// 规范化并校验工作区相对路径。
    /// </summary>
    /// <param name="value">待验证的相对路径。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的相对路径。</returns>
    private static string NormalizeRelativePath(string value, string parameterName)
    {
        var path = RequireSafeText(value, parameterName).Replace('\\', '/');
        if (Path.IsPathRooted(path) || path.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("制品路径必须是无路径穿越的工作区相对路径。", parameterName);
        return path;
    }

    /// <summary>
    /// 将工作区根目录与安全相对路径组合为绝对路径。
    /// </summary>
    /// <param name="workspaceRoot">工作区根目录。</param>
    /// <param name="relativePath">工作区相对路径。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>组合后的绝对路径。</returns>
    private static string CombineRelativePath(string workspaceRoot, string relativePath, string parameterName)
    {
        var normalized = NormalizeRelativePath(relativePath, parameterName);
        var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("制品路径必须位于工作区内。", parameterName);
        return fullPath;
    }

    /// <summary>
    /// 校验工作区根目录为存在的绝对路径。
    /// </summary>
    /// <param name="value">工作区根目录。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的绝对路径。</returns>
    private static string RequireFullPath(string value, string parameterName)
    {
        var root = RequireSafeText(value, parameterName);
        if (!Path.IsPathRooted(root) || !Directory.Exists(root))
            throw new ArgumentException("工作区根目录必须存在且为绝对路径。", parameterName);
        return Path.GetFullPath(root);
    }

    /// <summary>
    /// 校验目标框架名称。
    /// </summary>
    /// <param name="value">目标框架名称。</param>
    /// <returns>规范化后的目标框架名称。</returns>
    private static string RequireFramework(string value)
    {
        var framework = RequireSafeText(value, nameof(value));
        if (!System.Text.RegularExpressions.Regex.IsMatch(framework, "^net[0-9]+\\.[0-9]+$"))
            throw new ArgumentException("目标框架格式无效。", nameof(value));
        return framework;
    }

    /// <summary>
    /// 校验运行标识。
    /// </summary>
    /// <param name="value">运行标识。</param>
    /// <returns>规范化后的运行标识。</returns>
    private static string RequireRunId(string value)
    {
        var runId = RequireSafeText(value, nameof(value));
        if (!System.Text.RegularExpressions.Regex.IsMatch(runId, "^[A-Za-z0-9][A-Za-z0-9.-]{2,127}$"))
            throw new ArgumentException("运行标识格式无效。", nameof(value));
        return runId;
    }

    /// <summary>
    /// 校验证据会话标识。
    /// </summary>
    /// <param name="value">证据会话标识。</param>
    /// <returns>规范化后的证据会话标识。</returns>
    private static string RequireEvidenceSessionId(string value)
    {
        var sessionId = RequireSafeText(value, nameof(value));
        if (!System.Text.RegularExpressions.Regex.IsMatch(sessionId, "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$"))
            throw new ArgumentException("证据会话标识格式无效。", nameof(value));
        return sessionId;
    }

    /// <summary>
    /// 校验 Provider 标识令牌。
    /// </summary>
    /// <param name="value">待验证的标识。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的标识。</returns>
    private static string RequireToken(string value, string parameterName)
    {
        var token = RequireSafeText(value, parameterName);
        if (!System.Text.RegularExpressions.Regex.IsMatch(token, "^[A-Za-z][A-Za-z0-9]*$"))
            throw new ArgumentException("Provider 名称格式无效。", parameterName);
        return token;
    }

    /// <summary>
    /// 校验文本值不为空且不包含敏感连接信息。
    /// </summary>
    /// <param name="value">待验证的文本值。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的文本值。</returns>
    private static string RequireSafeText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("值不能为空。", parameterName);
        var result = value.Trim();
        EnsureNoSensitiveText(result, parameterName);
        return result;
    }

    /// <summary>
    /// 校验发布元数据不使用占位值。
    /// </summary>
    /// <param name="value">发布元数据值。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的发布元数据。</returns>
    private static string RequireReleaseMetadata(string value, string parameterName)
    {
        var result = RequireSafeText(value, parameterName);
        if (string.Equals(result, "not-recorded", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result, "unknown", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("发布证据元数据不能使用占位值。", parameterName);
        return result;
    }

    /// <summary>
    /// 校验文本不包含连接信息。
    /// </summary>
    /// <param name="value">待检查的文本。</param>
    /// <param name="parameterName">参数名称。</param>
    private static void EnsureNoSensitiveText(string value, string parameterName)
    {
        if (value.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("发布证据不能包含连接信息。", parameterName);
    }

    /// <summary>
    /// 确认运行制品文件存在。
    /// </summary>
    /// <param name="path">制品文件路径。</param>
    /// <param name="parameterName">参数名称。</param>
    private static void EnsureFileExists(string path, string parameterName)
    {
        if (!File.Exists(path))
            throw new ArgumentException("当前运行制品不存在。", parameterName);
    }

    /// <summary>
    /// 校验二进制 manifest 的身份、路径和哈希。
    /// </summary>
    /// <param name="manifestPath">manifest 文件路径。</param>
    /// <param name="workspaceRoot">工作区根目录。</param>
    /// <param name="runRelativeDirectory">运行目录相对路径。</param>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="framework">目标框架。</param>
    /// <param name="runId">运行标识。</param>
    /// <param name="evidenceSessionId">证据聚合会话标识。</param>
    /// <param name="sourceIdentity">源码身份标识。</param>
    /// <param name="sourceState">源码工作区状态。</param>
    /// <param name="parameterName">错误参数名称。</param>
    private static void ValidateBinaryManifest(string manifestPath, string workspaceRoot,
        string runRelativeDirectory, string provider, string framework, string runId, string evidenceSessionId,
        string sourceIdentity, string sourceState, string parameterName)
    {
        var text = File.ReadAllText(manifestPath, Encoding.UTF8);
        EnsureNoSensitiveText(text, parameterName);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        EnsureSummaryValue(root, "Provider", provider);
        EnsureSummaryValue(root, "Framework", framework);
        EnsureSummaryValue(root, "RunId", runId);
        EnsureSummaryValue(root, "EvidenceSessionId", evidenceSessionId);
        EnsureSummaryValue(root, "SourceIdentity", sourceIdentity);
        EnsureSummaryValue(root, "SourceState", sourceState);
        if (!root.TryGetProperty("Assemblies", out var assemblies) || assemblies.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("二进制 manifest 缺少程序集清单。", parameterName);

        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in assemblies.EnumerateArray())
        {
            if (!item.TryGetProperty("Path", out var pathValue) || pathValue.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("Sha256", out var hashValue) || hashValue.ValueKind != JsonValueKind.String)
                throw new ArgumentException("二进制 manifest 条目无效。", parameterName);
            var relativePath = NormalizeManifestPath(pathValue.GetString(), parameterName);
            if (!relativePath.StartsWith(runRelativeDirectory + "/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("二进制 manifest 文件必须位于当前运行目录。", parameterName);
            var hash = hashValue.GetString()?.Trim();
            if (hash == null || !System.Text.RegularExpressions.Regex.IsMatch(hash, "^[A-Fa-f0-9]{64}$"))
                throw new ArgumentException("二进制 manifest 哈希无效。", parameterName);
            if (!entries.TryAdd(relativePath, hash))
                throw new ArgumentException("二进制 manifest 不能包含重复程序集。", parameterName);
            var fullPath = CombineRelativePath(workspaceRoot, relativePath, parameterName);
            EnsureFileExists(fullPath, parameterName);
            using var stream = File.OpenRead(fullPath);
            using var sha256 = SHA256.Create();
            var actualHash = Convert.ToHexString(sha256.ComputeHash(stream));
            if (!string.Equals(actualHash, hash, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("二进制 manifest 哈希与实际文件不一致。", parameterName);
        }

        var providerAssemblyName = string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase)
            ? "Sqlite"
            : provider;
        var requiredNames = new[]
        {
            $"Bing.Dapper.{providerAssemblyName}.Tests.Integration.dll",
            $"Bing.Dapper.{providerAssemblyName}.dll",
            "Bing.Data.Sql.dll"
        };
        foreach (var requiredName in requiredNames)
        {
            if (!entries.Keys.Any(path => string.Equals(Path.GetFileName(path), requiredName,
                    StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"二进制 manifest 缺少 {requiredName}。", parameterName);
        }
    }

    /// <summary>
    /// 校验归档能力矩阵绑定到指定运行。
    /// </summary>
    /// <param name="matrixPath">能力矩阵文件路径。</param>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="framework">目标框架。</param>
    /// <param name="runId">运行标识。</param>
    /// <param name="evidenceSessionId">证据聚合会话标识。</param>
    /// <param name="sourceIdentity">源码身份标识。</param>
    /// <param name="matrixRelativePath">矩阵相对路径。</param>
    /// <param name="trxRelativePath">TRX 相对路径。</param>
    /// <param name="binaryManifestRelativePath">二进制 manifest 相对路径。</param>
    private static void ValidateArchivedMatrix(string matrixPath, string provider, string framework, string runId,
        string evidenceSessionId, string sourceIdentity, string matrixRelativePath, string trxRelativePath,
        string binaryManifestRelativePath)
    {
        var text = File.ReadAllText(matrixPath, Encoding.UTF8);
        EnsureNoSensitiveText(text, nameof(matrixPath));
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("Entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
            throw new ArgumentException("归档能力矩阵缺少场景条目。", nameof(matrixPath));

        var hasReleaseEvidence = false;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("Provider", out var providerValue) ||
                providerValue.ValueKind != JsonValueKind.String ||
                !string.Equals(providerValue.GetString(), provider, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("归档能力矩阵包含其他 Provider 条目。", nameof(matrixPath));
            var state = entry.TryGetProperty("State", out var stateValue) &&
                        stateValue.ValueKind == JsonValueKind.String
                ? stateValue.GetString()
                : string.Empty;
            if (!entry.TryGetProperty("IntegrationEvidence", out var integrationEvidence))
            {
                if (string.Equals(state, nameof(ProviderCapabilityEvidenceState.RealIntegrationProven),
                        StringComparison.Ordinal))
                    throw new ArgumentException("归档能力矩阵的真实执行条目缺少证据。", nameof(matrixPath));
                continue;
            }
            if (integrationEvidence.ValueKind != JsonValueKind.Object ||
                !integrationEvidence.TryGetProperty("ArtifactKind", out var artifactKindValue) ||
                artifactKindValue.ValueKind != JsonValueKind.String ||
                !string.Equals(artifactKindValue.GetString(), nameof(ProviderCapabilityArtifactKind.ReleaseEvidence),
                    StringComparison.Ordinal))
                throw new ArgumentException("归档能力矩阵不得使用 TestGenerated 证据。", nameof(matrixPath));
            EnsureMatrixValue(integrationEvidence, "Provider", provider);
            EnsureMatrixValue(integrationEvidence, "Framework", framework);
            EnsureMatrixValue(integrationEvidence, "RunId", runId);
            EnsureMatrixValue(integrationEvidence, "EvidenceSessionId", evidenceSessionId);
            EnsureMatrixValue(integrationEvidence, "SourceIdentity", sourceIdentity);
            EnsureMatrixValue(integrationEvidence, "TrxPath", trxRelativePath);
            EnsureMatrixValue(integrationEvidence, "ArtifactPath", matrixRelativePath);
            EnsureMatrixValue(integrationEvidence, "BinaryManifestPath", binaryManifestRelativePath);
            hasReleaseEvidence = true;
        }
        if (!hasReleaseEvidence)
            throw new ArgumentException("归档能力矩阵缺少 ReleaseEvidence 条目。", nameof(matrixPath));
    }

    /// <summary>
    /// 校验矩阵字段与当前运行值一致。
    /// </summary>
    /// <param name="root">矩阵条目 JSON。</param>
    /// <param name="name">字段名称。</param>
    /// <param name="expected">期望值。</param>
    private static void EnsureMatrixValue(JsonElement root, string name, string expected)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
            throw new ArgumentException($"归档能力矩阵字段 {name} 与当前运行不一致。", nameof(root));
    }

    /// <summary>
    /// 规范化并校验二进制 manifest 路径。
    /// </summary>
    /// <param name="value">待验证的 manifest 路径。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的 manifest 路径。</returns>
    private static string NormalizeManifestPath(string value, string parameterName)
    {
        var path = RequireSafeText(value, parameterName).Replace('\\', '/');
        if (Path.IsPathRooted(path) || path.Split('/').Any(segment => segment is "" or "." or "..") ||
            !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("二进制 manifest 路径无效。", parameterName);
        return path;
    }
}

/// <summary>
/// 发布级 Provider 证据的唯一创建和写入入口。
/// </summary>
public static class ProviderReleaseEvidenceWriter
{
    /// <summary>
    /// 验证当前 CI 运行并写入同目录的 JSON 和 Markdown 发布矩阵。
    /// </summary>
    /// <param name="request">当前 runner 的制品绑定信息。</param>
    /// <returns>写入制品后的能力矩阵。</returns>
    public static ProviderCapabilityMatrix Write(ProviderReleaseEvidenceRequest request)
    {
        var run = ProviderReleaseEvidenceValidator.Validate(request);
        var matrix = ProviderCapabilityCatalog.CreateReleaseMatrix(run);
        matrix.WriteJson(run.MatrixJsonPath);
        matrix.WriteMarkdown(run.MatrixMarkdownPath);
        return matrix;
    }
}

/// <summary>
/// Provider 合同矩阵的逐场景目录。
/// </summary>
public static class ProviderCapabilityCatalog
{
    /// <summary>
    /// Provider 能力场景定义集合。
    /// </summary>
    private static readonly ProviderCapabilityScenarioDefinition[] Definitions =
    {
        Definition("MySql", "Core query", "scalar/list/single", "MySqlQueryTest.ExecuteScalar_ShouldReturnActualCount"),
        Definition("MySql", "Parameters and null", "explicit null and partial mapping", "MySqlQueryTest.ExecuteSql_ShouldPersistExplicitNullParameter"),
        Definition("MySql", "Type mapping", "json/decimal/datetime", "MySqlQueryTest.ExecuteSql_ShouldPersistJsonParameter"),
        Definition("MySql", "Streaming and resource release", "sync early termination", "MySqlQueryTest.StreamQuery_ShouldReleaseReaderWhenEnumerationStopsEarly"),
        Definition("MySql", "Cancellation", "pre-cancelled async stream", "MySqlQueryTest.StreamAsync_ShouldReleaseResourcesWhenCancelled"),
        Definition("MySql", "DML / mutation / batch", "affected rows", "MySqlExecutorTest.ExecuteSqlAsync_ShouldReturnAffectedRowsForInsertUpdateAndDelete"),
        Definition("MySql", "DML / mutation / batch", "provider batch mutation", "MySqlBatchAndMultipleContractTest.BatchCrud_WhenEntitiesAreProvided_ShouldPersistAndMutateRows"),
        Definition("MySql", "Transactions", "commit/rollback/uncompleted", "MySqlQueryTest.TransactionScope_ShouldRollbackWhenDisposedWithoutCompletion"),
        Definition("MySql", "Procedures / output / input-output", "OUT and INOUT", "MySqlQueryTest.ExecuteProcedure_ShouldExposeInputOutputParameter"),
        Definition("MySql", "Multiple result / Returning / Output", "multiple result contract", "MySqlBatchAndMultipleContractTest.Execute_WhenResultsAreReadInOrder_ShouldReleaseResourcesAfterDispose"),

        Definition("PostgreSql", "Core query", "scalar/list/single", "PostgreSqlQueryTest.ExecuteScalar_ShouldReturnActualCount"),
        Definition("PostgreSql", "Parameters and null", "explicit null and UUID array", "PostgreSqlQueryTest.ExecuteSql_ShouldPersistExplicitNullParameter"),
        Definition("PostgreSql", "Type mapping", "UUID/decimal/datetime/JSONB", "PostgreSqlQueryTest.ExecuteSingle_ShouldReturnMappedPostgreSqlTypes"),
        Definition("PostgreSql", "Streaming and resource release", "sync early termination", "PostgreSqlQueryTest.StreamQuery_ShouldReleaseReaderWhenEnumerationStopsEarly"),
        Definition("PostgreSql", "Cancellation", "pre-cancelled async stream", "PostgreSqlQueryTest.StreamAsync_ShouldReleaseResourcesWhenCancelled"),
        Definition("PostgreSql", "DML / mutation / batch", "affected rows", "PostgreSqlExecutorTest.ExecuteSqlAsync_ShouldReturnAffectedRowsForInsertUpdateAndDelete"),
        Definition("PostgreSql", "DML / mutation / batch", "optimized batch/update-from/delete-using", "PostgreSqlExecutorTest.UpdateBatchAsync_WhenProviderOptimized_ShouldUpdateRowsAndRejectConcurrencyConflict"),
        Definition("PostgreSql", "Transactions", "commit/rollback/uncompleted", "PostgreSqlQueryTest.TransactionScope_ShouldRollbackWhenDisposedWithoutCompletion"),
        Definition("PostgreSql", "Procedures / output / input-output", "native function result", "PostgreSqlProcedureContractTest.ExecuteFunctionAsync_WhenFunctionReturnsTable_ShouldMaterializeNativeRows"),
        Definition("PostgreSql", "Procedures / output / input-output", "output parameter semantics", ProviderCapabilityEvidenceState.Unsupported, null,
            "PostgreSQL Function 结果集与 SQL Server/MySQL OUT 参数不是同一统一语义；当前 Procedure/OUT 参数入口按数据库语义拒绝。"),
        Definition("PostgreSql", "Multiple result / Returning / Output", "multi-row Returning", "PostgreSqlExecutorTest.ExecuteQueryAsync_WhenInsertReturningIsConfigured_ShouldMaterializeReturnedRows"),

        Definition("SqlServer", "Core query", "scalar/list/single", "SqlServerQueryTest.GetValue_SelectOne_ShouldReturnOne"),
        Definition("SqlServer", "Parameters and null", "typed and explicit null", "SqlServerExecutionContractTest.Query_WhenTypedAndNullParametersAreBound_ShouldMaterializeValues"),
        Definition("SqlServer", "Type mapping", "decimal/datetime2/entity materialization", "SqlServerExecutionContractTest.Query_WhenTypedAndNullParametersAreBound_ShouldMaterializeValues"),
        Definition("SqlServer", "Streaming and resource release", "async stream and pre-cancel", "SqlServerExecutionContractTest.Query_WhenStreamingAndPreCancelled_ShouldReturnRowsAndCancel"),
        Definition("SqlServer", "Cancellation", "pre-cancelled query", "SqlServerExecutionContractTest.Query_WhenStreamingAndPreCancelled_ShouldReturnRowsAndCancel"),
        Definition("SqlServer", "DML / mutation / batch", "provider batch mutation", "SqlServerExecutionContractTest.BatchCrud_WhenEntitiesAreProvided_ShouldPersistAndMutateRows"),
        Definition("SqlServer", "Transactions", "commit and rollback", "SqlServerExecutionContractTest.Transaction_WhenCompletedOrRolledBack_ShouldKeepExpectedVisibility"),
        Definition("SqlServer", "Transactions", "uncompleted scope", "SqlServerExecutionContractTest.Transaction_WhenScopeIsDisposedWithoutCompletion_ShouldRollbackChanges"),
        Definition("SqlServer", "Procedures / output / input-output", "provider procedure contract", "SqlServerExecutionContractTest.ExecuteProcedureAsync_WhenOutputDirectionsAreConfigured_ShouldReturnValuesAndRows"),
        Definition("SqlServer", "Multiple result / Returning / Output", "INSERTED output", "SqlServerQueryAggregateTest.ExecuteQueryAsync_WhenInsertOutputIsConfigured_ShouldMaterializeReturnedRows"),
        Definition("SqlServer", "Multiple result / Returning / Output", "multiple result early dispose", "SqlServerExecutionContractTest.ExecuteMultiple_WhenFirstResultIsReadAndDisposedEarly_ShouldReleaseResources"),
        Definition("SqlServer", "Parameter limit", "IN parameter count 2098 control", "SqlServerExecutionContractTest.Query_WhenInParameterCountIs2098_ShouldExecuteSuccessfully"),
        Definition("SqlServer", "Parameter limit", "IN parameter count 2099", "SqlServerExecutionContractTest.Query_WhenInParameterCountIs2099_ShouldRejectAtSqlServerLimit"),
        Definition("SqlServer", "Parameter limit", "IN parameter count 2100", "SqlServerExecutionContractTest.Query_WhenInParameterCountIs2100_ShouldRejectAtSqlServerLimit"),
        Definition("SqlServer", "Parameter limit", "IN parameter count 2101", "SqlServerExecutionContractTest.Query_WhenInParameterCountIs2101_ShouldRejectAndRemainReusable"),
        Definition("SqlServer", "Parameter limit", "2099/2100/2101 planner boundaries", ProviderCapabilityEvidenceState.UnitProven, "SqlMutationBatchPlannerTest.Plan_WhenParameterCountIs2101_ShouldSplitWithoutExceedingSqlServerLimit", "SQL Server Provider Profile 的 2100 参数上限由 Bing.Data.Sql 单元测试验证。"),

        Definition("SQLite", "Core query", "controlled scalar/list/single", "SqliteExecutionIntegrationTest.ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence"),
        Definition("SQLite", "Parameters and null", "local explicit parameter binding", "SqliteExecutionIntegrationTest.SqlTextQuery_WhenStreamedOrCancelled_ShouldBindParametersAndReleaseExecutionResources"),
        Definition("SQLite", "Type mapping", "local materialization", "SqliteExecutionIntegrationTest.ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence"),
        Definition("SQLite", "Streaming and resource release", "sync early termination", "SqliteExecutionIntegrationTest.StreamQuery_ShouldReleaseReaderWhenEnumerationStopsEarly"),
        Definition("SQLite", "Cancellation", "during async enumeration", "SqliteExecutionIntegrationTest.StreamAsync_ShouldReleaseResourcesWhenCancelledDuringEnumeration"),
        Definition("SQLite", "DML / mutation / batch", "unified mutation CRUD", "SqliteMutationExecutionIntegrationTest.ExecuteMutation_WhenUnifiedMutationBuildersAreConfigured_ShouldExecuteCrud"),
        Definition("SQLite", "Transactions", "commit/rollback/uncompleted", "SqliteExecutionIntegrationTest.TransactionScope_ShouldRollbackWhenDisposedWithoutCompletion"),
        Definition("SQLite", "Procedures / output / input-output", "stored procedure output", ProviderCapabilityEvidenceState.Unsupported, null,
            "SQLite Provider 不支持存储过程或输出参数。"),
        Definition("SQLite", "Multiple result / Returning / Output", "multiple query cancellation", "SqliteMultipleQueryIntegrationTest.ReadAsync_WhenCancellationRequested_ShouldReleaseExecutionResources"),

        Definition("Oracle", "Core query", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有授权 Oracle 测试数据库和可重复 fixture。"),
        Definition("Oracle", "Parameters and null", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有授权 Oracle 测试数据库和可重复 fixture。"),
        Definition("Oracle", "Type mapping", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有授权 Oracle 测试数据库和可重复 fixture。"),
        Definition("Oracle", "Streaming and resource release", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有授权 Oracle 测试数据库和可重复 fixture。"),
        Definition("Oracle", "Cancellation", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有授权 Oracle 测试数据库和可重复 fixture。"),
        Definition("Oracle", "DML / mutation / batch", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有授权 Oracle 测试数据库和可重复 fixture。"),
        Definition("Oracle", "Transactions", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有授权 Oracle 测试数据库和可重复 fixture。"),
        Definition("Oracle", "Procedures / output / input-output", "safe fixture", ProviderCapabilityEvidenceState.ImplementationGap, null,
            "尚无安全 Oracle 过程 fixture。"),
        Definition("Oracle", "Multiple result / Returning / Output", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有授权 Oracle 测试数据库和可重复 fixture。"),

        Definition("Doris", "Core query", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有专属 Doris 合同 lane 或授权测试数据库。"),
        Definition("Doris", "Parameters and null", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有专属 Doris 合同 lane 或授权测试数据库。"),
        Definition("Doris", "Type mapping", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有专属 Doris 合同 lane 或授权测试数据库。"),
        Definition("Doris", "Streaming and resource release", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有专属 Doris 合同 lane 或授权测试数据库。"),
        Definition("Doris", "Cancellation", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有专属 Doris 合同 lane 或授权测试数据库。"),
        Definition("Doris", "DML / mutation / batch", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有专属 Doris 合同 lane 或授权测试数据库。"),
        Definition("Doris", "Transactions", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有专属 Doris 合同 lane 或授权测试数据库。"),
        Definition("Doris", "Procedures / output / input-output", "provider support", ProviderCapabilityEvidenceState.ImplementationGap, null,
            "没有 Doris 过程输出能力的实现或 fixture。"),
        Definition("Doris", "Multiple result / Returning / Output", "protected provider lane", ProviderCapabilityEvidenceState.NotExecuted, null,
            "没有专属 Doris 合同 lane 或授权测试数据库。")
    };

    /// <summary>
    /// 创建未执行状态的完整跨 Provider 能力矩阵。
    /// </summary>
    /// <returns>完整的基础能力矩阵。</returns>
    public static ProviderCapabilityMatrix CreateBaselineMatrix()
    {
        var matrix = new ProviderCapabilityMatrix();
        foreach (var definition in Definitions)
            matrix.Add(CreateEvidence(definition, null));
        return matrix;
    }

    /// <summary>
    /// 基于已验证的当前 Provider 运行创建同目录发布矩阵。
    /// </summary>
    /// <param name="run">已验证的 CI 运行。</param>
    /// <returns>当前 Provider 的发布能力矩阵。</returns>
    internal static ProviderCapabilityMatrix CreateReleaseMatrix(ProviderReleaseEvidenceValidator.ProviderValidatedReleaseRun run)
    {
        if (run == null)
            throw new ArgumentNullException(nameof(run));
        var matrix = new ProviderCapabilityMatrix();
        foreach (var definition in Definitions.Where(item => string.Equals(item.Provider, run.Provider,
                     StringComparison.OrdinalIgnoreCase)))
            matrix.Add(CreateEvidence(definition, run));
        if (matrix.Entries.Count == 0)
            throw new ArgumentException("未找到 Provider 能力目录。", nameof(run));
        return matrix;
    }

    /// <summary>
    /// 返回用于直接单元测试的完整场景定义。
    /// </summary>
    /// <returns>能力场景定义集合。</returns>
    public static IReadOnlyList<ProviderCapabilityScenarioDefinition> GetDefinitions() => Definitions;

    /// <summary>
    /// 根据场景定义和运行结果创建能力证据。
    /// </summary>
    /// <param name="definition">能力场景定义。</param>
    /// <param name="run">已验证的 Provider 运行。</param>
    /// <returns>能力证据。</returns>
    private static ProviderCapabilityEvidence CreateEvidence(ProviderCapabilityScenarioDefinition definition,
        ProviderReleaseEvidenceValidator.ProviderValidatedReleaseRun run)
    {
        if (run != null && definition.State == ProviderCapabilityEvidenceState.NotExecuted &&
            run.PassedTestMethods.Any(name => name.EndsWith(definition.TestMethod, StringComparison.Ordinal)))
        {
            var metadata = ProviderIntegrationEvidenceMetadata.CreateValidatedReleaseEvidence(run,
                definition.TestMethod);
            return ProviderCapabilityEvidence.CreateRealIntegration(definition.Provider, definition.Capability,
                definition.Scenario, $"TRX 已通过：{definition.TestMethod}", run.MatrixJsonRelativePath, metadata);
        }
        var evidence = string.IsNullOrWhiteSpace(definition.TestMethod)
            ? definition.Evidence
            : $"测试方法：{definition.TestMethod}。{definition.Evidence}";
        return new ProviderCapabilityEvidence(definition.Provider, definition.Capability, definition.Scenario,
            definition.State, evidence, definition.TestMethod ?? string.Empty);
    }

    /// <summary>
    /// 创建 Provider 能力场景定义。
    /// </summary>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="capability">能力名称。</param>
    /// <param name="scenario">场景名称。</param>
    /// <param name="testMethod">测试方法名称。</param>
    /// <returns>场景定义。</returns>
    private static ProviderCapabilityScenarioDefinition Definition(string provider, string capability, string scenario,
        string testMethod) => Definition(provider, capability, scenario, ProviderCapabilityEvidenceState.NotExecuted,
        testMethod, "受保护 Provider lane 尚未执行；默认 gate 跳过不构成真实执行证据。");

    /// <summary>
    /// 创建 Provider 能力场景定义。
    /// </summary>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="capability">能力名称。</param>
    /// <param name="scenario">场景名称。</param>
    /// <param name="state">能力证据状态。</param>
    /// <param name="testMethod">测试方法名称。</param>
    /// <param name="evidence">证据说明。</param>
    /// <returns>场景定义。</returns>
    private static ProviderCapabilityScenarioDefinition Definition(string provider, string capability, string scenario,
        ProviderCapabilityEvidenceState state, string testMethod, string evidence) => new(provider, capability,
        scenario, state, testMethod, evidence);
}

/// <summary>
/// Provider 能力目录中的一个唯一场景。
/// </summary>
public sealed class ProviderCapabilityScenarioDefinition
{
    /// <summary>
    /// 初始化一个 <see cref="ProviderCapabilityScenarioDefinition"/> 类型的实例。
    /// </summary>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="capability">能力名称。</param>
    /// <param name="scenario">场景名称。</param>
    /// <param name="state">能力证据状态。</param>
    /// <param name="testMethod">测试方法名称。</param>
    /// <param name="evidence">证据说明。</param>
    internal ProviderCapabilityScenarioDefinition(string provider, string capability, string scenario,
        ProviderCapabilityEvidenceState state, string testMethod, string evidence)
    {
        Provider = provider;
        Capability = capability;
        Scenario = scenario;
        State = state;
        TestMethod = testMethod;
        Evidence = evidence;
    }

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
    /// 获取测试方法名称。
    /// </summary>
    public string TestMethod { get; }

    /// <summary>
    /// 获取证据说明。
    /// </summary>
    public string Evidence { get; }
}
