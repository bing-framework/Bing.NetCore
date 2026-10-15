[CmdletBinding()]
param(
    [ValidateSet("net6.0")]
    [string]$Framework = "net6.0",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$ResultsDirectory = "artifacts/admin-mysqlcap-results",

    [switch]$ValidateOnly
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
# 两套 ORM 必须同时覆盖整单库存 7 项、库存跨进程 4 项、真实死锁 2 项、提交确认丢失及出箱恢复 3 项、消费取消 2 项和有序 CAP 3 项；仅达到总数不能替代逐项验收。
$projects = @(
    @{
        Name = "EFCore"
        MinimumTests = 34
        Filter = "FullyQualifiedName~AdminMySqlCapIntegrationTest|FullyQualifiedName~OrderedEventCapIntegrationTest"
        RequiredTestMethods = @(
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenRepeated_ShouldCommitInventoryAuditAndOutboxOnce"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenScopesCompeteForSameOrder_ShouldCommitExactlyOnce"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenDifferentOrdersCompete_ShouldKeepNonNegativeAtomicStock"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenLaterSkuIsInsufficient_ShouldRememberResultWithoutPartialDeduction"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCriticalAuditFails_ShouldRollbackAllBusinessWrites"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCancelledAfterOutbox_ShouldRollbackAllWrites"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenProcessesCompeteForSameOrder_ShouldCommitExactlyOnce"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenProcessesCompeteForDifferentOrders_ShouldKeepStockAtomic"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenProcessDiesBeforeCommit_ShouldRollbackAndRetryOriginalOrder"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenProcessDiesAfterCommit_ShouldRecoverOriginalResultWithoutDuplicateWrites"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenInnoDbDeadlocks_ShouldAwaitCleanupAndRetryFreshScope"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenInnoDbDeadlocksPersist_ShouldStopAfterFourCleanAttempts"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOriginalCommittedResult"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldPreserveRecoveryAndCleanupFailures"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOutboxThroughBackgroundScan"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::Consumption_WhenCancelledBeforeBusinessCommit_ShouldRetryAndCommitOnce"
            "Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest::Consumption_WhenCancelledAfterBusinessCommit_ShouldRetryWithoutDuplicateWrites"
            "Bing.Admin.EFCore.Tests.OrderedEventCapIntegrationTest::PublishAsync_WhenCommitted_ShouldReceiveAndProcessOnceInOrder"
            "Bing.Admin.EFCore.Tests.OrderedEventCapIntegrationTest::PublishAsync_WhenRolledBack_ShouldRetryFromFirstSequence"
            "Bing.Admin.EFCore.Tests.OrderedEventCapIntegrationTest::ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence"
        )
        Path = Join-Path $repositoryRoot "modules\admin\tests\Bing.Admin.EFCore.Tests\Bing.Admin.EFCore.Tests.csproj"
    }
    @{
        Name = "FreeSQL"
        MinimumTests = 33
        Filter = "FullyQualifiedName~AdminMySqlCapIntegrationTest|FullyQualifiedName~OrderedEventCapIntegrationTest"
        RequiredTestMethods = @(
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenRepeated_ShouldCommitInventoryAuditAndOutboxOnce"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenScopesCompeteForSameOrder_ShouldCommitExactlyOnce"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenDifferentOrdersCompete_ShouldKeepNonNegativeAtomicStock"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenLaterSkuIsInsufficient_ShouldRememberResultWithoutPartialDeduction"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCriticalAuditFails_ShouldRollbackAllBusinessWrites"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCancelledAfterOutbox_ShouldRollbackAllWrites"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenProcessesCompeteForSameOrder_ShouldCommitExactlyOnce"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenProcessesCompeteForDifferentOrders_ShouldKeepStockAtomic"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenProcessDiesBeforeCommit_ShouldRollbackAndRetryOriginalOrder"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenProcessDiesAfterCommit_ShouldRecoverOriginalResultWithoutDuplicateWrites"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenInnoDbDeadlocks_ShouldAwaitCleanupAndRetryFreshScope"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenInnoDbDeadlocksPersist_ShouldStopAfterFourCleanAttempts"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOriginalCommittedResult"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldPreserveRecoveryAndCleanupFailures"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOutboxThroughBackgroundScan"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::Consumption_WhenCancelledBeforeBusinessCommit_ShouldRetryAndCommitOnce"
            "Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest::Consumption_WhenCancelledAfterBusinessCommit_ShouldRetryWithoutDuplicateWrites"
            "Bing.Admin.FreeSQL.Tests.OrderedEventCapIntegrationTest::PublishAsync_WhenCommitted_ShouldReceiveAndProcessOnceInOrder"
            "Bing.Admin.FreeSQL.Tests.OrderedEventCapIntegrationTest::PublishAsync_WhenRolledBack_ShouldRetryFromFirstSequence"
            "Bing.Admin.FreeSQL.Tests.OrderedEventCapIntegrationTest::ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence"
        )
        Path = Join-Path $repositoryRoot "modules\admin\tests\Bing.Admin.FreeSQL.Tests\Bing.Admin.FreeSQL.Tests.csproj"
    }
)

function Get-EnvironmentValue {
    param([string]$Name)

    return [Environment]::GetEnvironmentVariable($Name)
}

function Test-Enabled {
    param([string]$Name)

    return [string]::Equals((Get-EnvironmentValue $Name), "true", [StringComparison]::OrdinalIgnoreCase)
}

function Get-ConnectionValue {
    param(
        [string]$ConnectionString,
        [string[]]$Names
    )

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    try {
        # PowerShell 的 IDictionary 适配器会把 ConnectionString 解析成键名；显式调用 setter 才能使用 .NET 解析器。
        $builder.set_ConnectionString($ConnectionString)
    }
    catch {
        throw "MySQL 连接字符串无法解析。"
    }
    foreach ($name in $Names) {
        if ($builder.ContainsKey($name) -and -not [string]::IsNullOrWhiteSpace([string]$builder[$name])) {
            return ([string]$builder[$name]).Trim()
        }
    }
    return $null
}

function Test-SafeDatabaseName {
    param([string]$DatabaseName)

    if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
        return $false
    }
    $systemNames = @(
        "information_schema", "master", "model", "msdb", "mysql", "performance_schema",
        "postgres", "sys", "tempdb", "template0", "template1"
    )
    $normalized = $DatabaseName.Trim()
    if ($systemNames -contains $normalized.ToLowerInvariant()) {
        return $false
    }
    $tokens = $normalized -split "[_-]"
    if (@($tokens | Where-Object { $_ -in @("prod", "production", "development") }).Count -gt 0) {
        return $false
    }
    return $normalized.EndsWith("_test", [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.EndsWith("_tests", [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.EndsWith("_integration", [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.EndsWith("_integration_test", [StringComparison]::OrdinalIgnoreCase)
}

function Test-TcpEndpoint {
    param(
        [string]$HostName,
        [int]$Port
    )

    if ([string]::IsNullOrWhiteSpace($HostName) -or $Port -lt 1 -or $Port -gt 65535) {
        return $false
    }
    try {
        return [bool](Test-NetConnection -ComputerName $HostName -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue)
    }
    catch {
        return $false
    }
}

function Resolve-ResultsDirectory {
    $normalized = $ResultsDirectory.Replace("/", "\").Trim("\")
    if ([string]::IsNullOrWhiteSpace($normalized) -or
        [System.IO.Path]::IsPathRooted($normalized) -or
        $normalized -notmatch "^artifacts[\\/]admin-mysqlcap-results(?:[\\/].*)?$") {
        throw "ResultsDirectory 必须位于工作区内的 artifacts/admin-mysqlcap-results 目录。"
    }
    $path = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $normalized))
    $allowed = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\admin-mysqlcap-results"))
    $allowedPrefix = $allowed.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $path.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        -not $path.Equals($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "ResultsDirectory 必须位于工作区内的 artifacts/admin-mysqlcap-results 目录。"
    }
    return $path
}

function New-Diagnostic {
    param(
        [string]$Status,
        [string]$Reason,
        [string]$Phase,
        [string]$DatabaseName,
        [bool]$ConnectionConfigured,
        [bool]$MySqlReachable,
        [bool]$RabbitReachable,
        [string]$TrxPath = $null,
        [string[]]$TrxPaths = @(),
        [string]$FailedProject = $null,
        [int]$TestExitCode = 0,
        [int]$ExitCode = 0,
        [DateTimeOffset]$StartedAtUtc = ([DateTimeOffset]::UtcNow)
    )

    return [ordered]@{
        Scenario = "AdminMySqlCap"
        Framework = $Framework
        Configuration = $Configuration
        ExecutionStatus = $Status
        BlockedReason = $Reason
        FailurePhase = $Phase
        DatabaseName = $DatabaseName
        ConnectionConfigured = $ConnectionConfigured
        MySqlReachable = $MySqlReachable
        RabbitReachable = $RabbitReachable
        RabbitHostConfigured = -not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue "CAP_RABBITMQ_HOST")
        )
        RabbitCredentialsConfigured =
            -not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue "CAP_RABBITMQ_USERNAME")) -and
            -not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue "CAP_RABBITMQ_PASSWORD"))
        TrxPath = $TrxPath
        TrxPaths = @($TrxPaths)
        FailedProject = $FailedProject
        TestExitCode = $TestExitCode
        ExitCode = $ExitCode
        StartedAtUtc = $StartedAtUtc.ToString("O")
        CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        SourceState = "dirty-or-unknown"
    }
}

function Write-Diagnostic {
    param(
        [object]$Diagnostic,
        [string]$RunDirectory
    )

    New-Item -ItemType Directory -Path $RunDirectory -Force | Out-Null
    $diagnosticPath = Join-Path $RunDirectory "admin-mysqlcap-diagnostic.json"
    ($Diagnostic | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $diagnosticPath -Encoding utf8
    Write-Host ("Admin MySQL/CAP 诊断：状态={0}; 阶段={1}; 原因={2}; 文件={3}" -f
        $Diagnostic.ExecutionStatus, $Diagnostic.FailurePhase, $Diagnostic.BlockedReason,
        $diagnosticPath)
    return $diagnosticPath
}

function Get-Preflight {
    $connectionString = Get-EnvironmentValue "ConnectionStrings__MySqlConnection"
    $databaseName = $null
    $mysqlHost = $null
    $mysqlPort = 3306
    $errors = [System.Collections.Generic.List[string]]::new()

    if (-not (Test-Enabled "RUN_MYSQL_INTEGRATION_TESTS")) {
        $errors.Add("RUN_MYSQL_INTEGRATION_TESTS=true 未设置。")
    }
    if (-not (Test-Enabled "RUN_MYSQL_CAP_INTEGRATION_TESTS")) {
        $errors.Add("RUN_MYSQL_CAP_INTEGRATION_TESTS=true 未设置。")
    }
    if (Test-Enabled "RUN_INTEGRATION_TESTS") {
        $errors.Add("RUN_INTEGRATION_TESTS 不得启用受控 MySQL/CAP 流程，请只启用 Provider 专用门控。")
    }
    foreach ($otherProvider in @("RUN_POSTGRESQL_INTEGRATION_TESTS", "RUN_SQLSERVER_INTEGRATION_TESTS")) {
        if (Test-Enabled $otherProvider) {
            $errors.Add("$otherProvider 不得与 MySQL/CAP 流程同时启用。")
        }
    }
    if (-not (Test-Enabled "ALLOW_DATABASE_RESET_FOR_TESTS")) {
        $errors.Add("ALLOW_DATABASE_RESET_FOR_TESTS=true 未设置。")
    }
    foreach ($project in $projects) {
        if (-not (Test-Path -LiteralPath $project.Path -PathType Leaf)) {
            $errors.Add("Admin $($project.Name) 测试项目不存在。")
        }
    }
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        $errors.Add("ConnectionStrings__MySqlConnection 未设置。")
    }
    else {
        try {
            $databaseName = Get-ConnectionValue $connectionString @("Database", "Initial Catalog")
            $mysqlHost = Get-ConnectionValue $connectionString @("Server", "Host", "Data Source")
            $portValue = Get-ConnectionValue $connectionString @("Port")
            if (-not [string]::IsNullOrWhiteSpace($portValue)) {
                $mysqlPort = 0
                if (-not [int]::TryParse($portValue, [ref]$mysqlPort) -or
                    $mysqlPort -lt 1 -or $mysqlPort -gt 65535) {
                    $errors.Add("MySQL 端口必须是 1 到 65535 之间的整数。")
                    $mysqlPort = 0
                }
            }
            if (-not (Test-SafeDatabaseName $databaseName)) {
                $errors.Add("MySQL 数据库必须是专用测试库，名称需以 _test、_tests 或 _integration 结尾。")
            }
            if ([string]::IsNullOrWhiteSpace($mysqlHost)) {
                $errors.Add("MySQL 连接字符串缺少服务器地址。")
            }
        }
        catch {
            $errors.Add($_.Exception.Message)
        }
    }

    foreach ($name in @("CAP_RABBITMQ_HOST", "CAP_RABBITMQ_USERNAME", "CAP_RABBITMQ_PASSWORD")) {
        if ([string]::IsNullOrWhiteSpace((Get-EnvironmentValue $name))) {
            $errors.Add("$name 未设置。")
        }
    }

    $rabbitHost = Get-EnvironmentValue "CAP_RABBITMQ_HOST"
    $rabbitPort = 5672
    $rabbitPortValue = Get-EnvironmentValue "CAP_RABBITMQ_PORT"
    if (-not [string]::IsNullOrWhiteSpace($rabbitPortValue)) {
        $rabbitPort = 0
        if (-not [int]::TryParse($rabbitPortValue, [ref]$rabbitPort) -or
            $rabbitPort -lt 1 -or $rabbitPort -gt 65535) {
            $errors.Add("RabbitMQ 端口必须是 1 到 65535 之间的整数。")
            $rabbitPort = 0
        }
    }

    $canProbe = $errors.Count -eq 0
    $mysqlReachable = if ($canProbe -and -not [string]::IsNullOrWhiteSpace($mysqlHost)) {
        Test-TcpEndpoint $mysqlHost $mysqlPort
    }
    else {
        $false
    }
    if ($canProbe -and -not $mysqlReachable) {
        $errors.Add("MySQL 端点不可连接。")
    }
    $rabbitReachable = if ($canProbe -and -not [string]::IsNullOrWhiteSpace($rabbitHost)) {
        Test-TcpEndpoint $rabbitHost $rabbitPort
    }
    else {
        $false
    }
    if ($canProbe -and -not $rabbitReachable) {
        $errors.Add("RabbitMQ 端点不可连接。")
    }

    return [pscustomobject]@{
        Errors = @($errors)
        DatabaseName = $databaseName
        ConnectionConfigured = -not [string]::IsNullOrWhiteSpace($connectionString)
        MySqlReachable = $mysqlReachable
        RabbitReachable = $rabbitReachable
    }
}

# 检查当前 Admin 集成测试类达到项目门槛且全数通过。
function Test-TrxCounters {
    param(
        [object]$Counters,
        [int]$MinimumTests
    )

    if ($null -eq $Counters -or $MinimumTests -lt 1) {
        return $false
    }
    $total = [int]$Counters.total
    $passed = [int]$Counters.passed
    return $total -ge $MinimumTests -and $passed -eq $total -and
        [int]$Counters.failed -eq 0 -and [int]$Counters.notExecuted -eq 0
}

# 检查 TRX 是否包含并通过项目声明的每个关键测试方法。
function Test-TrxRequiredMethods {
    param(
        [object]$Trx,
        [string[]]$RequiredTestMethods
    )

    if ($null -eq $Trx -or @($RequiredTestMethods).Count -eq 0) {
        return $false
    }

    $testIdsByName = @{}
    foreach ($unitTest in @($Trx.TestRun.TestDefinitions.UnitTest)) {
        $method = $unitTest.TestMethod
        if ($null -eq $method) {
            continue
        }
        $methodKey = "{0}::{1}" -f ([string]$method.className), ([string]$method.name)
        $testIdsByName[$methodKey] = [string]$unitTest.id
    }

    $outcomesById = @{}
    foreach ($result in @($Trx.TestRun.Results.UnitTestResult)) {
        $outcomesById[[string]$result.testId] = [string]$result.outcome
    }

    foreach ($requiredMethod in $RequiredTestMethods) {
        if ([string]::IsNullOrWhiteSpace($requiredMethod) -or
            -not $testIdsByName.ContainsKey($requiredMethod)) {
            return $false
        }
        $testId = $testIdsByName[$requiredMethod]
        if (-not $outcomesById.ContainsKey($testId) -or $outcomesById[$testId] -ne "Passed") {
            return $false
        }
    }

    return $true
}

# 收集本次运行已经生成的项目 TRX 文件。
function Get-ExistingTrxPaths {
    param(
        [string]$RunDirectory,
        [object[]]$Projects
    )

    foreach ($project in $Projects) {
        $path = Join-Path $RunDirectory ("admin-$($project.Name.ToLowerInvariant())-mysqlcap.trx")
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $path
        }
    }
}

# 测试失败时保留已经完成的预检与项目结果。
function New-TestFailureDiagnostic {
    param(
        [object]$Preflight,
        [string]$Reason,
        [string]$FailedProject,
        [int]$TestExitCode,
        [string[]]$TrxPaths,
        [DateTimeOffset]$StartedAtUtc
    )

    $existingPaths = @($TrxPaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $lastTrx = if ($existingPaths.Count -gt 0) { $existingPaths[-1] } else { $null }
    $runnerExitCode = if ($TestExitCode -ne 0) { $TestExitCode } else { 1 }
    return New-Diagnostic -Status "Failed" -Reason $Reason -Phase "Runner" `
        -DatabaseName $Preflight.DatabaseName -ConnectionConfigured ([bool]$Preflight.ConnectionConfigured) `
        -MySqlReachable ([bool]$Preflight.MySqlReachable) -RabbitReachable ([bool]$Preflight.RabbitReachable) `
        -TrxPath $lastTrx -TrxPaths $existingPaths -FailedProject $FailedProject `
        -TestExitCode $TestExitCode -ExitCode $runnerExitCode -StartedAtUtc $StartedAtUtc
}

$startedAtUtc = [DateTimeOffset]::UtcNow
$runDirectory = $null
$preflight = $null
$failedProject = $null
$testExitCode = 0
try {
    $resultsRoot = Resolve-ResultsDirectory
    $runDirectory = Join-Path $resultsRoot ("{0}-{1}" -f $Framework, $startedAtUtc.ToString("yyyyMMddTHHmmssfffZ"))
    $preflight = Get-Preflight
    if ($preflight.Errors.Count -gt 0) {
        $diagnostic = New-Diagnostic -Status "Blocked" -Reason ($preflight.Errors -join " ") -Phase "Preflight" `
            -DatabaseName $preflight.DatabaseName -ConnectionConfigured $preflight.ConnectionConfigured `
            -MySqlReachable $preflight.MySqlReachable -RabbitReachable $preflight.RabbitReachable `
            -ExitCode 2 -StartedAtUtc $startedAtUtc
        Write-Diagnostic $diagnostic $runDirectory | Out-Null
        exit 2
    }
    if ($ValidateOnly) {
        $diagnostic = New-Diagnostic -Status "PreflightPassed" -Reason $null -Phase "Preflight" `
            -DatabaseName $preflight.DatabaseName -ConnectionConfigured $preflight.ConnectionConfigured `
            -MySqlReachable $preflight.MySqlReachable -RabbitReachable $preflight.RabbitReachable `
            -ExitCode 0 -StartedAtUtc $startedAtUtc
        Write-Diagnostic $diagnostic $runDirectory | Out-Null
        exit 0
    }

    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    foreach ($project in $projects) {
        $failedProject = $project.Name
        $trxName = "admin-$($project.Name.ToLowerInvariant())-mysqlcap.trx"
        $trxPath = Join-Path $runDirectory $trxName
        $arguments = @(
            "test", $project.Path, "-f", $Framework, "-c", $Configuration, "--no-restore",
            "--filter", $project.Filter,
            "--logger", "trx;LogFileName=$trxName",
            "--results-directory", $runDirectory
        )
        & dotnet @arguments
        $testExitCode = $LASTEXITCODE
        if ($testExitCode -ne 0) {
            throw "Admin $($project.Name) dotnet test 返回退出码 $testExitCode。"
        }
        if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
            throw "未找到 Admin $($project.Name) MySQL/CAP 测试 TRX 文件。"
        }
        [xml]$trx = Get-Content -LiteralPath $trxPath -Raw -Encoding utf8
        $counters = $trx.TestRun.ResultSummary.Counters
        if (-not (Test-TrxCounters $counters $project.MinimumTests) -or
            -not (Test-TrxRequiredMethods $trx $project.RequiredTestMethods)) {
            throw "Admin $($project.Name) MySQL/CAP 测试未达到至少 $($project.MinimumTests) 项全通过且无跳过，并完整通过必需方法集合的验收条件。"
        }
    }
    $diagnostic = New-Diagnostic -Status "Passed" -Reason $null -Phase "Test" `
        -DatabaseName $preflight.DatabaseName -ConnectionConfigured $preflight.ConnectionConfigured `
        -MySqlReachable $preflight.MySqlReachable -RabbitReachable $preflight.RabbitReachable `
        -TrxPath $runDirectory -ExitCode 0 -StartedAtUtc $startedAtUtc
    Write-Diagnostic $diagnostic $runDirectory | Out-Null
    exit 0
}
catch {
    if ($null -eq $runDirectory) {
        throw
    }
    $message = $_.Exception.Message
    $existingTrxPaths = @(Get-ExistingTrxPaths -RunDirectory $runDirectory -Projects $projects)
    $diagnostic = New-TestFailureDiagnostic -Preflight $preflight -Reason $message `
        -FailedProject $failedProject -TestExitCode $testExitCode -TrxPaths $existingTrxPaths `
        -StartedAtUtc $startedAtUtc
    Write-Diagnostic $diagnostic $runDirectory | Out-Null
    throw
}
