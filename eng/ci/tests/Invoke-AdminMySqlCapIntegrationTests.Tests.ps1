[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$runner = Join-Path $repositoryRoot "eng\ci\Invoke-AdminMySqlCapIntegrationTests.ps1"
$resultsDirectory = "artifacts/admin-mysqlcap-results/preflight-tests/$([guid]::NewGuid().ToString('N'))"
$resultsPath = Join-Path $repositoryRoot $resultsDirectory
$names = @(
    "RUN_MYSQL_INTEGRATION_TESTS", "RUN_MYSQL_CAP_INTEGRATION_TESTS", "ALLOW_DATABASE_RESET_FOR_TESTS",
    "RUN_INTEGRATION_TESTS", "RUN_POSTGRESQL_INTEGRATION_TESTS", "RUN_SQLSERVER_INTEGRATION_TESTS",
    "ConnectionStrings__MySqlConnection", "CAP_RABBITMQ_HOST", "CAP_RABBITMQ_PORT",
    "CAP_RABBITMQ_USERNAME", "CAP_RABBITMQ_PASSWORD"
)
$original = @{}
foreach ($name in $names) {
    $original[$name] = [Environment]::GetEnvironmentVariable($name)
}

# 只加载项目声明和校验函数，避免单元断言触发外部数据库预检与测试运行。
$tokens = $null
$parseErrors = $null
$runnerAst = [System.Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw "Admin MySQL/CAP 验收脚本存在 PowerShell 语法错误。"
}
$projectDefinition = $runnerAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
        $node.Left.VariablePath.UserPath -eq "projects"
}, $false)
if ($null -eq $projectDefinition) {
    throw "未找到 Admin MySQL/CAP 项目验收声明。"
}
. ([scriptblock]::Create($projectDefinition.Extent.Text))
$countersDefinition = $runnerAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq "Test-TrxCounters"
}, $false)
if ($null -eq $countersDefinition) {
    throw "未找到 TRX 计数校验函数。"
}
. ([scriptblock]::Create($countersDefinition.Extent.Text))
foreach ($functionName in @("Get-EnvironmentValue", "New-Diagnostic", "Get-ExistingTrxPaths", "New-TestFailureDiagnostic", "Test-TrxRequiredMethods")) {
    $definition = $runnerAst.Find({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -eq $functionName
    }, $false)
    if ($null -eq $definition) {
        throw "未找到 $functionName 函数。"
    }
    . ([scriptblock]::Create($definition.Extent.Text))
}
# 固定两套 ORM 的验收范围，避免漏掉库存跨进程、真实死锁、提交确认丢失、消费取消和有序链路。
$runnerText = Get-Content -LiteralPath $runner -Raw -Encoding utf8
if ($runnerText -notmatch 'Name = "EFCore"' -or
    $runnerText -notmatch 'MinimumTests = 34' -or
    $runnerText -notmatch 'FullyQualifiedName~AdminMySqlCapIntegrationTest\|FullyQualifiedName~OrderedEventCapIntegrationTest' -or
    $runnerText -notmatch 'Name = "FreeSQL"' -or
    $runnerText -notmatch 'MinimumTests = 33' -or
    $runnerText -notmatch 'FullyQualifiedName~AdminMySqlCapIntegrationTest\|FullyQualifiedName~OrderedEventCapIntegrationTest' -or
    $runnerText -notmatch '"--filter", \$project.Filter') {
    throw "EFCore/FreeSQL 验收必须包含库存跨进程、真实死锁、提交确认丢失、有序 CAP 链路及对应门槛。"
}
$requiredMethodMarkers = @(
    "InventoryOrder_WhenRepeated_ShouldCommitInventoryAuditAndOutboxOnce",
    "InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated",
    "InventoryOrder_WhenScopesCompeteForSameOrder_ShouldCommitExactlyOnce",
    "InventoryOrder_WhenDifferentOrdersCompete_ShouldKeepNonNegativeAtomicStock",
    "InventoryOrder_WhenLaterSkuIsInsufficient_ShouldRememberResultWithoutPartialDeduction",
    "InventoryOrder_WhenCriticalAuditFails_ShouldRollbackAllBusinessWrites",
    "InventoryOrder_WhenCancelledAfterOutbox_ShouldRollbackAllWrites",
    "InventoryOrder_WhenProcessesCompeteForSameOrder_ShouldCommitExactlyOnce",
    "InventoryOrder_WhenProcessesCompeteForDifferentOrders_ShouldKeepStockAtomic",
    "InventoryOrder_WhenProcessDiesBeforeCommit_ShouldRollbackAndRetryOriginalOrder",
    "InventoryOrder_WhenProcessDiesAfterCommit_ShouldRecoverOriginalResultWithoutDuplicateWrites",
    "InventoryOrder_WhenInnoDbDeadlocks_ShouldAwaitCleanupAndRetryFreshScope",
    "InventoryOrder_WhenInnoDbDeadlocksPersist_ShouldStopAfterFourCleanAttempts",
    "InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOriginalCommittedResult",
    "InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldPreserveRecoveryAndCleanupFailures",
    "InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOutboxThroughBackgroundScan",
    "Consumption_WhenCancelledBeforeBusinessCommit_ShouldRetryAndCommitOnce",
    "Consumption_WhenCancelledAfterBusinessCommit_ShouldRetryWithoutDuplicateWrites",
    "PublishAsync_WhenCommitted_ShouldReceiveAndProcessOnceInOrder",
    "PublishAsync_WhenRolledBack_ShouldRetryFromFirstSequence",
    "ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence"
)
foreach ($methodMarker in $requiredMethodMarkers) {
    if ($runnerText -notmatch [regex]::Escape($methodMarker)) {
        throw "验收脚本缺少必需方法 $methodMarker。"
    }
}

# 每套 ORM 固定验收二十一个完整方法名，不能用另一套 ORM 或无关方法代替。
if (@($projects).Count -ne 2) {
    throw "Admin MySQL/CAP 验收必须包含且仅包含 EFCore、FreeSQL 两套项目。"
}
foreach ($project in $projects) {
    if ($project.Name -notin @("EFCore", "FreeSQL") -or @($project.RequiredTestMethods).Count -ne 21) {
        throw "Admin $($project.Name) 必须逐项验收二十一个关键方法。"
    }
    $minimum = if ($project.Name -eq "EFCore") { 34 } else { 33 }
    if ($project.MinimumTests -ne $minimum) {
        throw "Admin $($project.Name) 最低测试数量必须是 $minimum。"
    }
    foreach ($methodMarker in $requiredMethodMarkers) {
        $testClass = if ($methodMarker.StartsWith("InventoryOrder_", [StringComparison]::Ordinal) -or
            $methodMarker.StartsWith("Consumption_", [StringComparison]::Ordinal)) {
            "AdminMySqlCapIntegrationTest"
        }
        else {
            "OrderedEventCapIntegrationTest"
        }
        $required = "Bing.Admin.$($project.Name).Tests.$testClass" + "::" + $methodMarker
        if ($project.RequiredTestMethods -cnotcontains $required) {
            throw "Admin $($project.Name) 缺少完整必需方法 $required。"
        }
    }
}

# 生成受控 TRX，将计数声明为达标，独立验证每个方法的存在与 Passed 状态。
function New-RequiredMethodsFixture {
    param(
        [string[]]$RequiredMethods,
        [int]$MinimumTests,
        [string]$ExcludedMethod = $null,
        [string]$ChangedMethod = $null,
        [string]$ChangedOutcome = "Passed"
    )

    $document = [xml]"<TestRun><TestDefinitions/><Results/><ResultSummary><Counters/></ResultSummary></TestRun>"
    $definitions = $document.TestRun.SelectSingleNode("TestDefinitions")
    $results = $document.TestRun.SelectSingleNode("Results")
    $index = 0
    foreach ($method in $RequiredMethods) {
        if ($method -ceq $ExcludedMethod) {
            continue
        }
        $parts = $method -split "::", 2
        $id = "required-$index"
        $unitTest = $document.CreateElement("UnitTest")
        $unitTest.SetAttribute("id", $id)
        $testMethod = $document.CreateElement("TestMethod")
        $testMethod.SetAttribute("className", $parts[0])
        $testMethod.SetAttribute("name", $parts[1])
        $unitTest.AppendChild($testMethod) | Out-Null
        $definitions.AppendChild($unitTest) | Out-Null
        $result = $document.CreateElement("UnitTestResult")
        $result.SetAttribute("testId", $id)
        $outcome = if ($method -ceq $ChangedMethod) { $ChangedOutcome } else { "Passed" }
        $result.SetAttribute("outcome", $outcome)
        $results.AppendChild($result) | Out-Null
        $index++
    }
    while ($index -lt $MinimumTests) {
        $id = "unrelated-$index"
        $unitTest = $document.CreateElement("UnitTest")
        $unitTest.SetAttribute("id", $id)
        $testMethod = $document.CreateElement("TestMethod")
        $testMethod.SetAttribute("className", "Unrelated.Tests.PassingTests")
        $testMethod.SetAttribute("name", "PassingTest$index")
        $unitTest.AppendChild($testMethod) | Out-Null
        $definitions.AppendChild($unitTest) | Out-Null
        $result = $document.CreateElement("UnitTestResult")
        $result.SetAttribute("testId", $id)
        $result.SetAttribute("outcome", "Passed")
        $results.AppendChild($result) | Out-Null
        $index++
    }
    $counters = $document.TestRun.ResultSummary.SelectSingleNode("Counters")
    foreach ($name in @("total", "passed")) {
        $counters.SetAttribute($name, [string]$index)
    }
    $counters.SetAttribute("failed", "0")
    $counters.SetAttribute("notExecuted", "0")
    return $document
}

$processMethodMarkers = $requiredMethodMarkers | Where-Object {
    $_.StartsWith("InventoryOrder_WhenProcesses", [StringComparison]::Ordinal) -or
        $_.StartsWith("InventoryOrder_WhenProcessDies", [StringComparison]::Ordinal) -or
        $_.StartsWith("InventoryOrder_WhenInnoDbDeadlocks", [StringComparison]::Ordinal) -or
        $_.StartsWith("InventoryOrder_WhenCommitAcknowledgementIsLost", [StringComparison]::Ordinal) -or
        $_.StartsWith("Consumption_WhenCancelled", [StringComparison]::Ordinal)
}
$processRejectionCases = 0
foreach ($project in $projects) {
    $complete = New-RequiredMethodsFixture $project.RequiredTestMethods $project.MinimumTests
    if (-not (Test-TrxCounters $complete.TestRun.ResultSummary.Counters $project.MinimumTests) -or
        -not (Test-TrxRequiredMethods $complete $project.RequiredTestMethods)) {
        throw "Admin $($project.Name) 的完整二十一项必需方法夹具应通过。"
    }
    foreach ($methodMarker in $processMethodMarkers) {
        $required = "Bing.Admin.$($project.Name).Tests.AdminMySqlCapIntegrationTest" + "::" + $methodMarker
        $missing = New-RequiredMethodsFixture $project.RequiredTestMethods $project.MinimumTests -ExcludedMethod $required
        if (-not (Test-TrxCounters $missing.TestRun.ResultSummary.Counters $project.MinimumTests) -or
            (Test-TrxRequiredMethods $missing $project.RequiredTestMethods)) {
            throw "Admin $($project.Name) 缺少 $methodMarker 时不能由无关通过方法补数。"
        }
        $processRejectionCases++
        foreach ($outcome in @("Failed", "NotExecuted", "Inconclusive", "Aborted", "Timeout", "")) {
            $notPassed = New-RequiredMethodsFixture $project.RequiredTestMethods $project.MinimumTests `
                -ChangedMethod $required -ChangedOutcome $outcome
            if (Test-TrxRequiredMethods $notPassed $project.RequiredTestMethods) {
                throw "Admin $($project.Name) 的 $methodMarker 为 '$outcome' 时必须拒绝。"
            }
            $processRejectionCases++
        }
    }
}
if ($processRejectionCases -ne 154) {
    throw "库存跨进程、真实死锁、提交确认丢失和消费取消必需方法应包含二十二项缺失及一百三十二项非 Passed 状态拒绝断言。"
}
Write-Host "库存跨进程、真实死锁、提交确认丢失和消费取消必需方法集合：两套各二十一项完整夹具通过，一百五十四项拒绝断言通过。"
foreach ($case in @(
    @{ Name = "EFCore 恰好达标"; Minimum = 34; Total = 34; Passed = 34; Failed = 0; Skipped = 0; Accepted = $true },
    @{ Name = "EFCore 多一项"; Minimum = 34; Total = 35; Passed = 35; Failed = 0; Skipped = 0; Accepted = $true },
    @{ Name = "EFCore 少一项"; Minimum = 34; Total = 33; Passed = 33; Failed = 0; Skipped = 0; Accepted = $false },
    @{ Name = "EFCore 含跳过"; Minimum = 34; Total = 34; Passed = 33; Failed = 0; Skipped = 1; Accepted = $false },
    @{ Name = "EFCore 含失败"; Minimum = 34; Total = 34; Passed = 33; Failed = 1; Skipped = 0; Accepted = $false },
    @{ Name = "FreeSQL 恰好达标"; Minimum = 33; Total = 33; Passed = 33; Failed = 0; Skipped = 0; Accepted = $true },
    @{ Name = "FreeSQL 多一项"; Minimum = 33; Total = 34; Passed = 34; Failed = 0; Skipped = 0; Accepted = $true },
    @{ Name = "FreeSQL 少一项"; Minimum = 33; Total = 32; Passed = 32; Failed = 0; Skipped = 0; Accepted = $false },
    @{ Name = "FreeSQL 含跳过"; Minimum = 33; Total = 33; Passed = 32; Failed = 0; Skipped = 1; Accepted = $false },
    @{ Name = "FreeSQL 含失败"; Minimum = 33; Total = 33; Passed = 32; Failed = 1; Skipped = 0; Accepted = $false }
)) {
    $counters = [pscustomobject]@{
        total = $case.Total
        passed = $case.Passed
        failed = $case.Failed
        notExecuted = $case.Skipped
    }
    if ((Test-TrxCounters $counters $case.Minimum) -ne $case.Accepted) {
        throw "$($case.Name) 的 TRX 计数结果不符。"
    }
}

$requiredFixtureMethods = @(
    "Fixture.Tests.AdminMySqlCapIntegrationTest::InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated",
    "Fixture.Tests.OrderedEventCapIntegrationTest::ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence"
)
$completeFixtureTrx = [xml]@'
<TestRun>
  <TestDefinitions>
    <UnitTest id="inventory"><TestMethod className="Fixture.Tests.AdminMySqlCapIntegrationTest" name="InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated" /></UnitTest>
    <UnitTest id="ordered"><TestMethod className="Fixture.Tests.OrderedEventCapIntegrationTest" name="ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence" /></UnitTest>
  </TestDefinitions>
  <Results>
    <UnitTestResult testId="inventory" outcome="Passed" />
    <UnitTestResult testId="ordered" outcome="Passed" />
  </Results>
</TestRun>
'@
if (-not (Test-TrxRequiredMethods $completeFixtureTrx $requiredFixtureMethods)) {
    throw "完整 TRX 应通过所有必需方法。"
}
$unrelatedFixtureTrx = [xml]@'
<TestRun>
  <TestDefinitions>
    <UnitTest id="inventory"><TestMethod className="Fixture.Tests.AdminMySqlCapIntegrationTest" name="InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated" /></UnitTest>
    <UnitTest id="unrelated-one"><TestMethod className="Fixture.Tests.AdminMySqlCapIntegrationTest" name="UnrelatedPassingTestOne" /></UnitTest>
    <UnitTest id="unrelated-two"><TestMethod className="Fixture.Tests.AdminMySqlCapIntegrationTest" name="UnrelatedPassingTestTwo" /></UnitTest>
  </TestDefinitions>
  <Results>
    <UnitTestResult testId="inventory" outcome="Passed" />
    <UnitTestResult testId="unrelated-one" outcome="Passed" />
    <UnitTestResult testId="unrelated-two" outcome="Passed" />
  </Results>
  <ResultSummary><Counters total="3" passed="3" failed="0" notExecuted="0" /></ResultSummary>
</TestRun>
'@
if (-not (Test-TrxCounters $unrelatedFixtureTrx.TestRun.ResultSummary.Counters 2)) {
    throw "无关测试夹具应先满足总数门槛，以验证必需方法检查的独立作用。"
}
if (Test-TrxRequiredMethods $unrelatedFixtureTrx $requiredFixtureMethods) {
    throw "缺少必需方法时，即使无关测试补足总数也必须拒绝。"
}
if (Test-TrxCounters $null 31) {
    throw "缺失 TRX 计数应被拒绝。"
}
if (Test-TrxCounters ([pscustomobject]@{ total = 31; passed = 31; failed = 0; notExecuted = 0 }) 0) {
    throw "无效门槛应被拒绝。"
}

# 使用本地伪 TRX 验证失败诊断保留预检值、失败项目和真实测试退出码。
$Framework = "net6.0"
$Configuration = "Release"
$fixtureDirectory = Join-Path $resultsPath "runner-failure"
New-Item -ItemType Directory -Path $fixtureDirectory -Force | Out-Null
$efTrx = Join-Path $fixtureDirectory "admin-efcore-mysqlcap.trx"
$freeTrx = Join-Path $fixtureDirectory "admin-freesql-mysqlcap.trx"
"fixture" | Set-Content -LiteralPath $efTrx -Encoding utf8
"fixture" | Set-Content -LiteralPath $freeTrx -Encoding utf8
$existing = @(Get-ExistingTrxPaths -RunDirectory $fixtureDirectory -Projects @(
    @{ Name = "EFCore" }, @{ Name = "FreeSQL" }
))
if ($existing.Count -ne 2 -or $existing[0] -ne $efTrx -or $existing[1] -ne $freeTrx) {
    throw "失败诊断未枚举本轮已有的 TRX 文件。"
}
$preflight = [pscustomobject]@{
    DatabaseName = "bing_integration_test"
    ConnectionConfigured = $true
    MySqlReachable = $true
    RabbitReachable = $true
}
$failure = New-TestFailureDiagnostic -Preflight $preflight -Reason "Admin FreeSQL dotnet test 返回退出码 17。" `
    -FailedProject "FreeSQL" -TestExitCode 17 -TrxPaths $existing -StartedAtUtc ([DateTimeOffset]::UtcNow)
if ($failure.DatabaseName -ne $preflight.DatabaseName -or -not $failure.ConnectionConfigured -or
    -not $failure.MySqlReachable -or -not $failure.RabbitReachable -or
    $failure.FailedProject -ne "FreeSQL" -or $failure.TestExitCode -ne 17 -or
    $failure.ExitCode -ne 17 -or $failure.TrxPath -ne $freeTrx -or
    $failure.TrxPaths.Count -ne 2 -or $failure.ExecutionStatus -ne "Failed") {
    throw "测试进程失败时未保留预检、项目、退出码和已有 TRX。"
}
$missingTrx = New-TestFailureDiagnostic -Preflight $preflight -Reason "测试进程提前失败。" `
    -FailedProject "EFCore" -TestExitCode 9 -TrxPaths @() -StartedAtUtc ([DateTimeOffset]::UtcNow)
if (-not [string]::IsNullOrEmpty($missingTrx.TrxPath) -or $missingTrx.TrxPaths.Count -ne 0 -or
    $missingTrx.FailedProject -ne "EFCore" -or $missingTrx.ExitCode -ne 9) {
    throw "未生成 TRX 的测试进程失败诊断不符：TrxPath='$($missingTrx.TrxPath)'，路径数=$($missingTrx.TrxPaths.Count)，项目=$($missingTrx.FailedProject)，退出码=$($missingTrx.ExitCode)。"
}
$thresholdFailure = New-TestFailureDiagnostic -Preflight $preflight -Reason "TRX 未达门槛。" `
    -FailedProject "EFCore" -TestExitCode 0 -TrxPaths @($efTrx) -StartedAtUtc ([DateTimeOffset]::UtcNow)
if ($thresholdFailure.ExitCode -ne 1 -or $thresholdFailure.TestExitCode -ne 0 -or
    $thresholdFailure.TrxPath -ne $efTrx) {
    throw "TRX 门槛失败应区分脚本失败与测试进程退出码。"
}

function Assert-BlockedPreflight {
    param(
        [string]$ExpectedReason,
        [switch]$ValidateOnly
    )

    $before = @(Get-ChildItem -LiteralPath $resultsPath -Filter "admin-mysqlcap-diagnostic.json" -Recurse -File -ErrorAction SilentlyContinue)
    $arguments = @("-NoProfile", "-File", $runner, "-ResultsDirectory", $resultsDirectory)
    if ($ValidateOnly) {
        $arguments += "-ValidateOnly"
    }
    & pwsh @arguments | Out-Null
    if ($LASTEXITCODE -ne 2) {
        throw "预检失败时退出码应为 2，实际为 $LASTEXITCODE。"
    }
    $after = @(Get-ChildItem -LiteralPath $resultsPath -Filter "admin-mysqlcap-diagnostic.json" -Recurse -File)
    if ($after.Count -ne $before.Count + 1) {
        throw "预检应只生成一份新的诊断文件。"
    }
    $latest = $after | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    $diagnostic = Get-Content -LiteralPath $latest.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($diagnostic.ExecutionStatus -ne "Blocked" -or $diagnostic.FailurePhase -ne "Preflight" -or
        $diagnostic.ExitCode -ne 2 -or $diagnostic.BlockedReason -notlike "*$ExpectedReason*" -or
        $diagnostic.DatabaseName -ne "bing_integration_test" -or -not $diagnostic.ConnectionConfigured) {
        throw "预检诊断状态或原因不符：$($latest.FullName)"
    }
}

try {
    [Environment]::SetEnvironmentVariable("RUN_MYSQL_INTEGRATION_TESTS", "true")
    [Environment]::SetEnvironmentVariable("RUN_MYSQL_CAP_INTEGRATION_TESTS", "true")
    [Environment]::SetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS", "true")
    foreach ($name in @("RUN_INTEGRATION_TESTS", "RUN_POSTGRESQL_INTEGRATION_TESTS", "RUN_SQLSERVER_INTEGRATION_TESTS")) {
        [Environment]::SetEnvironmentVariable($name, $null)
    }
    [Environment]::SetEnvironmentVariable("CAP_RABBITMQ_HOST", "127.0.0.1")
    [Environment]::SetEnvironmentVariable("CAP_RABBITMQ_USERNAME", "test")
    [Environment]::SetEnvironmentVariable("CAP_RABBITMQ_PASSWORD", "test")

    [Environment]::SetEnvironmentVariable("ConnectionStrings__MySqlConnection", "Server=127.0.0.1;Port=0;Database=bing_integration_test;User Id=test;Password=test")
    [Environment]::SetEnvironmentVariable("CAP_RABBITMQ_PORT", "0")
    Assert-BlockedPreflight -ExpectedReason "MySQL 端口必须" -ValidateOnly

    [Environment]::SetEnvironmentVariable("ConnectionStrings__MySqlConnection", "Server=127.0.0.1;Port=abc;Database=bing_integration_test;User Id=test;Password=test")
    [Environment]::SetEnvironmentVariable("CAP_RABBITMQ_PORT", "65536")
    Assert-BlockedPreflight -ExpectedReason "RabbitMQ 端口必须"

    [Environment]::SetEnvironmentVariable("ConnectionStrings__MySqlConnection", "Server=127.0.0.1;Port=-1;Database=bing_integration_test;User Id=test;Password=test")
    [Environment]::SetEnvironmentVariable("CAP_RABBITMQ_PORT", "-1")
    Assert-BlockedPreflight -ExpectedReason "MySQL 端口必须" -ValidateOnly

    [Environment]::SetEnvironmentVariable("ConnectionStrings__MySqlConnection", "Server=127.0.0.1;Port=65536;Database=bing_integration_test;User Id=test;Password=test")
    [Environment]::SetEnvironmentVariable("CAP_RABBITMQ_PORT", "abc")
    Assert-BlockedPreflight -ExpectedReason "RabbitMQ 端口必须"

    [Environment]::SetEnvironmentVariable("ConnectionStrings__MySqlConnection", "Server=127.0.0.1;Port=3306;Database=bing_integration_test;User Id=test;Password=test")
    [Environment]::SetEnvironmentVariable("RUN_MYSQL_CAP_INTEGRATION_TESTS", $null)
    Assert-BlockedPreflight -ExpectedReason "RUN_MYSQL_CAP_INTEGRATION_TESTS=true"

    Write-Host "Admin MySQL/CAP 预检、TRX 计数及必需方法校验脚本测试通过。"
}
finally {
    foreach ($name in $names) {
        [Environment]::SetEnvironmentVariable($name, $original[$name])
    }
}
