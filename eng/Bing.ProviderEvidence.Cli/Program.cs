using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bing.Test.Shared;

var arguments = ParseArguments(args);
try
{
    if (arguments.TryGetValue("aggregate-output-directory", out var aggregateOutputDirectory))
    {
        var aggregateEvidenceSessionId = arguments.TryGetValue("evidence-session-id", out var configuredSessionId)
            ? configuredSessionId
            : null;
        var aggregateSourceIdentity = arguments.TryGetValue("source-identity", out var configuredSourceIdentity)
            ? configuredSourceIdentity
            : null;
        AggregateReports(GetRequired(arguments, "workspace-root"), aggregateOutputDirectory,
            arguments.TryGetValue("provider-results-directory", out var providerResultsDirectory)
                ? providerResultsDirectory
                : "artifacts/provider-test-results",
            arguments.TryGetValue("sqlite-results-directory", out var sqliteResultsDirectory)
                ? sqliteResultsDirectory
                : null,
            arguments.TryGetValue("unit-results-directory", out var unitResultsDirectory)
                ? unitResultsDirectory
                : null,
            arguments.TryGetValue("unit-inventory", out var unitInventory)
                ? unitInventory
                : null,
            arguments.TryGetValue("formal-host-results-directory", out var formalHostDirectory)
                ? formalHostDirectory
                : null,
            arguments.TryGetValue("rs0026-inventory", out var rs0026Inventory)
                ? rs0026Inventory
                : null,
            arguments.TryGetValue("api-gate-file", out var apiGateFile) ? apiGateFile : null,
            aggregateEvidenceSessionId, aggregateSourceIdentity);
        return 0;
    }
    if (arguments.TryGetValue("baseline-output-directory", out var baselineOutputDirectory))
    {
        WriteBaselineMatrix(GetRequired(arguments, "workspace-root"), baselineOutputDirectory);
        return 0;
    }
    var request = new ProviderReleaseEvidenceRequest
    {
        WorkspaceRoot = GetRequired(arguments, "workspace-root"),
        Provider = GetRequired(arguments, "provider"),
        Framework = GetRequired(arguments, "framework"),
        RunId = GetRequired(arguments, "run-id"),
        EvidenceSessionId = GetRequired(arguments, "evidence-session-id"),
        TrxRelativePath = GetRequired(arguments, "trx"),
        SummaryRelativePath = GetRequired(arguments, "summary"),
        MatrixJsonRelativePath = GetRequired(arguments, "matrix-json"),
        MatrixMarkdownRelativePath = GetRequired(arguments, "matrix-markdown"),
        BinaryManifestRelativePath = GetRequired(arguments, "binary-manifest"),
        SourceIdentity = GetRequired(arguments, "source-identity"),
        ProviderVersion = GetRequired(arguments, "provider-version"),
        DatabaseVersion = GetRequired(arguments, "database-version"),
        DriverVersion = GetRequired(arguments, "driver-version"),
        RuntimeVersion = GetRequired(arguments, "runtime-version"),
        OperatingSystem = GetRequired(arguments, "operating-system"),
        SourceState = GetRequired(arguments, "source-state"),
        StartedAtUtc = ParseUtc(arguments, "started-at-utc"),
        CompletedAtUtc = ParseUtc(arguments, "completed-at-utc")
    };
    var matrix = ProviderReleaseEvidenceWriter.Write(request);
    Console.WriteLine($"Provider release matrix generated: Provider={request.Provider}; Framework={request.Framework}; RunId={request.RunId}; ReleaseReady={matrix.IsReleaseReady}.");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or InvalidDataException or FormatException or IOException or System.Xml.XmlException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine($"Provider release evidence validation failed: {exception.Message}");
    return 1;
}

static Dictionary<string, string> ParseArguments(string[] values)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < values.Length; index += 2)
    {
        if (!values[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= values.Length ||
            string.IsNullOrWhiteSpace(values[index + 1]))
            throw new ArgumentException("CLI 参数必须使用 --name value 形式。");
        var name = values[index][2..];
        if (!result.TryAdd(name, values[index + 1]))
            throw new ArgumentException("CLI 参数不能重复。");
    }
    return result;
}

static string GetRequired(IReadOnlyDictionary<string, string> values, string name) =>
    values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"缺少 CLI 参数 --{name}。");

static DateTimeOffset ParseUtc(IReadOnlyDictionary<string, string> values, string name)
{
    var value = GetRequired(values, name);
    if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result) ||
        result.Offset != TimeSpan.Zero)
        throw new ArgumentException($"CLI 参数 --{name} 必须是 UTC ISO-8601 时间。", name);
    return result;
}

static void WriteBaselineMatrix(string workspaceRoot, string outputDirectory)
{
    if (!Path.IsPathRooted(workspaceRoot) || !Directory.Exists(workspaceRoot))
        throw new ArgumentException("工作区根目录必须存在且为绝对路径。", nameof(workspaceRoot));
    var normalizedDirectory = outputDirectory.Replace('\\', '/').Trim('/');
    if (Path.IsPathRooted(normalizedDirectory) ||
        normalizedDirectory.Split('/').Any(segment => segment is "" or "." or "..") ||
        !normalizedDirectory.StartsWith("artifacts/provider-test-results/", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("基线矩阵目录必须是 artifacts/provider-test-results 下的相对目录。",
            nameof(outputDirectory));
    var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var destination = Path.GetFullPath(Path.Combine(root,
        normalizedDirectory.Replace('/', Path.DirectorySeparatorChar)));
    if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("基线矩阵目录必须位于工作区内。", nameof(outputDirectory));
    var matrix = ProviderCapabilityCatalog.CreateBaselineMatrix();
    matrix.WriteJson(Path.Combine(destination, "provider-capability-matrix.json"));
    matrix.WriteMarkdown(Path.Combine(destination, "provider-capability-matrix.md"));
    Console.WriteLine($"Provider capability baseline generated: Entries={matrix.Entries.Count}; ReleaseReady={matrix.IsReleaseReady}.");
}

static void AggregateReports(string workspaceRoot, string outputDirectory, string providerResultsDirectory,
    string sqliteResultsDirectory, string unitResultsDirectory, string unitInventory,
    string formalHostResultsDirectory,
    string rs0026Inventory, string apiGateFile, string evidenceSessionId, string sourceIdentity)
{
    var root = RequireWorkspaceRoot(workspaceRoot);
    var outputRoot = ResolveWorkspaceDirectory(root, outputDirectory, "artifacts/test-results", createIfMissing: true);
    var providerRoot = ResolveWorkspaceDirectory(root, providerResultsDirectory, "artifacts/provider-test-results");
    var providerRuns = LoadProviderRuns(root, providerRoot, evidenceSessionId, sourceIdentity);
    var sqliteRuns = string.IsNullOrWhiteSpace(sqliteResultsDirectory)
        ? Array.Empty<IntegrationRun>()
        : LoadSqliteRuns(root, ResolveWorkspaceDirectory(root, sqliteResultsDirectory, "artifacts/test-results"),
            evidenceSessionId, sourceIdentity);
    var integrationRuns = providerRuns.Concat(sqliteRuns)
        .OrderBy(run => run.Provider, StringComparer.OrdinalIgnoreCase)
        .ThenBy(run => run.Framework, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var unitRuns = string.IsNullOrWhiteSpace(unitResultsDirectory)
        ? Array.Empty<UnitRun>()
        : LoadUnitRuns(root, ResolveWorkspaceDirectory(root, unitResultsDirectory, "artifacts/test-results"));
    var unitEvidenceComplete = ValidateUnitEvidence(root, unitResultsDirectory, unitInventory, evidenceSessionId,
        sourceIdentity, unitRuns);
    var formalHostComplete = ValidateFormalHostEvidence(root, formalHostResultsDirectory, evidenceSessionId,
        sourceIdentity);
    var rs0026GatePassed = ValidateRs0026Evidence(root, rs0026Inventory, evidenceSessionId, sourceIdentity);
    var apiGatePassed = ValidateApiEvidence(root, apiGateFile, evidenceSessionId, sourceIdentity);
    var matrix = CreateAggregateCapabilityMatrix(integrationRuns, unitRuns, unitEvidenceComplete, formalHostComplete,
        rs0026GatePassed, apiGatePassed, evidenceSessionId);
    WriteUtf8(Path.Combine(outputRoot, "provider-capability-matrix.json"),
        JsonSerializer.Serialize(matrix, new JsonSerializerOptions { WriteIndented = true }));
    WriteUtf8(Path.Combine(outputRoot, "provider-capability-matrix.md"), CreateCapabilityMarkdown(matrix));
    WriteUtf8(Path.Combine(outputRoot, "unit-test-report.md"), CreateUnitMarkdown(unitRuns));
    WriteUtf8(Path.Combine(outputRoot, "integration-test-report.md"), CreateIntegrationMarkdown(integrationRuns));
    Console.WriteLine($"Provider reports generated: Runs={integrationRuns.Length}; UnitRuns={unitRuns.Length}; Output={outputDirectory}.");
}

static string RequireWorkspaceRoot(string value)
{
    if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value) || !Directory.Exists(value))
        throw new ArgumentException("工作区根目录必须存在且为绝对路径。", nameof(value));
    return Path.GetFullPath(value);
}

static string ResolveWorkspaceDirectory(string workspaceRoot, string relativePath, string requiredPrefix,
    bool createIfMissing = false)
{
    var normalized = RequireRelativeArtifactPath(relativePath, requiredPrefix);
    var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var path = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
    if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
        !path.Equals(root, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("制品目录必须位于工作区内。", nameof(relativePath));
    if (!Directory.Exists(path) && createIfMissing)
        Directory.CreateDirectory(path);
    if (!Directory.Exists(path))
        throw new DirectoryNotFoundException($"制品目录不存在：{normalized}。");
    return path;
}

static string RequireRelativeArtifactPath(string value, string requiredPrefix)
{
    var normalized = (value ?? string.Empty).Replace('\\', '/').Trim('/');
    var prefix = requiredPrefix.TrimEnd('/');
    if (Path.IsPathRooted(normalized) || string.IsNullOrWhiteSpace(normalized) ||
        normalized.Split('/').Any(segment => segment is "" or "." or "..") ||
        !(normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
          normalized.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)))
        throw new ArgumentException($"目录必须是 {requiredPrefix} 下的工作区相对路径。", nameof(value));
    return normalized;
}

static IntegrationRun[] LoadProviderRuns(string workspaceRoot, string providerRoot, string evidenceSessionId,
    string sourceIdentity)
{
    var runs = new List<IntegrationRun>();
    foreach (var summaryPath in Directory.EnumerateFiles(providerRoot, "*.json", SearchOption.AllDirectories))
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(summaryPath, Encoding.UTF8));
        }
        catch (JsonException)
        {
            continue;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!HasString(root, "Provider") || !HasString(root, "Framework") || !HasString(root, "RunId") ||
                !HasString(root, "EvidenceSessionId") || !HasString(root, "TrxPath"))
                continue;
            if (string.Equals(GetString(root, "Provider"), "SQLite", StringComparison.OrdinalIgnoreCase))
                continue;
            var trxPath = ResolveWorkspaceFile(workspaceRoot, root.GetProperty("TrxPath").GetString());
            var run = new IntegrationRun
            {
                Provider = GetString(root, "Provider"),
                Framework = GetString(root, "Framework"),
                RunId = GetString(root, "RunId"),
                EvidenceSessionId = GetString(root, "EvidenceSessionId"),
                SummaryPath = ToWorkspaceRelativePath(workspaceRoot, summaryPath),
                TrxPath = GetString(root, "TrxPath"),
                SourceState = GetString(root, "SourceState"),
                SourceIdentity = GetString(root, "SourceIdentity"),
                ProviderVersion = GetString(root, "ProviderVersion"),
                DatabaseVersion = GetString(root, "DatabaseVersion"),
                DriverVersion = GetString(root, "DriverVersion"),
                RuntimeVersion = GetString(root, "RuntimeVersion"),
                OperatingSystem = GetString(root, "OperatingSystem"),
                DatabaseName = GetString(root, "DatabaseName"),
                ConfigurationSource = GetString(root, "ConfigurationSource"),
                ExecutionStatus = GetString(root, "ExecutionStatus"),
                Discovered = GetInt(root, "Discovered"),
                Executed = GetInt(root, "Executed"),
                Passed = GetInt(root, "Passed"),
                Failed = GetInt(root, "Failed"),
                Skipped = GetInt(root, "Skipped"),
                CoreSkipped = GetInt(root, "CoreSkipped"),
                OptionalSkipped = GetInt(root, "OptionalSkipped"),
                CompletedAtUtc = GetUtc(root, "CompletedAtUtc"),
                PassedTestMethods = LoadPassedTestMethods(trxPath),
                BinaryManifestPath = GetString(root, "BinaryManifestPath")
            };
            run.ReleaseEvidenceValid = TryValidateReleaseEvidence(workspaceRoot, summaryPath, run,
                evidenceSessionId, sourceIdentity);
            runs.Add(run);
        }
    }
    return runs.ToArray();
}

static IntegrationRun[] LoadSqliteRuns(string workspaceRoot, string sqliteRoot, string evidenceSessionId,
    string sourceIdentity)
{
    var runs = new List<IntegrationRun>();
    foreach (var matrixPath in Directory.EnumerateFiles(sqliteRoot, "*.json", SearchOption.AllDirectories))
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(matrixPath, Encoding.UTF8));
        }
        catch (JsonException)
        {
            continue;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("Entries", out var entries) || entries.ValueKind != JsonValueKind.Array ||
                !entries.EnumerateArray().Any())
                continue;
            var entryValues = entries.EnumerateArray().ToArray();
            if (!entryValues.All(entry => string.Equals(GetString(entry, "Provider"), "SQLite",
                    StringComparison.OrdinalIgnoreCase)))
                continue;
            var matrixDirectory = Path.GetDirectoryName(matrixPath);
            var fixedMatrixPath = Path.Combine(matrixDirectory!, "provider-capability-matrix.json");
            if (File.Exists(fixedMatrixPath) &&
                !string.Equals(matrixPath, fixedMatrixPath, StringComparison.OrdinalIgnoreCase))
                continue;
            var matrixFileName = Path.GetFileName(matrixPath);
            var frameworkMatch = Regex.Match(matrixFileName, "(?<tfm>net[0-9]+\\.[0-9]+)",
                RegexOptions.IgnoreCase);
            string framework;
            string summaryPath = null;
            if (frameworkMatch.Success)
            {
                framework = frameworkMatch.Groups["tfm"].Value;
                var candidate = Path.Combine(matrixDirectory!, $"sqlite-{framework}.json");
                if (File.Exists(candidate))
                    summaryPath = candidate;
            }
            else if (string.Equals(matrixFileName, "provider-capability-matrix.json",
                         StringComparison.OrdinalIgnoreCase))
            {
                var summaryCandidates = Directory.EnumerateFiles(matrixDirectory!, "sqlite-net*.json",
                    SearchOption.TopDirectoryOnly).ToArray();
                if (summaryCandidates.Length != 1)
                    continue;
                summaryPath = summaryCandidates[0];
                using var summaryDocument = JsonDocument.Parse(File.ReadAllText(summaryPath, Encoding.UTF8));
                framework = GetString(summaryDocument.RootElement, "Framework");
            }
            else
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(framework))
                continue;

            if (summaryPath != null)
            {
                using var summaryDocument = JsonDocument.Parse(File.ReadAllText(summaryPath, Encoding.UTF8));
                var summary = summaryDocument.RootElement;
                if (!HasValue(summary, "Provider", "SQLite") ||
                    !string.Equals(GetString(summary, "Framework"), framework, StringComparison.OrdinalIgnoreCase))
                    continue;
                var summaryTrxPath = GetString(summary, "TrxPath");
                var summaryTrxFilePath = ResolveWorkspaceFile(workspaceRoot, summaryTrxPath);
                var releaseRun = new IntegrationRun
                {
                    Provider = GetString(summary, "Provider"),
                    Framework = GetString(summary, "Framework"),
                    RunId = GetString(summary, "RunId"),
                    EvidenceSessionId = GetString(summary, "EvidenceSessionId"),
                    SummaryPath = ToWorkspaceRelativePath(workspaceRoot, summaryPath),
                    TrxPath = summaryTrxPath,
                    SourceState = GetString(summary, "SourceState"),
                    SourceIdentity = GetString(summary, "SourceIdentity"),
                    ProviderVersion = GetString(summary, "ProviderVersion"),
                    DatabaseVersion = GetString(summary, "DatabaseVersion"),
                    DriverVersion = GetString(summary, "DriverVersion"),
                    RuntimeVersion = GetString(summary, "RuntimeVersion"),
                    OperatingSystem = GetString(summary, "OperatingSystem"),
                    DatabaseName = GetString(summary, "DatabaseName"),
                    ConfigurationSource = GetString(summary, "ConfigurationSource"),
                    ExecutionStatus = GetString(summary, "ExecutionStatus"),
                    Discovered = GetInt(summary, "Discovered"),
                    Executed = GetInt(summary, "Executed"),
                    Passed = GetInt(summary, "Passed"),
                    Failed = GetInt(summary, "Failed"),
                    Skipped = GetInt(summary, "Skipped"),
                    CoreSkipped = GetInt(summary, "CoreSkipped"),
                    OptionalSkipped = GetInt(summary, "OptionalSkipped"),
                    CompletedAtUtc = GetUtc(summary, "CompletedAtUtc"),
                    PassedTestMethods = LoadPassedTestMethods(summaryTrxFilePath),
                    BinaryManifestPath = GetString(summary, "BinaryManifestPath")
                };
                releaseRun.ReleaseEvidenceValid = TryValidateReleaseEvidence(workspaceRoot, summaryPath,
                    releaseRun, evidenceSessionId, sourceIdentity);
                runs.Add(releaseRun);
                continue;
            }

            var entry = entryValues[0];
            if (!entry.TryGetProperty("IntegrationEvidence", out var evidence) ||
                evidence.ValueKind != JsonValueKind.Object)
                continue;
            var trxPath = GetString(evidence, "TrxPath");
            var trxFilePath = ResolveWorkspaceFile(workspaceRoot, trxPath);
            var runId = Path.GetFileNameWithoutExtension(matrixPath);
            runs.Add(new IntegrationRun
            {
                Provider = "SQLite",
                Framework = framework,
                RunId = runId,
                EvidenceSessionId = GetString(evidence, "EvidenceSessionId"),
                SummaryPath = string.Empty,
                TrxPath = trxPath,
                SourceState = "test-generated",
                SourceIdentity = GetString(evidence, "SourceIdentity"),
                ProviderVersion = GetString(evidence, "ProviderVersion"),
                DatabaseVersion = GetString(evidence, "DatabaseVersion"),
                DriverVersion = GetString(evidence, "DriverVersion"),
                RuntimeVersion = string.Empty,
                OperatingSystem = string.Empty,
                DatabaseName = string.Empty,
                ConfigurationSource = "ControlledLocalFile",
                ExecutionStatus = "TestGenerated",
                Discovered = ReadTrxCounter(trxFilePath, "total"),
                Executed = ReadTrxCounter(trxFilePath, "passed") + ReadTrxCounter(trxFilePath, "failed"),
                Passed = ReadTrxCounter(trxFilePath, "passed"),
                Failed = ReadTrxCounter(trxFilePath, "failed"),
                Skipped = ReadTrxCounter(trxFilePath, "notExecuted"),
                CoreSkipped = ReadTrxCounter(trxFilePath, "notExecuted"),
                OptionalSkipped = 0,
                CompletedAtUtc = GetUtc(evidence, "CompletedAtUtc"),
                PassedTestMethods = LoadPassedTestMethods(trxFilePath)
            });
        }
    }
    return runs.ToArray();
}

static UnitRun[] LoadUnitRuns(string workspaceRoot, string unitRoot)
{
    var runs = new List<UnitRun>();
    foreach (var trxPath in Directory.EnumerateFiles(unitRoot, "*.trx", SearchOption.AllDirectories))
    {
        var document = XDocument.Load(trxPath, LoadOptions.None);
        var counters = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Counters");
        var fileName = Path.GetFileNameWithoutExtension(trxPath);
        var fileMatch = Regex.Match(fileName, "^(?<project>.+)-(?<framework>net[0-9]+\\.[0-9]+)$",
            RegexOptions.IgnoreCase);
        if (counters == null || !fileMatch.Success)
            continue;
        var project = fileMatch.Groups["project"].Value;
        var framework = fileMatch.Groups["framework"].Value;
        runs.Add(new UnitRun
        {
            Project = project,
            Framework = framework,
            TrxPath = ToWorkspaceRelativePath(workspaceRoot, trxPath),
            Discovered = ReadCounter(counters, "total"),
            Executed = ReadCounter(counters, "passed") + ReadCounter(counters, "failed"),
            Passed = ReadCounter(counters, "passed"),
            Failed = ReadCounter(counters, "failed"),
            Skipped = ReadCounter(counters, "notExecuted"),
            Duration = document.Root?.Descendants().FirstOrDefault(element => element.Name.LocalName == "ResultSummary")?
                .Attribute("duration")?.Value ?? string.Empty,
            AssemblyIdentityValid = ValidateUnitTrxIdentity(document, counters, project, framework)
        });
    }
    return runs.OrderBy(run => run.Project, StringComparer.OrdinalIgnoreCase)
        .ThenBy(run => run.Framework, StringComparer.OrdinalIgnoreCase).ToArray();
}

static bool ValidateUnitEvidence(string workspaceRoot, string unitResultsDirectory, string unitInventory,
    string evidenceSessionId, string sourceIdentity, IReadOnlyCollection<UnitRun> unitRuns)
{
    if (string.IsNullOrWhiteSpace(unitResultsDirectory) || string.IsNullOrWhiteSpace(unitInventory) ||
        string.IsNullOrWhiteSpace(evidenceSessionId) || string.IsNullOrWhiteSpace(sourceIdentity))
        return false;
    try
    {
        var normalizedUnitDirectory = RequireRelativeArtifactPath(unitResultsDirectory, "artifacts/test-results");
        var normalizedInventoryPath = RequireRelativeArtifactPath(unitInventory, "artifacts/test-results");
        if (!string.Equals(normalizedInventoryPath, normalizedUnitDirectory + "/unit-inventory.json",
                StringComparison.OrdinalIgnoreCase))
            return false;
        var inventoryPath = ResolveWorkspaceFile(workspaceRoot, normalizedInventoryPath);
        using var document = JsonDocument.Parse(File.ReadAllText(inventoryPath, Encoding.UTF8));
        var inventory = document.RootElement;
        if (!HasValue(inventory, "EvidenceSessionId", evidenceSessionId) ||
            !HasValue(inventory, "SourceIdentity", sourceIdentity) ||
            !HasValue(inventory, "SourceState", "clean") ||
            !inventory.TryGetProperty("Entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return false;

        var startedAtUtc = GetUtc(inventory, "StartedAtUtc");
        var completedAtUtc = GetUtc(inventory, "CompletedAtUtc");
        var now = DateTimeOffset.UtcNow;
        var timestampTolerance = TimeSpan.FromMinutes(2);
        if (startedAtUtc == DateTimeOffset.MinValue || completedAtUtc == DateTimeOffset.MinValue ||
            completedAtUtc < startedAtUtc || completedAtUtc > now + timestampTolerance)
            return false;
        var inventoryTime = new DateTimeOffset(File.GetLastWriteTimeUtc(inventoryPath), TimeSpan.Zero);
        if (inventoryTime < startedAtUtc - timestampTolerance || inventoryTime > completedAtUtc + timestampTolerance)
            return false;

        var expectedEntries = GetExpectedUnitEntries();
        if (entries.GetArrayLength() != expectedEntries.Count || unitRuns.Count != expectedEntries.Count)
            return false;
        var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unitRunsByPath = unitRuns.ToDictionary(run => run.TrxPath, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.EnumerateArray())
        {
            var project = GetString(entry, "Project");
            var framework = GetString(entry, "Framework");
            var key = $"{project}|{framework}";
            if (!expectedEntries.Contains(key) || !seenEntries.Add(key))
                return false;
            var trxPath = RequireRelativeArtifactPath(GetString(entry, "TrxPath"), "artifacts/test-results");
            if (!trxPath.StartsWith(normalizedUnitDirectory + "/", StringComparison.OrdinalIgnoreCase))
                return false;
            var trxFilePath = ResolveWorkspaceFile(workspaceRoot, trxPath);
            var expectedTrxFileName = $"{project}-{framework}.trx";
            if (!string.Equals(Path.GetFileName(trxFilePath), expectedTrxFileName,
                    StringComparison.OrdinalIgnoreCase))
                return false;
            var trxTime = new DateTimeOffset(File.GetLastWriteTimeUtc(trxFilePath), TimeSpan.Zero);
            if (trxTime < startedAtUtc - timestampTolerance || trxTime > completedAtUtc + timestampTolerance)
                return false;
            var hash = GetString(entry, "Sha256");
            if (!Regex.IsMatch(hash, "^[A-Fa-f0-9]{64}$"))
                return false;
            using var stream = File.OpenRead(trxFilePath);
            var actualHash = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actualHash, hash, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!unitRunsByPath.TryGetValue(trxPath, out var run) ||
                !string.Equals(run.Project, project, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(run.Framework, framework, StringComparison.OrdinalIgnoreCase) ||
                !run.AssemblyIdentityValid || run.Passed <= 0 || run.Failed != 0 || run.Skipped != 0)
                return false;
        }
        return seenEntries.Count == expectedEntries.Count;
    }
    catch (Exception exception) when (exception is ArgumentException or FileNotFoundException or
        DirectoryNotFoundException or JsonException or IOException)
    {
        return false;
    }
}

static bool ValidateUnitTrxIdentity(XDocument document, XElement counters, string project, string framework)
{
    var expectedAssembly = project + ".dll";
    var testMethods = document.Descendants()
        .Where(element => element.Name.LocalName == "TestMethod")
        .ToArray();
    if (testMethods.Length == 0)
        return false;

    foreach (var testMethod in testMethods)
    {
        var codeBase = (string)testMethod.Attribute("codeBase") ?? string.Empty;
        var normalizedCodeBase = codeBase.Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedCodeBase))
            return false;
        var assemblyName = normalizedCodeBase[(normalizedCodeBase.LastIndexOf('/') + 1)..];
        if (!string.Equals(assemblyName, expectedAssembly, StringComparison.OrdinalIgnoreCase))
            return false;
        var pathSegments = normalizedCodeBase.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!pathSegments.Any(segment => string.Equals(segment, framework, StringComparison.OrdinalIgnoreCase)))
            return false;
    }

    return TryReadCounter(counters, "total", out var total) && total >= 0 &&
        TryReadCounter(counters, "executed", out var executed) && executed >= 0 &&
        TryReadCounter(counters, "passed", out var passed) && passed >= 0 &&
        TryReadCounter(counters, "failed", out var failed) && failed >= 0 &&
        TryReadCounter(counters, "notExecuted", out var notExecuted) && notExecuted >= 0 &&
        total == executed + notExecuted && executed == passed + failed;
}

static bool TryReadCounter(XElement counters, string name, out int value) =>
    int.TryParse((string)counters.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

static IReadOnlySet<string> GetExpectedUnitEntries() => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Bing.Core.Tests|net6.0", "Bing.Core.Tests|net8.0",
    "Bing.Dapper.Core.Tests|net6.0", "Bing.Dapper.Core.Tests|net8.0",
    "Bing.Dapper.MySql.Tests|net6.0", "Bing.Dapper.MySql.Tests|net8.0",
    "Bing.Dapper.PostgreSql.Tests|net6.0", "Bing.Dapper.PostgreSql.Tests|net8.0",
    "Bing.Dapper.SqlServer.Tests|net6.0", "Bing.Dapper.SqlServer.Tests|net8.0",
    "Bing.Dapper.Sqlite.Tests|net6.0", "Bing.Dapper.Sqlite.Tests|net8.0",
    "Bing.Dapper.Oracle.Tests|net6.0", "Bing.Dapper.Oracle.Tests|net8.0",
    "Bing.Data.Sql.Tests|net6.0", "Bing.Data.Sql.Tests|net8.0",
    "Bing.Data.Sql.CustomProvider.Tests|net6.0", "Bing.Data.Sql.CustomProvider.Tests|net8.0",
    "Bing.Data.Sql.Analyzers.Tests|net8.0",
    "Bing.Test.Shared|net6.0", "Bing.Test.Shared|net8.0"
};

static object CreateAggregateCapabilityMatrix(IReadOnlyList<IntegrationRun> runs,
    IReadOnlyCollection<UnitRun> unitRuns, bool unitEvidenceComplete, bool formalHostComplete, bool rs0026GatePassed,
    bool apiGatePassed, string evidenceSessionId)
{
    var entries = ProviderCapabilityCatalog.GetDefinitions().Select(definition =>
    {
        var providerRuns = runs.Where(run => string.Equals(run.Provider, definition.Provider,
            StringComparison.OrdinalIgnoreCase)).ToArray();
        var matchingRuns = providerRuns.Where(run => !string.IsNullOrWhiteSpace(definition.TestMethod) &&
            run.PassedTestMethods.Any(method => method.EndsWith(definition.TestMethod, StringComparison.Ordinal))).ToArray();
        var status = GetCapabilityStatus(definition, matchingRuns);
        return new
        {
            definition.Provider,
            definition.Capability,
            definition.Scenario,
            PlannedState = definition.State.ToString(),
            Status = status,
            definition.Evidence,
            definition.TestMethod,
            Runs = (matchingRuns.Length > 0 ? matchingRuns : providerRuns).Select(run => new
            {
                run.Provider,
                run.Framework,
                run.RunId,
                run.EvidenceSessionId,
                run.SourceIdentity,
                run.Discovered,
                run.Executed,
                run.Passed,
                run.Failed,
                run.Skipped,
                run.CoreSkipped,
                run.OptionalSkipped,
                run.SourceState,
                run.ProviderVersion,
                run.DatabaseVersion,
                run.DriverVersion,
                run.RuntimeVersion,
                run.OperatingSystem,
                run.TrxPath,
                run.SummaryPath,
                run.BinaryManifestPath,
                run.ExecutionStatus
            }).ToArray()
        };
    }).ToArray();
    var readinessRuns = runs.Select(run => new ProviderReleaseReadinessRun
    {
        Provider = run.Provider,
        Framework = run.Framework,
        EvidenceSessionId = run.EvidenceSessionId,
        SourceIdentity = run.SourceIdentity,
        ReleaseEvidenceValid = run.ReleaseEvidenceValid,
        Executed = run.Executed,
        Failed = run.Failed,
        CoreSkipped = run.CoreSkipped,
        PassedTestMethods = run.PassedTestMethods
    }).ToArray();
    var unitTestsPassed = unitEvidenceComplete && unitRuns.Count > 0 && unitRuns.All(run => run.Failed == 0 &&
        run.Passed > 0 && run.Skipped == 0);
    var releaseReady = ProviderReleaseReadiness.IsReady(readinessRuns, unitTestsPassed,
        formalHostComplete, rs0026GatePassed, apiGatePassed);
    return new
    {
        ReleaseReady = releaseReady,
        ReadinessChecks = new
        {
            CoreProviders = new[] { "MySql", "PostgreSql", "SqlServer", "SQLite" },
            RequiredFrameworks = new[] { "net6.0", "net8.0" },
            UnitEvidenceComplete = unitEvidenceComplete,
            UnitTestsPassed = unitTestsPassed,
            FormalHostComplete = formalHostComplete,
            Rs0026GatePassed = rs0026GatePassed,
            ApiGatePassed = apiGatePassed,
            EvidenceSessionId = evidenceSessionId,
            ProviderRunsValidated = readinessRuns.Count(run => run.ReleaseEvidenceValid),
            ProviderRunsRequired = 8,
            MissingProviderFrameworkRuns = ProviderReleaseReadiness.GetMissingRuns(readinessRuns)
        },
        GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
        Entries = entries
    };
}

static bool TryValidateReleaseEvidence(string workspaceRoot, string summaryPath, IntegrationRun run,
    string expectedEvidenceSessionId, string expectedSourceIdentity)
{
    if (!string.Equals(run.ExecutionStatus, "Executed", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(run.SourceState, "clean", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(run.BinaryManifestPath) ||
        string.IsNullOrWhiteSpace(expectedEvidenceSessionId) ||
        !string.Equals(run.EvidenceSessionId, expectedEvidenceSessionId, StringComparison.Ordinal) ||
        string.IsNullOrWhiteSpace(expectedSourceIdentity) ||
        !string.Equals(run.SourceIdentity, expectedSourceIdentity, StringComparison.Ordinal))
        return false;
    try
    {
        var summaryRelativePath = ToWorkspaceRelativePath(workspaceRoot, summaryPath);
        var runDirectory = Path.GetDirectoryName(summaryRelativePath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(runDirectory))
            return false;
        var matrixJsonRelativePath = $"{runDirectory}/provider-capability-matrix.json";
        var matrixMarkdownRelativePath = $"{runDirectory}/provider-capability-matrix.md";
        ResolveWorkspaceFile(workspaceRoot, matrixJsonRelativePath);
        ResolveWorkspaceFile(workspaceRoot, matrixMarkdownRelativePath);
        ProviderReleaseEvidenceValidator.ValidateArchived(new ProviderReleaseEvidenceRequest
        {
            WorkspaceRoot = workspaceRoot,
            Provider = run.Provider,
            Framework = run.Framework,
            RunId = run.RunId,
            EvidenceSessionId = run.EvidenceSessionId,
            TrxRelativePath = run.TrxPath,
            SummaryRelativePath = summaryRelativePath,
            MatrixJsonRelativePath = matrixJsonRelativePath,
            MatrixMarkdownRelativePath = matrixMarkdownRelativePath,
            BinaryManifestRelativePath = run.BinaryManifestPath,
            SourceIdentity = run.SourceIdentity,
            ProviderVersion = run.ProviderVersion,
            DatabaseVersion = run.DatabaseVersion,
            DriverVersion = run.DriverVersion,
            RuntimeVersion = run.RuntimeVersion,
            OperatingSystem = run.OperatingSystem,
            SourceState = run.SourceState,
            StartedAtUtc = GetUtcFromSummary(workspaceRoot, summaryPath, "StartedAtUtc"),
            CompletedAtUtc = run.CompletedAtUtc
        }, expectedEvidenceSessionId);
        return true;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or
        System.Xml.XmlException or JsonException)
    {
        return false;
    }
}

static DateTimeOffset GetUtcFromSummary(string workspaceRoot, string summaryPath, string name)
{
    using var document = JsonDocument.Parse(File.ReadAllText(summaryPath, Encoding.UTF8));
    return GetUtc(document.RootElement, name);
}

static bool ValidateFormalHostEvidence(string root, string relativeDirectory, string expectedEvidenceSessionId,
    string expectedSourceIdentity)
{
    if (string.IsNullOrWhiteSpace(relativeDirectory) || string.IsNullOrWhiteSpace(expectedEvidenceSessionId) ||
        string.IsNullOrWhiteSpace(expectedSourceIdentity))
        return false;
    try
    {
        var directory = ResolveWorkspaceDirectory(root, relativeDirectory, "artifacts/benchmarks");
        var manifestPath = Path.Combine(directory, "formal-host-complete.json");
        if (!File.Exists(manifestPath))
            return false;
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
        var manifest = document.RootElement;
        var expectedReportCounts = GetExpectedFormalHostReportCounts();
        var expectedBenchmarkCount = expectedReportCounts.Values.Sum();
        if (!HasValue(manifest, "Status", "Complete") || !HasValue(manifest, "Job", "FormalHost") ||
            !HasValue(manifest, "EvidenceSessionId", expectedEvidenceSessionId) ||
            !HasValue(manifest, "SourceIdentity", expectedSourceIdentity) ||
            !HasValue(manifest, "SourceState", "clean") ||
            GetInt(manifest, "LaunchCount") != 3 || GetInt(manifest, "WarmupCount") != 6 ||
            GetInt(manifest, "IterationCount") != 15 ||
            GetInt(manifest, "BenchmarkCount") != expectedBenchmarkCount ||
            GetInt(manifest, "ExpectedBenchmarkCount") != expectedBenchmarkCount ||
            !manifest.TryGetProperty("ReportCounts", out var reportCounts) ||
            reportCounts.ValueKind != JsonValueKind.Object ||
            !manifest.TryGetProperty("Reports", out var reports) || reports.ValueKind != JsonValueKind.Array ||
            !reports.EnumerateArray().Any() ||
            !manifest.TryGetProperty("Artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array)
            return false;

        if (reportCounts.EnumerateObject().Count() != expectedReportCounts.Count ||
            expectedReportCounts.Any(expected =>
                !reportCounts.TryGetProperty(expected.Key, out var count) ||
                count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var actual) ||
                actual != expected.Value))
            return false;

        var reportPaths = reports.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
            ? item.GetString()
            : string.Empty).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        var files = reportPaths.Select(path => ResolveWorkspaceFile(root, path)).ToArray();
        if (!ValidateArtifactHashes(root, artifacts, reportPaths))
            return false;
        var csv = files.Where(path => path.EndsWith("-report.csv", StringComparison.OrdinalIgnoreCase)).ToArray();
        var markdown = files.Any(path => path.EndsWith("-report-github.md", StringComparison.OrdinalIgnoreCase));
        var html = files.Any(path => path.EndsWith("-report.html", StringComparison.OrdinalIgnoreCase));
        var raw = files.Any(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
        if (csv.Length != expectedReportCounts.Count || !markdown || !html || !raw)
            return false;
        foreach (var expected in expectedReportCounts)
        {
            var path = csv.SingleOrDefault(candidate =>
                string.Equals(Path.GetFileName(candidate), expected.Key + "-report.csv",
                    StringComparison.OrdinalIgnoreCase));
            if (path == null || !ValidateFormalHostCsv(path, expected.Value))
                return false;
        }
        return true;
    }
    catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or
        FileNotFoundException or JsonException)
    {
        return false;
    }
}

static bool ValidateRs0026Evidence(string root, string relativePath, string expectedEvidenceSessionId,
    string expectedSourceIdentity)
{
    if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(expectedEvidenceSessionId) ||
        string.IsNullOrWhiteSpace(expectedSourceIdentity))
        return false;
    try
    {
        var path = ResolveWorkspaceFile(root, relativePath);
        using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var inventory = document.RootElement;
        if (!HasValue(inventory, "EvidenceSessionId", expectedEvidenceSessionId) ||
            !HasValue(inventory, "SourceIdentity", expectedSourceIdentity) ||
            !HasValue(inventory, "SourceState", "clean") ||
            !inventory.TryGetProperty("Entries", out var inventoryEntries) ||
            inventoryEntries.ValueKind != JsonValueKind.Array ||
            !inventory.TryGetProperty("Artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array)
            return false;
        var entries = inventoryEntries.EnumerateArray().ToArray();
        var expectedProjects = new[]
        {
            "Bing.Data.Sql", "Bing.Dapper.Core", "Bing.Dapper.MySql", "Bing.Dapper.PostgreSql",
            "Bing.Dapper.SqlServer", "Bing.Dapper.Sqlite", "Bing.Dapper.Oracle"
        };
        if (entries.Length != expectedProjects.Length ||
            !expectedProjects.All(project => entries.Any(entry => HasValue(entry, "Project", project))))
            return false;
        foreach (var entry in entries)
        {
            if (GetInt(entry, "ExitCode") != 0 || GetInt(entry, "RS0016") != 0 ||
                GetInt(entry, "RS0017") != 0 || GetInt(entry, "RS0018") != 0 ||
                GetInt(entry, "RS0026") != 0 ||
                GetInt(entry, "ErrorLines") != 0 || !HasString(entry, "Log"))
                return false;
            var logPath = ResolveWorkspaceFile(root, GetString(entry, "Log"));
            var logText = File.ReadAllText(logPath, Encoding.UTF8);
            if (logText.Contains("NoWarn", StringComparison.OrdinalIgnoreCase) ||
                logText.Contains("SuppressMessage", StringComparison.OrdinalIgnoreCase) ||
                logText.Contains("#pragma", StringComparison.OrdinalIgnoreCase))
                return false;
        }
            return ValidateArtifactHashes(root, artifacts, entries.Select(entry => GetString(entry, "Log")));
    }
    catch (Exception exception) when (exception is ArgumentException or FileNotFoundException or JsonException)
    {
        return false;
    }
}

static bool ValidateApiEvidence(string root, string relativePath, string expectedEvidenceSessionId,
    string expectedSourceIdentity)
{
    if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(expectedEvidenceSessionId) ||
        string.IsNullOrWhiteSpace(expectedSourceIdentity))
        return false;
    try
    {
        var path = ResolveWorkspaceFile(root, relativePath);
        using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var evidence = document.RootElement;
         return HasValue(evidence, "EvidenceSessionId", expectedEvidenceSessionId) &&
             HasValue(evidence, "SourceIdentity", expectedSourceIdentity) &&
             HasValue(evidence, "SourceState", "clean") &&
             evidence.TryGetProperty("Artifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array &&
             HasValue(evidence, "Status", "PASS") &&
               HasValue(evidence, "PublicApiAnalyzer", "PASS") &&
             HasValue(evidence, "ReflectionContract", "PASS") &&
             ValidateArtifactHashes(root, artifacts, artifacts.EnumerateArray().Select(item => GetString(item, "Path")));
    }
    catch (Exception exception) when (exception is ArgumentException or FileNotFoundException or JsonException)
    {
        return false;
    }
}

static bool HasValue(JsonElement root, string name, string expected) =>
    root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
    string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase);

static string GetCapabilityStatus(ProviderCapabilityScenarioDefinition definition,
    IReadOnlyCollection<IntegrationRun> matchingRuns)
{
    if (definition.State == ProviderCapabilityEvidenceState.Unsupported)
        return "UNSUPPORTED";
    if (definition.State == ProviderCapabilityEvidenceState.ImplementationGap)
        return "BLOCKED";
    if (matchingRuns.Count == 0)
        return definition.State == ProviderCapabilityEvidenceState.Declared ? "NOT VERIFIED" : "NOT VERIFIED";
    if (matchingRuns.Any(run => run.Failed > 0 || run.CoreSkipped > 0))
        return "FAILED";
    return matchingRuns.Any(run => string.Equals(run.SourceState, "clean", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(run.ExecutionStatus, "Executed", StringComparison.OrdinalIgnoreCase) &&
                       run.ReleaseEvidenceValid)
        ? "PASS"
        : "NOT VERIFIED";
}

static string CreateCapabilityMarkdown(dynamic matrix)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Provider Capability Matrix");
    builder.AppendLine();
    builder.AppendLine("> 当前报告只聚合现有 summary/TRX 和 SQLite TestGenerated 制品；dirty source 或 TestGenerated 不构成 Release Evidence。");
    builder.AppendLine();
    builder.AppendLine("| Provider | Capability | Scenario | Planned State | Status | Test Method | Runs |");
    builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
    foreach (var entry in matrix.Entries)
    {
        var runs = string.Join("<br>", ((IEnumerable<dynamic>)entry.Runs).Select(run =>
            $"{run.Framework}: {run.RunId}"));
        builder.AppendLine($"| {EscapeMarkdown(entry.Provider)} | {EscapeMarkdown(entry.Capability)} | " +
                           $"{EscapeMarkdown(entry.Scenario)} | {EscapeMarkdown(entry.PlannedState)} | " +
                           $"{EscapeMarkdown(entry.Status)} | {EscapeMarkdown(entry.TestMethod ?? string.Empty)} | " +
                           $"{EscapeMarkdown(runs)} |");
    }
    return builder.ToString();
}

static string CreateUnitMarkdown(IReadOnlyList<UnitRun> runs)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Unit Test Report");
    builder.AppendLine();
    builder.AppendLine("| Project | TFM | Discovered | Executed | Passed | Failed | Skipped | Duration | TRX |");
    builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- |");
    foreach (var run in runs)
        builder.AppendLine($"| {EscapeMarkdown(run.Project)} | {EscapeMarkdown(run.Framework)} | {run.Discovered} | " +
                           $"{run.Executed} | {run.Passed} | {run.Failed} | {run.Skipped} | " +
                           $"{EscapeMarkdown(run.Duration)} | {EscapeMarkdown(run.TrxPath)} |");
    if (runs.Count == 0)
        builder.AppendLine("| none | | 0 | 0 | 0 | 0 | 0 | | 未提供 unit TRX |");
    return builder.ToString();
}

static string CreateIntegrationMarkdown(IReadOnlyList<IntegrationRun> runs)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Integration Test Report");
    builder.AppendLine();
    builder.AppendLine("> 保留全部 Provider/TFM 输入（不按“最新”折叠重复项）；Provider/TFM 唯一性由 Release Readiness 门禁校验，Release Evidence 仍要求 clean source 和受保护验证。");
    builder.AppendLine();
    builder.AppendLine("| Provider | TFM | Database | Provider Version | Database Version | Driver Version | Discovered | Executed | Passed | Failed | Core Skip | Optional Skip | Source | Status | RunId | TRX |");
    builder.AppendLine("| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | --- |");
    foreach (var run in runs)
    {
        var status = run.Failed > 0 || run.CoreSkipped > 0 ? "FAILED" :
            run.ReleaseEvidenceValid &&
            string.Equals(run.ExecutionStatus, "Executed", StringComparison.OrdinalIgnoreCase) &&
            run.Executed > 0 &&
            run.Failed == 0 &&
            run.CoreSkipped == 0 &&
            string.Equals(run.SourceState, "clean", StringComparison.OrdinalIgnoreCase)
                ? "PASS"
                : "NOT VERIFIED";
        builder.AppendLine($"| {EscapeMarkdown(run.Provider)} | {EscapeMarkdown(run.Framework)} | " +
                           $"{EscapeMarkdown(run.DatabaseName)} | {EscapeMarkdown(run.ProviderVersion)} | " +
                           $"{EscapeMarkdown(run.DatabaseVersion)} | {EscapeMarkdown(run.DriverVersion)} | " +
                           $"{run.Discovered} | {run.Executed} | {run.Passed} | {run.Failed} | {run.CoreSkipped} | " +
                           $"{run.OptionalSkipped} | {EscapeMarkdown(run.SourceState)} | {status} | " +
                           $"{EscapeMarkdown(run.RunId)} | {EscapeMarkdown(run.TrxPath)} |");
    }
    if (runs.Count == 0)
        builder.AppendLine("| none | | | | | | 0 | 0 | 0 | 0 | 0 | 0 | | NOT VERIFIED | | |");
    return builder.ToString();
}

static HashSet<string> LoadPassedTestMethods(string trxPath)
{
    var document = XDocument.Load(trxPath, LoadOptions.None);
    return document.Descendants()
        .Where(element => element.Name.LocalName == "UnitTestResult" &&
                          string.Equals((string)element.Attribute("outcome"), "Passed",
                              StringComparison.OrdinalIgnoreCase))
        .Select(element => (string)element.Attribute("testName"))
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .ToHashSet(StringComparer.Ordinal);
}

static int ReadTrxCounter(string path, string name)
{
    var document = XDocument.Load(path, LoadOptions.None);
    var counters = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Counters");
    return counters == null ? 0 : ReadCounter(counters, name);
}

static int ReadCounter(XElement counters, string name) =>
    int.TryParse((string)counters.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        ? value
        : 0;

static string ResolveWorkspaceFile(string workspaceRoot, string relativePath)
{
    var normalized = RequireRelativeArtifactPath(relativePath, "artifacts/");
    var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var path = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
    if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        !File.Exists(path))
        throw new FileNotFoundException("TRX 制品不存在或不在工作区内。", normalized);
    return path;
}

static string ToWorkspaceRelativePath(string workspaceRoot, string path)
{
    var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
        Path.DirectorySeparatorChar;
    var fullPath = Path.GetFullPath(path);
    if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("制品路径必须位于工作区内。", nameof(path));
    return fullPath[root.Length..].Replace('\\', '/');
}

static bool HasString(JsonElement root, string name) =>
    root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
    !string.IsNullOrWhiteSpace(value.GetString());

static string GetString(JsonElement root, string name) =>
    root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? string.Empty
        : string.Empty;

static int GetInt(JsonElement root, string name) =>
    root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;

static DateTimeOffset GetUtc(JsonElement root, string name) =>
    DateTimeOffset.TryParse(GetString(root, name), CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind, out var result) ? result.ToUniversalTime() : DateTimeOffset.MinValue;

static void WriteUtf8(string path, string content)
{
    if (content.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
        content.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
        content.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
        content.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("聚合报告包含连接信息。");
    File.WriteAllText(path, content, new UTF8Encoding(false));
}

static string EscapeMarkdown(string value) =>
    (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

static bool ValidateArtifactHashes(string root, JsonElement artifacts, IEnumerable<string> requiredPaths)
{
    var expectedPaths = requiredPaths.Where(path => !string.IsNullOrWhiteSpace(path))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var actualPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var artifact in artifacts.EnumerateArray())
    {
        if (!artifact.TryGetProperty("Path", out var pathValue) || pathValue.ValueKind != JsonValueKind.String ||
            !artifact.TryGetProperty("Sha256", out var hashValue) || hashValue.ValueKind != JsonValueKind.String)
            return false;
        var relativePath = pathValue.GetString();
        var expectedHash = hashValue.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(expectedHash) ||
            !Regex.IsMatch(expectedHash, "^[A-Fa-f0-9]{64}$") || !actualPaths.Add(relativePath))
            return false;
        var path = ResolveWorkspaceFile(root, relativePath);
        using var stream = File.OpenRead(path);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var actualHash = Convert.ToHexString(sha256.ComputeHash(stream));
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            return false;
    }
    return expectedPaths.Count > 0 && expectedPaths.All(actualPaths.Contains);
}

static IReadOnlyDictionary<string, int> GetExpectedFormalHostReportCounts() =>
    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Bing.Data.Sql.Benchmarks.SqlAggregateRenderingBenchmarks"] = 19,
        ["Bing.Data.Sql.Benchmarks.SqlBuilderAppendToBenchmarks"] = 6,
        ["Bing.Data.Sql.Benchmarks.SqlBuilderCteAndParameterTokenBenchmarks"] = 2,
        ["Bing.Data.Sql.Benchmarks.SqlDebugSqlBenchmarks"] = 3,
        ["Bing.Data.Sql.Benchmarks.SqliteDapperE2EBenchmarks"] = 24,
        ["Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks"] = 28,
        ["Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks"] = 12,
        ["Bing.Data.Sql.Benchmarks.SqlMetadataBenchmarks"] = 27,
        ["Bing.Data.Sql.Benchmarks.SqlMutationBenchmarks"] = 15
    };

static bool ValidateFormalHostCsv(string path, int expectedRows)
{
    var lines = File.ReadAllLines(path, Encoding.UTF8).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
    if (lines.Length != expectedRows + 1)
        return false;
    var header = ParseCsvRecord(lines[0]);
    var jobIndex = Array.FindIndex(header, value => string.Equals(value, "Job", StringComparison.Ordinal));
    var launchIndex = Array.FindIndex(header, value => string.Equals(value, "LaunchCount", StringComparison.Ordinal));
    var warmupIndex = Array.FindIndex(header, value => string.Equals(value, "WarmupCount", StringComparison.Ordinal));
    var iterationIndex = Array.FindIndex(header, value => string.Equals(value, "IterationCount", StringComparison.Ordinal));
    if (jobIndex < 0 || launchIndex < 0 || warmupIndex < 0 || iterationIndex < 0)
        return false;
    foreach (var line in lines.Skip(1))
    {
        var fields = ParseCsvRecord(line);
        var maxIndex = Math.Max(Math.Max(jobIndex, launchIndex), Math.Max(warmupIndex, iterationIndex));
        if (fields.Length <= maxIndex || !string.Equals(fields[jobIndex], "FormalHost", StringComparison.Ordinal) ||
            !int.TryParse(fields[launchIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var launch) ||
            !int.TryParse(fields[warmupIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var warmup) ||
            !int.TryParse(fields[iterationIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iteration) ||
            launch != 3 || warmup != 6 || iteration != 15)
            return false;
    }
    return true;
}

static string[] ParseCsvRecord(string line)
{
    var values = new List<string>();
    var value = new StringBuilder();
    var quoted = false;
    for (var index = 0; index < line.Length; index++)
    {
        var character = line[index];
        if (quoted)
        {
            if (character == '"' && index + 1 < line.Length && line[index + 1] == '"')
            {
                value.Append('"');
                index++;
            }
            else if (character == '"')
            {
                quoted = false;
            }
            else
            {
                value.Append(character);
            }
        }
        else if (character == '"')
        {
            quoted = true;
        }
        else if (character == ',')
        {
            values.Add(value.ToString());
            value.Clear();
        }
        else
        {
            value.Append(character);
        }
    }
    if (quoted)
        return Array.Empty<string>();
    values.Add(value.ToString());
    return values.ToArray();
}

sealed class IntegrationRun
{
    public string Provider { get; set; }
    public string Framework { get; set; }
    public string RunId { get; set; }
    public string EvidenceSessionId { get; set; }
    public string SummaryPath { get; set; }
    public string TrxPath { get; set; }
    public string BinaryManifestPath { get; set; }
    public string SourceState { get; set; }
    public string SourceIdentity { get; set; }
    public string ProviderVersion { get; set; }
    public string DatabaseVersion { get; set; }
    public string DriverVersion { get; set; }
    public string RuntimeVersion { get; set; }
    public string OperatingSystem { get; set; }
    public string DatabaseName { get; set; }
    public string ConfigurationSource { get; set; }
    public string ExecutionStatus { get; set; }
    public int Discovered { get; set; }
    public int Executed { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int CoreSkipped { get; set; }
    public int OptionalSkipped { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public bool ReleaseEvidenceValid { get; set; }
    public HashSet<string> PassedTestMethods { get; set; } = new(StringComparer.Ordinal);
}

sealed class UnitRun
{
    public string Project { get; set; }
    public string Framework { get; set; }
    public string TrxPath { get; set; }
    public int Discovered { get; set; }
    public int Executed { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public string Duration { get; set; }
    public bool AssemblyIdentityValid { get; set; }
}
