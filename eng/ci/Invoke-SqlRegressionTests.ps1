[CmdletBinding()]
param(
    [string]$ResultsDirectory = "output/sql-regression",
    [string]$RestoreConfigFile
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"
# 逐格记录 dotnet 的退出码，失败格不影响剩余测试和 TRX 收集。
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$testRoot = Join-Path $repositoryRoot "framework/tests"
$results = [IO.Path]::GetFullPath($ResultsDirectory, $repositoryRoot)
$runDirectory = Join-Path $results ("run-" + [Guid]::NewGuid().ToString("N"))
$dualFrameworkProjects = @(
    "Bing.Data.Sql.Tests"
    "Bing.Data.Sql.CustomProvider.Tests"
    "Bing.Dapper.Core.Tests"
    "Bing.Dapper.Sqlite.Tests"
    "Bing.Dapper.MySql.Tests"
    "Bing.Dapper.PostgreSql.Tests"
    "Bing.Dapper.SqlServer.Tests"
    "Bing.Dapper.Oracle.Tests"
    "Bing.Uow.Tests"
    "Bing.Dapper.Sqlite.Tests.Integration"
)
$net8Projects = $dualFrameworkProjects + @("Bing.Data.Sql.Analyzers.Tests", "Bing.Data.Sql.Generators.Tests")
$matrix = [ordered]@{ "net6.0" = $dualFrameworkProjects; "net8.0" = $net8Projects }
$failures = [System.Collections.Generic.List[string]]::new()
$verifiedReports = [ordered]@{}
$restoreArguments = @()
if ($RestoreConfigFile) {
    $restoreArguments = @("-p:RestoreConfigFile=$((Resolve-Path -LiteralPath $RestoreConfigFile).Path)")
}

New-Item -ItemType Directory -Path $runDirectory | Out-Null
Push-Location $repositoryRoot
try {
    foreach ($framework in $matrix.Keys) {
        foreach ($project in $matrix[$framework]) {
            $file = Join-Path $testRoot "$project/$project.csproj"
            $trxName = "$project.$framework.trx"
            $caseName = "$project/$framework"
            & dotnet test $file --configuration Release --framework $framework --nologo `
                -p:UseSharedCompilation=false -m:1 -nr:false `
                --results-directory $runDirectory --logger "trx;LogFileName=$trxName" @restoreArguments
            if ($LASTEXITCODE -ne 0) {
                $failures.Add("$caseName (exit $LASTEXITCODE)")
                continue
            }

            $trx = Join-Path $runDirectory $trxName
            if (!(Test-Path -LiteralPath $trx -PathType Leaf)) {
                $failures.Add("$caseName (TRX missing)")
                continue
            }
            try {
                $summary = ([xml](Get-Content -LiteralPath $trx -Encoding utf8 -Raw)).TestRun.ResultSummary
                $executed = [int]$summary.Counters.executed
                $failed = [int]$summary.Counters.failed
            }
            catch {
                $failures.Add("$caseName (invalid TRX)")
                continue
            }
            if ($summary.outcome -notin @('Completed', 'Passed') -or $failed -ne 0) {
                $failures.Add("$caseName (TRX outcome=$($summary.outcome), failed=$failed)")
            }
            elseif ($executed -lt 1) {
                $failures.Add("$caseName (no tests executed)")
            }
            else {
                try {
                    $verifiedReports[$trxName] = (Get-FileHash -LiteralPath $trx -Algorithm SHA256).Hash.ToLowerInvariant()
                }
                catch {
                    $failures.Add("$caseName (TRX hash failed)")
                }
            }
        }
    }
}
finally {
    Pop-Location
}
if ($failures.Count) {
    throw "SQL regression failed: $($failures -join ', '). TRX: $runDirectory"
}
$workflow = [ordered]@{
    repository = $env:GITHUB_REPOSITORY
    runId = $env:GITHUB_RUN_ID
    sha = $env:GITHUB_SHA
    attempt = $env:GITHUB_RUN_ATTEMPT
}
$receipt = [ordered]@{
    workflow = $workflow
    reports = $verifiedReports
}
$receiptJson = ConvertTo-Json -InputObject $receipt -Depth 4
[IO.File]::WriteAllText((Join-Path $runDirectory "reports.json"), $receiptJson, [Text.UTF8Encoding]::new($false))
Write-Output "SQL_REGRESSION_PASS: $runDirectory"
