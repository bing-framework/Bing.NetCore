# SQL 投影基准

`SqlProjectionBenchmarks` 的既有工作负载使用 1、5、20 个同类型且显式命名的来源，覆盖自动全列投影无排除/少量排除/大量排除、连续 `Select` 累计与去重、`ReplaceSelect`、克隆后独立追加投影、参数名命中与未命中、Count/Sum 汇总投影、不同映射 profile 下克隆后追加投影，以及重复 `ToSql`。参数名未命中场景会捕获预期的多来源歧义，并检查 SQL 仍与失败前一致；单来源会验证唯一类型回退。

Count/Sum 汇总投影现在有三种同工作量写法：既有 selector（`functions.Count(x => x.Id)` / `functions.Sum(x => x.Quantity)`）、聚合上下文直接引用外层 Lambda 成员（`functions.Count(r1.Id)` / `functions.Sum(r1.Quantity)`），以及静态 `SqlFn`（`SqlFn.Count(r1.Id)` / `SqlFn.Sum(r1.Quantity)`）。三者都从同一 1/5/20 来源查询克隆，在 `GlobalSetup` 中逐字比较完整 SQL，并与完整预期 SQL 比较。新直接成员和 `SqlFn` 写法没有历史性能基线（`BEFORE_STATUS=NOT_APPLICABLE`）；三种形式可在相同构建与渲染工作量下横向比较，Dry 结果只证明场景可运行。

另有两项 `AutoGenerateTableAlias=false` 场景覆盖多来源克隆投影和缓存 SQL 重复渲染。来源均显式设置 `r1` 到 `rN`，确保关闭自动生成时绑定仍有效；`GlobalSetup` 对克隆 SQL 和两次重复渲染结果逐字校验完整 SQL。关闭策略场景同样没有历史基线。

`RenderContinuousSameTypeJoinProjection` 用 1、5、20 个同类型来源构造连续 Join：从 `From<ProjectionOrder>()` 开始，依次用 `(r1,r2)`、`(r2,r3)` 等参数角色调用不带别名的生产 `Join<T,T>(predicate)`，最后显式选择 `r1.Id`。`GlobalSetup` 在计时前构建并缓存 ParameterExpression、Join predicate 和投影表达式；计时路径只创建查询、绑定来源、调用 Join/Select 并渲染。`GlobalSetup` 逐字校验完整预期 SQL。该场景的三个 Dry 组合已定向运行，三个进程均退出 0，完整 SQL setup 校验通过，见[连续 Join 日志](../../../ai_docs/tasks/20261029-sqlquery-projection-mapping-aot/evidence/alias-sqlfn/benchmark-join-dry.log)。Dry 只验证可运行性，没有历史性能基线。

新增 `RenderHeterogeneousSourceProjection` 使用 1、5、20 个 CLR 类型各不相同且每种类型只出现一次的来源。每个 Lambda 都用不匹配任何 alias 的参数名，调用现有生产查询 API 按实体类型唯一回退；20 个封闭类型在基准代码中静态定义，计时路径没有反射建类型。`GlobalSetup` 会核对来源 SQL 和完整投影 SQL，确保来源数、表名、alias、每个 `Id` 列及唯一类型绑定都符合预期。这与既有同类型多源的参数名歧义/显式 alias 场景不同。基准使用 MySQL Builder 和空操作执行器，不连接数据库。

`SqlProjectionDryHostBenchmarks` 位于独立的 DryHost 程序集中，逐一调用相同工作负载，并且只声明 `DryJob`。以下命令将每个投影场景按 1、5、20 个来源运行一次，并把 BenchmarkDotNet 工件放到任务 evidence 目录：

```powershell
dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -- --filter "*SqlProjectionDryHostBenchmarks*" --artifacts .\ai_docs\tasks\20261029-sqlquery-projection-mapping-aot\evidence\sql-projection-dryhost-artifacts
$benchmarkExitCode = $LASTEXITCODE
if ($benchmarkExitCode -ne 0) { exit $benchmarkExitCode }
```

`BEFORE_STATUS=NOT_APPLICABLE`：仓库没有这些投影 API 的历史同构基准或可比基线。Dry 只验证工作负载可运行，不作为性能达标、性能回归或历史性能提升的结论。完整 `FormalHost` 场景保留在 `SqlProjectionBenchmarks`，不由上述命令选中。

聚合语法比较和别名关闭场景也可分别定向运行，避免扩大单项核验的 Dry 范围：

```powershell
dotnet run -c Release --no-build --project .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -- --filter "*Additional_RenderAggregateProjection*" --artifacts .\ai_docs\tasks\20261029-sqlquery-projection-mapping-aot\evidence\sql-aggregate-syntax-dryhost
dotnet run -c Release --no-build --project .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -- --filter "*AliasGenerationDisabled*" --artifacts .\ai_docs\tasks\20261029-sqlquery-projection-mapping-aot\evidence\sql-alias-disabled-dryhost
```

第一条覆盖三个聚合写法各自的 1/5/20 来源组合；第二条覆盖关闭自动别名后的克隆及重复渲染各自的 1/5/20 来源组合。以上共 15 个 Dry 组合见[本轮日志](../../../ai_docs/tasks/20261029-sqlquery-projection-mapping-aot/evidence/alias-sqlfn/benchmark-dry.log)。这些结果只提供可运行性证据。

连续 Join 场景单独执行，结果保存到新的任务证据目录：

```powershell
dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -c Release
dotnet run -c Release --no-build --project .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -- --filter "*Additional_RenderContinuousSameTypeJoinProjection*" --artifacts .\ai_docs\tasks\20261029-sqlquery-projection-mapping-aot\evidence\continuous-join-benchmark
$benchmarkExitCode = $LASTEXITCODE
if ($benchmarkExitCode -ne 0) { exit $benchmarkExitCode }
```

异构来源场景可单独执行三个参数组合：

```powershell
dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -c Release
dotnet run -c Release --no-build --project .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -- --filter "*Additional_RenderHeterogeneousSourceProjection*" --artifacts .\ai_docs\tasks\20261029-sqlquery-projection-mapping-aot\evidence\fix-round-1\heterogeneous-benchmark
$benchmarkExitCode = $LASTEXITCODE
if ($benchmarkExitCode -ne 0) { exit $benchmarkExitCode }
```

新增 DryHost wrapper 方法以 `Additional_` 开头。当前 `*Additional_*` 过滤器会运行 36 个组合（12 个方法 × 1/5/20 来源）。历史日志覆盖此前的扩展入口及异构来源；本轮三个聚合写法和两个自动别名关闭入口的 15 个组合见上方日志。连续 Join 入口也已单独定向运行三个组合；本轮合计 18 个 Dry 组合通过（上述 15 项加连续 Join 3 项），没有将历史结果计作本轮重跑。DryJob 输出存入新的任务证据目录：

```powershell
dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -c Release
dotnet run -c Release --no-build --project .\framework\tests\Bing.Data.Sql.Benchmarks.DryHost\Bing.Data.Sql.Projection.DryHost.csproj -- --filter "*Additional_*" --artifacts .\ai_docs\tasks\20261029-sqlquery-projection-mapping-aot\evidence\sql-projection-expanded-dryhost
$benchmarkExitCode = $LASTEXITCODE
if ($benchmarkExitCode -ne 0) { exit $benchmarkExitCode }
```

此前已完成一次 Release 构建和一次新增场景 DryJob：原有扩展工作负载执行 18 项（6 个方法 × 1/5/20 来源），进程退出码为 0。日志见 [`sql-projection-expanded-dryhost-success.log`](../../../ai_docs/tasks/20261029-sqlquery-projection-mapping-aot/evidence/sql-projection-expanded-dryhost-success.log)，BDN 工件见 [`sql-projection-expanded-dryhost`](../../../ai_docs/tasks/20261029-sqlquery-projection-mapping-aot/evidence/sql-projection-expanded-dryhost)。另单独筛选未命中回退/回滚方法，对 1/5/20 来源的 setup 断言与 DryJob 共执行 3 项，退出码为 0；日志见 [`sql-projection-expanded-binding-validation.log`](../../../ai_docs/tasks/20261029-sqlquery-projection-mapping-aot/evidence/sql-projection-expanded-binding-validation.log)。5/20 来源的预期歧义异常在方法内捕获，BDN 线程统计仍记录每轮 2 次异常（warmup 和 measurement），但没有未处理异常或失败进程。本轮已按单场景过滤器完成异构来源的 1/5/20 三个 Dry 组合，三个进程均退出 0；[实际日志与结果](../../../ai_docs/tasks/20261029-sqlquery-projection-mapping-aot/evidence/fix-round-1/heterogeneous-benchmark)保留完整 SQL 的 setup 验证。累计有 39 个不同工作负载组合的可运行证据（原 36 项复用，不算本轮重跑）；Dry 只有单次冷启动测量，BDN 也提示迭代远短于建议时长，这些数据只证明工作负载可运行，不用于性能结论。

### FIX-007～010 验证适用性（2026-10-30）

本轮修改聚合条件业务值转换、执行前结果校验与生成读取器，不改变来源解析、映射缓存键、SQL 格式化和 Clone，也未修改现有基准方法与数据集。沿用此前 18 个 Dry 组合的工作负载可运行证据，不将其称为本轮性能测量。新增 object 读取和非法目标拒绝用职责测试、SQLite 与四执行路径验收；无历史同构性能基线，不新增虚构 before 或性能阈值。

### FIX-011～013 验证适用性（2026-10-30）

本轮限定为聚合结果类型校验、编译期浮点分类及 char 业务参数转换。来源解析、映射缓存键、SQL 格式化、Builder Clone 与基准数据未变，复用既有 Dry 工作负载证据；没有重新测量或宣称性能提升。新增边界通过直接测试、SQLite 和原生宿主验证，历史同构性能对比不适用。
