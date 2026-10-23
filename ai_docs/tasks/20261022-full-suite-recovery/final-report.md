# 最终验收报告

## 结论

最新 Sol/high 独立复验为 PASS，`CACHE_RETRY_STATUS=PASS`、`OPEN_ACTIONABLE=0`，FIX-001..004 全部 CLOSED。缓存重试回归已按有限业务重试契约修正，生产 resolver 未变；随后当前候选 Release solution build 和默认 solution L4 均 PASS，L4 退出码 0。net6.0 为 7,103 项、6,515 Passed、0 Failed、588 NotExecuted；net8.0 为 6,563 项、6,046 Passed、0 Failed、517 NotExecuted。未执行项不计为通过。

正式 Release evidence 门禁仍为 `BLOCKED`，clean-source 条件未满足。未运行的外部 CI/IDE 验证和 L4 skip 项仍是未验证边界。本轮没有提交或清理工作区，也没有更改该门禁。

## 上一候选最终检查点（历史失败）

- SDK `10.0.400`；HEAD `5065c38ced05a95088e17d89d96c9a1610e37159`；TFM `net6.0`、`net8.0`。
- Build：`dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1`，退出码 `0`，0 error、118 warnings。
- L4：`dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory <artifacts/final-candidate-release-l4-20261022T091033Z/l4-trx>`，退出码 `1`。未传 runsettings、未过滤测试、xUnit 默认并行保持启用。
- L4 总计：net6.0 为 7,102 项、6,513 Passed、1 Failed、588 NotExecuted；net8.0 为 6,562 项、6,045 Passed、0 Failed、517 NotExecuted。合计 13,664 项、12,558 Passed、1 Failed、1,105 NotExecuted。跳过项不计入通过数。
- 覆盖：159 个 solution 项目全部评估，54 个测试项目形成 103 个 TFM 运行单元，103 次调用与 103 份 TRX 完整覆盖；其中 3 个资源/共享项目每 TFM 无可发现测试并产出空 TRX。SQLite 本地集成每 TFM 231 Passed、1 evidence-only skip。
- 原 181 项映射：29 项更新到当前 L4 的精确 Passed 结果；148 项保留独立验收的外部 Provider TRX；4 项 SQL Server 事务用例保留其直接 L1 Passed TRX。181 行仍全部 `CLOSED_PASSED`，这是逐项证据状态，不表示当前全量 L4 通过。
- Provider 数据库证据和本任务 Compose 清理证据按未变范围复用；没有启动数据库或外部服务。

## 上一候选证据（历史）

上一失败检查点的 evidence directory：`artifacts/final-candidate-release-l4-20261022T091033Z/`。包含 Release build 与 test 原始 stdout/stderr、103 份 TRX、`run-manifest.json`、`trx-inventory.json`、`solution-test-project-coverage.json`、逐项 skip/failure 清单、候选二进制身份和 181 映射刷新记录。历史 Round 0 L4 仍保留在原目录，单独标识为修复前检查点。

## 授权追加：并发元数据重试测试

本次新增证据位于 `artifacts/cache-retry-followup-20261022T094923Z/`。`baseline-manifest.json` 保存改动前测试源码、生产 resolver 源码及原失败 TRX/清单的 SHA256；`verification-summary.json` 汇总命令、UTC 偏移时间、候选身份、XML 计数和方法级结果。原 L4 失败 TRX 保留在历史目录，并在本目录复制为 `original-net6-failure.trx`。

本次只调整 `EntityMappingCacheIsolationTest`。并发调用按实际观察到的指定元数据异常执行一次显式业务重试；断言所有调用最终共享同一映射、失败数与重试数相等、provider 调用严格两次且后续缓存命中不再加载。独立迟到调用用例在首次失败已传播/移除后再解析，验证后续调用成功。所有同步等待有截止并在 finally 释放、汇合任务；共享生产 resolver 源文件未变化。

| 验证 | net6.0 | net8.0 | 退出码 |
| --- | ---: | ---: | ---: |
| L1 `EntityMappingCacheIsolationTest` | 25/25 通过 | 25/25 通过 | 0 / 0 |
| L2 完整 `Bing.Data.Sql.Tests` | 1,318/1,318 通过 | 1,318/1,318 通过 | 0 / 0 |

在该限定修复阶段，L1/L2 没有失败或跳过项，最终全量当时尚未刷新；上一候选 L4 的 1 项失败保留为历史记录。AOP、Provider、数据库、性能证据按未变范围复用，该阶段没有启动数据库、运行 Provider 或 L4。后续新最终检查点的通过结果见下节。

功能建议是保留 resolver 的失败传播和 key/value 原子失效项移除；重试是否适用由调用方业务策略决定。调用方若选择重试，应对明确可恢复错误做有界的一次显式重试，避免缓存层吞掉首次失败或静默重复业务操作。原始 181 项映射及其计数没有修改，本次失败单独登记，不扩成第 182 项。

当时状态：本次限定实现与 L1/L2 已完成，等待父任务安排 Sol/high 对本项的独立复验；通过后再安排当前候选 Release solution build 与默认 L4。`review.md` 保持独立证据且未修改；正式 clean-source Release evidence 门禁继续为 `BLOCKED`。后续独立复验及新最终检查点见下节。

## 缓存复验后的当前最终检查点

### 检查点与候选身份

独立 Review 为 PASS、`CACHE_RETRY_STATUS=PASS`、`OPEN_ACTIONABLE=0`，FIX-001..004 保持 CLOSED。本次没有新 Fix 或代码编辑。当前候选 HEAD 为 `5065c38ced05a95088e17d89d96c9a1610e37159`，SDK 为 `10.0.400`，已安装 .NET runtime `6.0.36`、`8.0.27` 和 `8.0.30`。相对上一个失败 L4 检查点，缓存测试源码因授权修复而变化；缓存测试源码 SHA256=`122F84838B2DC3499FA0DA3472E3452FBB0FE6453C8EDCE1C8D2332940D5F15C`，与独立 Review 身份相符。生产 resolver SHA256=`BE4E43C6CD015CFCF4B93019D2C960B381BB9A333C285435E59EA2E0A3BD453E`，未变化。Release 输出中的缓存测试 DLL 哈希见新 `run-manifest.json`。

唯一新证据目录为 `artifacts/final-candidate-cache-retry-release-l4-20261022T101104Z/`，包含隔离 runner、原始 build/test stdout/stderr、103 份 TRX、逐项汇总、项目覆盖、key regression 核验、skip 清单、候选身份和 181 刷新记录。

### 环境与命令

沿用上一候选已修正的独立 PowerShell runner。外部 Provider/CAP/cache 的 `RUN_*` 门控显式设为 false；连接与配置环境变量按前缀清除，仅记录键名、不记录值；`RUN_METADATA_PATH` 清除。四个 Provider 集成项目无隐式 runsettings 引用，未向 dotnet test 传 runsettings，不覆盖 xUnit 默认并行设置。SQLite 保持启用。本次未启动或操作数据库容器。

| 检查 | 命令 | 开始 / 结束（+08:00） | 退出码 |
| --- | --- | --- | ---: |
| Release build | `dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1` | 18:13:27.716 / 18:14:05.290 | 0 |
| 默认 L4 | `dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory <新证据目录>/l4-trx` | 18:14:06.109 / 18:15:14.001 | 0 |

Build 0 error、118 warnings。TRX 逐条 `UnitTestResult` 汇总为：

| TFM | 总数 | Passed | Failed | NotExecuted/Skipped |
| --- | ---: | ---: | ---: | ---: |
| net6.0 | 7,103 | 6,515 | 0 | 588 |
| net8.0 | 6,563 | 6,046 | 0 | 517 |
| 合计 | 13,666 | 12,561 | 0 | 1,105 |

159 个 solution 项目全部成功评估；54 个测试项目组成 103 个项目-TFM 对，runner 调用与 TRX 覆盖均为 103/103。97 个项目-TFM 对发现测试，另有三个资源/共享测试项目的六个 TFM 对为空结果；缺失调用和结果为零。SQLite 集成每个 TFM 231 Passed、0 Failed、1 evidence-only skip；AOP 每个 TFM 138/138 Passed；缓存并发失败重试与受控迟到调用两方法在 net6/net8 全部 Passed。`key-regression-results.json` 还核实原取消、source-backed、嵌套、虚代理及回调回归；全量 failure CSV 为空。

完整 1,105 项未执行项目和方法独立列在 `final-local-suite-skips.csv`，包含外部 Provider、缓存/Redis/CAP/FreeSQL 门控及 SQLite evidence-only 项；这些均未计入通过。

### 原 181 项与发布边界

`artifacts/original-181-closure.csv` 仍恰为 181 项，全部 `CLOSED_PASSED`，没有扩成 182 项。刷新本地原失败方法 29 项到当前 L4 精确 Passed TRX；148 项真实 MySQL/SQL Server/MultiProvider 结果复用已独立验收 TRX；4 项 SQL Server 事务用例在默认 L4 未执行，继续引用其真实 SQL Server L1 Passed TRX。缓存追加测试单独记录，不占用原映射行。此前 Provider 测试及专属 Compose 清理证据按未变范围复用，本轮未启动数据库。

当前本地 build 与默认全量 L4 验收通过；本地 skip 项仍未执行。外部 CI 和 IDE 验证未运行。正式 clean-source Release evidence 门禁仍为 `BLOCKED`；本轮未改门禁、未清理工作区、未暂存、提交、推送或发外部通知。

父任务最终核对：独立解析本次 103 份原始 TRX，确认上述分 TFM 计数及两个缓存回归均通过；逐项匹配原 181 行到 15 份当前或复用 TRX 中的测试方法、TFM 与 Passed 结果，未发现缺失或非通过项。当前 AOP 生产/测试、缓存生产/测试、解决方案和 global.json 身份均与本次运行清单一致。此后仅澄清本报告的历史阶段措辞，复用本次构建和全量证据。
