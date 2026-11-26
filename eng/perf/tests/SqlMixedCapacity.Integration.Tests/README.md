# PostgreSQL 混合探针集成测试

该入口用于 Windows 本机短时集成验证，复用 Benchmark 内部的 `MeasureAsync`，不复制查询、写入或清理算法。正式 `--mixed-capacity-probe` 继续要求已批准的负载和真实 cgroup；测试通过不表示任何硬件档位达标。

先启动独立的临时 PostgreSQL 16，只开放回环端口，创建专用 `bing_sql_aot_test` 库。诊断场景需要容器启动参数 `-c shared_preload_libraries=pg_stat_statements -c track_io_timing=on -c track_wal_io_timing=on`；测试用户只在这个临时库创建扩展，禁止对生产库执行配置。普通容量模式不要求这些扩展。设置 `RUN_POSTGRESQL_INTEGRATION_TESTS=true` 和 `ConnectionStrings__PostgreSqlConnection`，不要把密码写入命令日志。当前测试在不匹配的 cgroup 环境验证正式入口先拒绝请求，因此不要在恰好为 8C16G 的应用容器里运行本入口。

从仓库根目录构建。`common.props` 会覆盖 SDK 的输出位置，因此除 `--artifacts-path` 外还需要为依赖工程显式隔离输出：

```powershell
$root = Join-Path $PWD 'artifacts/validation/r02-pg/manual'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$targets = Join-Path $root 'isolated.targets'
@'
<Project><PropertyGroup>
<StageBuildOutputDir>$(MSBuildThisFileDirectory)bin/$(MSBuildProjectName)/$(Configuration)/$(TargetFramework)/</StageBuildOutputDir>
<OutputPath>$(StageBuildOutputDir)</OutputPath>
<OutDir>$(StageBuildOutputDir)</OutDir>
<TargetDir>$(StageBuildOutputDir)</TargetDir>
<TargetPath>$(StageBuildOutputDir)$(TargetFileName)</TargetPath>
<DocumentationFile>$(OutputPath)$(AssemblyName).xml</DocumentationFile>
</PropertyGroup></Project>
'@ | Set-Content -LiteralPath $targets -Encoding utf8
$project = 'eng/perf/tests/SqlMixedCapacity.Integration.Tests/SqlMixedCapacity.Integration.Tests.csproj'
$previousTargets = $env:DirectoryBuildTargetsPath
try {
    $env:DirectoryBuildTargetsPath = $targets
    dotnet build $project -c Release --artifacts-path $root --disable-build-servers `
        -p:UseSharedCompilation=false -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Integration build failed.' }
}
finally { $env:DirectoryBuildTargetsPath = $previousTargets }
$integration = Join-Path $root 'bin/SqlMixedCapacity.Integration.Tests/Release/net8.0/SqlMixedCapacity.Integration.Tests.dll'
dotnet $integration eng/perf/r02-workload.json artifacts/benchmarks/r02-pg/manual/tests.json
if ($LASTEXITCODE -ne 0) { throw 'Integration failed.' }
```

报告必须使用新的文件名。三个直接场景覆盖：

- `RejectsUnmatchedCgroupBeforeConnecting`：不匹配硬件限制时，在访问数据库之前返回 2，且不写正式容量收据。
- `ExecutesReadsWritesAndCleansFixture`：2,000 行、1 秒预热/3 秒测量、并发 8、池 2；真实 DI 查询和主键更新、确定读写调度、活动及池计数采样、测试表删除。
- `ExecutesReadsWritesAndCleansFixture(true)`：同样的短窗口、并发1，阶段计数匹配测量请求，预热不计入；普通场景阶段、连接和数据库命令字段必须为null；诊断场景的服务端读写调用数和行数对应请求，并观察WAL同步；两个场景均验证表清理。诊断收据额外保存到.stages.json。

输出使用 `SqlMixedCapacityIntegration-v1` 和独立的测量格式，记录候选程序集摘要及 `productionCapacityVerified=false`，不能交给正式容量门禁。进程级 Npgsql 忙闲计数还可能包含管理连接，不能直接与业务池上限比较。没有数据库门控、或连接到其他库时返回 2，不连接数据库、不写收据。

测试后删除自己创建的容器；保留最终证据并核对摘要，再清理该次 `artifacts/validation/` 下的可重建产物。真实 100K/30/120 负载、三档应用限额、独立数据库宿主、池排队和容量预算仍按 R02 正式方案验收。
