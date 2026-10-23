<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261022-slnx-migration
AI_EXECUTION_FINISHED_AT: 2026-10-22T03:57:05Z

# 实施执行报告

## 执行结论

主解决方案已从工作区版本 Bing.All.sln 迁移到 Bing.All.slnx，SDK、CI、测试根定位与当前使用文档已同步。T01–T04 的实施项完成，语义比较、SDK 10.0.400 Debug/Release 构建、删除旧入口后的裸构建和根定位测试均通过。

终态为 PARTIAL：主方案 Release 测试命令退出码为 1。TRX 有 154 个 MySQL/SQL Server 测试在关闭门控时仍执行，并在建立数据库会话前失败；Logging 的 24 个失败由 SDK 8 对照复现；AOP 和 EventBus 的全套运行失败在 SDK 8/10 定向复测中均未复现。跳过/未执行测试另计，不作为通过。未运行远端 CI。

## 任务信息

- Task ID：20261022-slnx-migration
- Plan：ai_docs/tasks/20261022-slnx-migration/plan.md（未修改）
- 基线提交：5065c38ced05a95088e17d89d96c9a1610e37159
- SDK：迁移前基线 SDK 8.0.424；候选 SDK 10.0.400，global.json 设置 rollForward=latestPatch、allowPrerelease=false。
- T01 快照以迁移前工作区字节为来源，不以 HEAD 覆盖已有变更。Bing.All.sln 快照为 113372 字节，SHA-256 54ea62ae4316d93479a7c3d9eeab3ad1767f7bb872cdc995de708cdb192957dd；相对 HEAD 已包含的 21 行及新增 Uow Sample 均保留。

## Change Impact Analysis

在 T02 开始前记录候选影响；兼容修正发生后追加受影响文件及范围，后续候选验证均在 CIA 后执行。

| 维度 | 影响分析 |
| --- | --- |
| ChangedFiles | 根 Bing.All.slnx、global.json、DocFX/Travis/AppVeyor 配置、ProviderReleaseEvidenceValidatorTest.cs、BingModuleManager.cs 与 6 个缓存测试文件的 Reverse 方法绑定、当前使用文档和派生 docs/index.md；比较器、静态检查器、快照、日志与 TRX 均位于本任务 artifacts。根 Bing.All.sln 在比较和新方案构建通过后移除。 |
| ChangedProjects | Bing.Core 及 Bing.Caching.Tests、Bing.Caching.CSRedis.Tests.Integration、Bing.EasyCaching.Tests 只将存在 SDK 10 重载歧义的 Reverse 调用显式绑定为 Enumerable.Reverse，维持 SDK 8 的 LINQ 语义；测试根定位属于 Bing.Test.Shared；主方案覆盖迁移时工作区中的 159 个项目和 63 个文件夹。 |
| ChangedPublicContracts | 无；未修改公共 API、API baseline、NuGet 版本或目标框架。 |
| ChangedRuntimePaths | 测试仓库根定位兼容 SLNX 并保留旧 SLN 检测；CI 在专用 dotnet 根安装 SDK 10 与 net6/net8 的 Microsoft.NETCore.App、Microsoft.AspNetCore.App，并检查 DOTNET_ROOT、PATH 与跨阶段持久性。 |
| ChangedProviders | 不改变 Provider lanes、secret 隔离或 gate 参数。 |
| ChangedTFMs | 无；保留 net6/net8 目标框架。 |
| ChangedBuildPackaging | 主解决方案序列化格式改变，须保持项目、文件夹、Solution Items、配置映射和构建选择语义。 |
| ChangedBenchmarkHarness | 无；不运行性能测试或更改性能预算。 |
| ChangedDocsOnly | 同步当前 README、贡献指南、样例、cheatsheet、开发、测试、工程化、架构及配方说明，并按既有流程同步 docs/index.md。 |
| AffectedDependents | dotnet sln/MSBuild 方案解析、Bing.Core 反向模块停止顺序、缓存拓扑测试、Bing.Test.Shared 根定位、CI restore/build/test、DocFX 和开发者 IDE/SDK 说明。 |
| RiskLevel | HIGH：全方案入口与全局工具链改变，触及 159 个项目；按计划验证迁移前 SDK8、SDK10旧方案、SLNX Debug/Release、SQLite及最终入口。 |

## 计划执行情况

| 计划项 | 状态 | 结果 |
| --- | --- | --- |
| T01 保存迁移起点 | COMPLETED | 保存工作区快照、Git/SDK/运行时和旧方案语义基线；SDK8 Release characterization 通过。 |
| T02 统一构建 SDK | COMPLETED | global.json、DocFX、Travis、AppVeyor 使用 SDK10.0.400，并保证 net6/net8 Core 与 ASP.NET Core runtime 在专用安装根可见。SDK10旧方案构建在必要的 Reverse 显式绑定后通过。 |
| T03 官方转换与语义核对 | COMPLETED_WITH_IDE_METADATA_DIFFERENCE | 官方迁移命令生成 SLNX；比较状态为 PASS_BUILD_SEMANTICS_WITH_IDE_METADATA_DIFFERENCE。项目/构建语义一致；旧 SolutionGuid 不由 serializer 表示，风险已记录。 |
| T04 切换入口及兼容引用 | COMPLETED | CI、测试根定位、当前文档更新；新方案构建和比较通过后删除根旧 SLN；删除后裸构建与 Bing.Test.Shared net6/net8 测试通过。 |

## 已完成事项

- T01 基线文件清单与字节快照在 artifacts/baseline/t01-manifest.json 和 artifacts/baseline/worktree_snapshot/。记录基线 Git、SDK/运行时及方案列表的命令输出在 artifacts/baseline/。
- T01 迁移前 SDK8命令：dotnet build Bing.All.sln -c Release --nologo -v minimal -m:1 -nr:false，exit 0；日志为 artifacts/logs/sdk8-baseline-sln-release-build-confirmation2.log，命令记录为同名 JSON。构建摘要为 129 warnings、0 errors。
- T02 将 global.json 固定至 SDK 10.0.400/latestPatch/allowPrerelease=false；CI 静态验证确认 3 份 YAML 可解析，DocFX 与 Travis Bash 块由 Git Bash 解析，AppVeyor before_build/build_script/test_script 由 PowerShell AST 解析。
- T02 SDK10旧方案完整 Release 构建最终命令 dotnet build Bing.All.sln -c Release --nologo -v minimal -m:1 -nr:false，exit 0，203 warnings、0 errors。首次 CS0023 与之后缓存测试 Reverse 重载歧义均保留首因日志；SDK8 对应 Bing.Core 和缓存测试定向编译通过。只作显式 Enumerable.Reverse 绑定，没有改业务包、analyzer、门禁或框架。
- T03 执行官方 dotnet sln Bing.All.sln migrate，exit 0。结构化比较器可重复运行，命令记录及结果分别在 artifacts/logs/t03-solution-semantic-comparison-final.json 和 artifacts/baseline/t03-sln-vs-slnx-semantic-comparison.json；最终状态 PASS_BUILD_SEMANTICS_WITH_IDE_METADATA_DIFFERENCE。
- 比较器确认项目路径集合 159/159 相同，文件夹层级 63/63 相同，Solution Items 28 项一致，无显式项目依赖差异。Debug|Any CPU 与 Release|Any CPU 映射均为项目自身配置、Build 启用、Deploy 禁用；SLNX 省略的 ActiveCfg/Build.0/Deploy.0 按其默认值比较。文件夹 GUID 映射为 SLNX 文件夹路径。无缺失或额外项目、文件夹、Solution Items 或依赖。
- 元数据差异已明确分析：旧 HideSolutionNode=FALSE；SLNX 无显式表示，默认显示 solution node，语义相同。旧 SolutionGuid={C1202A0F-83CC-4602-BCE5-20CB640BCAD4}；SLNX serializer 不保留该 IDE/extension identity，可能需要重新识别依赖旧 GUID 的集成。仓库当前代码/配置无此 GUID 消费者，证据在 artifacts/logs/reference-search-post-delete.json。报告不宣称零差异。
- SLNX 验证命令及结果：dotnet sln Bing.All.slnx list，exit 0；dotnet restore Bing.All.slnx，exit 0；dotnet build Bing.All.slnx -c Debug --no-restore -m:1，exit 0；dotnet build Bing.All.slnx -c Release --no-restore -m:1，exit 0。结果分别在 artifacts/logs/slnx-list.json、slnx-restore.json、slnx-debug-build-m1-retry.json、slnx-release-build-m1.json。
- Debug 首次并行尝试遇到 CS2012 输出文件锁；检查目标文件的 Restart Manager 未发现持有进程，不停止 Sample。首次串行重试因 PowerShell 将未引用的 -m:1 拆分为参数而报 MSB1031；修正参数传递后通过。失败记录和锁调查日志保留在 artifacts/logs/。
- 语义比较及新 SLNX Debug/Release 构建均通过后，仅删除根 Bing.All.sln；转换源和备份均为迁移前工作区文件。独立 templates/CodeSmith/Bing? 解决方案未修改，CodeSmith 的 .sln 保留原样。
- T04 ProviderReleaseEvidenceValidatorTest 的根定位同时识别 Bing.All.slnx 与 Bing.All.sln。删除旧入口后根裸构建 dotnet build -c Release --no-restore exit 0；Bing.Test.Shared Release net6.0 与 net8.0 两次测试各 91/91 passed、0 skipped、exit 0。
- SQLite 本地集成结果：Bing.Dapper.Sqlite.Tests.Integration 每个 TFM 231 passed、1 skipped；Bing.Samples.SqlUnitOfWork.Tests 每个 TFM 12 passed、15 gated skip。SQLite 单元测试每个 TFM 112 passed。SKIP/NotExecuted 均不计入 passed。
- 所有验证子进程显式关闭 RUN_INTEGRATION_TESTS、Provider 专用数据库 gate、CAP/MySQL gate、cache fault/large/migration gate 等变量，并清除外部连接字符串与 CAP RabbitMQ 配置；无生产连接配置输出或远端 CI/job/deploy。
- CI 修改仅为 .github/workflows/docfx.yml、.travis.yml、appveyor.yml。DocFX 用官方 setup-dotnet SDK10.0.400，再在同一 dotnet 根安装 net6/net8 Core 与 ASP.NET runtime；Travis/AppVeyor 在专用路径用微软官方安装脚本安装 SDK/runtime，设置 PATH/DOTNET_ROOT 并持久化后续步骤所需环境。原 Provider lanes、secrets 隔离、门控和命令参数保持。
- 静态 CI 检查：python ai_docs/tasks/20261022-slnx-migration/artifacts/validate_ci_config.py，exit 0；原始结果 artifacts/logs/ci-static-validation-final.json/.log。初次使用 System32 WSL bash shim 的静态校验失败，改用已安装 Git Bash 对相同脚本作解析后通过；CI workflow 本身未触发。
- IDE 文档区分格式与工具链要求：官方 MSBuild 文档说明 MSBuild 17.12 起可构建 SLNX，计划及 VS 页面基线为 Visual Studio 17.14；SDK/MSBuild/VS 配套矩阵列明 SDK10.0.400 最低 Visual Studio 18.0，feature-band ship-with 为 18.9。官方来源：[Customize solution build](https://learn.microsoft.com/en-us/visualstudio/ide/customize-solution-build) 和 [SDK, MSBuild, and Visual Studio versioning](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)。本机 SDK10.0.400、MSBuild 18.9.6、VS 18.6.11822.322：达到 SDK10 IDE最低版本但不是配套版本；IDE 集成未验证。证据 artifacts/logs/local-ide-evidence.json/.log。
- 文档搜索确认 .agents 与 eng 无旧主方案硬编码，活动文档已更新。docs/plans/chinese-comments-plan.md 第77行及 docs/plans/chinese-comments-execution.md 第64–66行仍记录旧命令；它们是 2026-09-01 已完成的历史计划/执行记录并含历史验证日志，不是当前可执行指南，因此保留。搜索及判断证据在 artifacts/logs/reference-search-post-delete.json/.log。docs/index.md 按既有脚本生成并检查通过。
- 任务 Sample 未被停止。最终 CIM/健康检查显示 PID 101852，命令运行 Bing.Samples.SqlUnitOfWork.dll，监听 127.0.0.1:5088；/diagnostics/events 返回 HTTP 200、11010 bytes。证据 artifacts/logs/uow-sample-final-state.json。

## 部分/未完成事项

- 主方案 Release 全量测试未达到无失败：dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory H:\Bing_Framework\Bing.NetCore\ai_docs\tasks\20261022-slnx-migration\artifacts\test-results\solution-release，exit 1。精确命令参数、门控变量和退出码在 artifacts/logs/slnx-release-tests.json；原始 TRX 位于 artifacts/test-results/solution-release/。
- TRX 合计 net6：7066 total、6506 passed、90 failed、470 NotExecuted；net8：6526 total、6036 passed、91 failed、399 NotExecuted。合计 13592 total、12542 passed、181 failed、869 NotExecuted。NotExecuted 为跳过项，不能算通过。逐项目与 fully-qualified 名称/首因在 artifacts/logs/slnx-release-trx-summary.json、slnx-release-test-failure-details.json 和 slnx-release-test-failure-summary.log。
- BLOCKED_EXTERNAL / 未门控历史失败：MySQL 集成测试 net6/net8 各 59 项，共 118 项，均在 MySqlConnector 建立连接时报告 Couldn't connect to server；SQL Server 集成测试各 18 项，共 36 项，在 SQL Server pre-login handshake 期间失败。检查失败栈，未建立成功数据库会话、未执行查询/写入；外部连接配置已清除且未使用生产库。这些测试即使 Provider gate 关闭仍由未门控 Fact 执行，因此不是 skip，也不计 PASS。按要求没有 SDK8重跑或再次触发外部连接。
- 既有配置失败：Bing.Logging.Serilog.Tests.Integration net6/net8 各 12 项失败，首因是注册期缺少 IConfiguration。SDK8对照中两个 TFM 的同一测试类各 12 项仍失败，归类为当前既有问题。SDK8对照运行的是 SDK10生成的 Release 程序集并使用 --no-build/--no-restore，所以比较的是 SDK选择下的测试执行，不是 SDK8重编译对照。版本前后证据为 artifacts/logs/sdk8-probe-version-before.json 与 sdk10-version-after-sdk8-probe.json。
- AOP Cancellation_ShouldNotCommit（net6/net8）与 EventBus OrderedEventConsumerTest.HostedWorker_WhenHandlerConstructionKeepsFailing_ShouldBackoff（net8）在主套件各失败一次；SDK8和SDK10精确过滤器复测均通过，故记为全套运行中出现但聚焦不可复现，不宣称根因已确定。对应探针记录为 artifacts/logs/sdk8-aop-net6.json、sdk8-aop-net8.json、sdk8-eventbus-net8.json、sdk10-aop-net6-recheck.json、sdk10-aop-net8-recheck.json、sdk10-eventbus-net8-recheck.json。
- 门控跳过与未执行共 869 项，包含 CSRedis/FreeRedis/cache fault、Doris/Oracle、EasyCaching、FreeSQL、MailKit、Admin、CAP/样例等。各项目计数见 slnx-release-trx-summary.json；跳过与上述未门控外部失败分列。
- 远端 DocFX、Travis、AppVeyor workflow 未执行；仅做 YAML、Bash、PowerShell AST 本地静态检查。因此 CI 云端安装、缓存及实际跨 job runtime 可见性仍待远端验证。
- IDE 集成只核对了本机版本与 Microsoft 支持矩阵，未在 Visual Studio 中打开并验证 SLNX 工作流。

## 修改文件

- 主入口与工具链：Bing.All.slnx（由工作区根 Bing.All.sln 官方迁移生成）；删除根 Bing.All.sln；global.json。
- CI：.github/workflows/docfx.yml、.travis.yml、appveyor.yml。
- 源码/测试：framework/src/Bing.Core/Bing/Core/Modularity/BingModuleManager.cs；framework/tests/Bing.Caching.Tests/CacheClusterTopologyTest.cs；framework/tests/Bing.Caching.CSRedis.Tests.Integration/ShardedCacheTest.cs；framework/tests/Bing.EasyCaching.Tests/EasyClusterFailureIntegrationTest.cs、EasyClusterIntegrationTest.cs、EasyClusterRestartIntegrationTest.cs、EasyClusterScanRestartIntegrationTest.cs；framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs。
- 当前文档：README.md、README.en.md、CONTRIBUTING.md、samples/README.md、samples/Bing.Samples.Winform/README.md、docs/cheatsheet.html、docs/getting-started/使用文档.md、docs/operations/工程化文档.md、docs/operations/测试指南.md、docs/operations/资产索引.md、docs/modules/development.md、docs/architecture/架构文档.md、docs/architecture/功能完成度.md、docs/guides/recipes/README.md、派生 docs/index.md。
- 本任务证据：ai_docs/tasks/20261022-slnx-migration/artifacts/ 下的工作区快照、方案语义比较器与结果、CI 静态检查器、日志/JSON 命令记录、TRX 和测试摘要。
- templates/CodeSmith/CodeGenerator.sln 的独立解决方案未修改。历史任务日志、baseline 与 docs/plans 历史记录未做全局替换。

## API/数据/配置变化

- 公共 API、目标框架、NuGet 版本、API baseline、Provider 语义和性能预算无变化。
- global.json 将仓库默认 SDK 固定到 10.0.400、latestPatch、禁用 prerelease。
- SDK10 新增 Reverse 重载歧义的源码位置显式限定 Enumerable.Reverse；其顺序行为与 SDK8 下原 LINQ 绑定一致。
- 解决方案格式变更唯一明确差异是未序列化的 IDE SolutionGuid 元数据；HideSolutionNode 的默认行为等价。无数据库 schema 或历史数据操作。

## 测试结果

| 验证 | 命令/结果 | 证据 |
| --- | --- | --- |
| 迁移前 SDK8 Release baseline | dotnet build Bing.All.sln -c Release --nologo -v minimal -m:1 -nr:false；exit 0 | artifacts/logs/sdk8-baseline-sln-release-build-confirmation2.json/.log |
| SDK10旧方案 Release | dotnet build Bing.All.sln -c Release --nologo -v minimal -m:1 -nr:false；最终 exit 0 | artifacts/logs/sdk10-old-sln-release-unlocked-after-compat-fixes.json/.log |
| SLNX 列表/restore | dotnet sln Bing.All.slnx list；dotnet restore Bing.All.slnx；均 exit 0 | artifacts/logs/slnx-list.json、slnx-restore.json |
| SLNX Debug/Release build | dotnet build Bing.All.slnx -c Debug --no-restore -m:1；Release 对应命令；均 exit 0 | artifacts/logs/slnx-debug-build-m1-retry.json、slnx-release-build-m1.json |
| 旧入口移除后的裸构建 | dotnet build -c Release --no-restore；exit 0 | artifacts/logs/root-bare-release-build.json/.log |
| Bing.Test.Shared net6/net8 | 两个 TFM 各 91 passed、0 skipped，exit 0 | artifacts/logs/shared-net6-post-delete.json、shared-net8-post-delete.json；TRX 在 artifacts/test-results/ |
| 主方案 Release 测试 | dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory H:\Bing_Framework\Bing.NetCore\ai_docs\tasks\20261022-slnx-migration\artifacts\test-results\solution-release；exit 1 | artifacts/logs/slnx-release-tests.json/.log 及 TRX |
| CI 配置静态检查 | python ai_docs/tasks/20261022-slnx-migration/artifacts/validate_ci_config.py；exit 0 | artifacts/logs/ci-static-validation-final.json/.log |
| 文档索引/引用扫描 | python eng/ci/sync-docs-index.py --check；exit 0；引用扫描无活动入口旧主方案引用 | artifacts/logs/docs-index-check-post-delete.json、reference-search-post-delete.json |
| Diff whitespace check | git diff --check；exit 0 | artifacts/logs/git-diff-check-final.json/.log |

最终生产符号到测试方法的追溯：BingModuleManager.Close 的反向停止顺序 -> Bing.Core.Tests.Modularity.ModuleLifecycleTest.InitializesOnce_AndStopsInReverseOrder；CacheClusterTopologyTest 相关路径 -> GetLocalMasterId_ReadsMyselfMasterRegardlessOfRowOrder、SlotOwnershipSnapshot_DetectsSameMasterSetMigration、EnsureSameView_RejectsDivergentMasterViews；ShardedCacheTest -> TwoShards_PrefixListAndStreamRemainConsistentAcrossBothNodes；EasyCaching Redis cluster failure/restart/scan 路径 -> RedisCluster_Failover_RebuiltManagerReadsAndClearsAllSlots、RedisCluster_CrossSlotBatchStreamRemoveAndClear_PreservesNamespaceIsolation、RedisCluster_MasterRestart_RebuiltManagerRecoversAllSlots、RedisClusterScan_MasterRestartDuringHeldResponse_PropagatesFailure；FindWorkspaceRoot -> ProviderReleaseEvidenceValidatorTest.Write_WhenCurrentSuccessfulArtifactsMatch_ShouldCreateTrustedReleaseEvidence，删除旧入口后的 Bing.Test.Shared net6/net8 全类测试各 91/91 通过。

## Build/Typecheck/Lint/Format

- .NET 主方案 Debug/Release、迁移前 SDK8 baseline、SDK10旧方案、裸根构建均已按上述结果执行。无独立 typecheck、lint 或 formatter 项适用于本方案。
- docs/index 检查与 git diff --check 均 exit 0。git diff --check 仅显示既有未相关 ai_docs/tasks/20261021-merged-code-audit/final-report.md 的 CRLF->LF 提示，没有 whitespace error；该文件未由本任务编辑。
- 未运行性能 benchmark，符合计划范围。

## 计划偏差

- 解决方案语义比较不是字节级或全元数据零差异。Build/project/folder/solution item/configuration/dependency 语义一致；SolutionGuid 未被 serializer 表示，仓内未发现消费者，作为 IDE identity 风险继续迁移并在本报告明示。
- 由于 SDK10 把 Reverse 解析到新标量/字符串扩展重载而造成编译失败，按父线程批准在 Bing.Core 单行及六个缓存测试文件中限定 Enumerable.Reverse；SDK8对应项目编译通过，SDK10 局部构建与旧方案完整构建随后通过。
- 计划“本地测试无新增失败”无法整体确证：主套件包含未门控数据库测试和配置失败，故终态为 PARTIAL；按要求不关闭或修改无关测试，也未对 MySQL/SQL Server 做 SDK8复跑。
- Debug 并行构建文件锁与 PowerShell 参数拆分错误均通过锁调查和正确参数重试解决，未中止 Sample。未远端运行 CI，也未声称 IDE 集成已验证。

## 基线问题

- SDK8 old-SLN Release baseline 全构建通过。SDK10 old-SLN 初始出现 Reverse 编译绑定错误；逐项目 SDK8 对照通过，完成最小显式绑定后 SDK10 old-SLN 全构建通过。首因原始记录：artifacts/logs/sdk10-baseline-sln-release-build.json/.log 与 sdk10-old-sln-release-after-bing-core-fix.json/.log。
- 初始并行构建/测试中的文件锁记录保留于 baseline 及 logs；单节点构建确认最终通过。T01工作区文件状态和新增 Sample 以快照为参照，不把 HEAD 当作转换源。

## 已知问题

- 整套 Release 测试当前非绿色；181 个失败包含 154 个门控未生效的外部数据库连接失败、24 个 SDK8 可复现的 Logging 配置失败和 3 个在单测过滤重跑时未复现的 AOP/EventBus 失败。分类与限制详见“部分/未完成事项”，不要将 869 NotExecuted 汇报为通过。
- CI 仅本地静态解析；外部 workflow、runtime 下载后的云环境及实际 hosted runner 未验证。
- SLNX 不承载旧 SolutionGuid；GUID绑定的外部 IDE 扩展需要自行重新识别方案。仓库内部搜索未发现依赖。

## 风险与回归关注点

- 独立验收应复核工作区来源的 159 项方案比较结果、SolutionGuid 元数据差异及 CI 安装根路径/PATH/runtime 配置。
- 后续若运行完整外部 Provider 测试，必须通过对应 gate 和明确提供的非生产测试环境；本任务未访问生产数据库，也未执行 CAP 远端作业。
- 保留现有 Sample 服务：PID 101852、127.0.0.1:5088、HTTP 200；本次收尾未停止。

## Reviewer 注意事项

- 独立验收由父线程另行委派；本执行器不撰写独立 review.md，也不自报独立 PASS。
- 先查看 semantic JSON 的 status 字段与 IDE 元数据差异，再查看完整 TRX 与 failure-details；不要把迁移/build 的通过扩展成整套 test PASS。
- 重点检查未暂存工作区中的既有 F008/Uow/CAP/Sample 内容仍在；本任务未用 HEAD 替换方案，也未改变 Git index。

## Git 状态

- 未执行 git add、commit、push、reset、clean、checkout 或 restore；不创建 PR。index 保持原状。
- 工作区原先已包含 F008/Uow/CAP/Sample 等未提交改动；本任务只在已批准的文件范围实施，并将当前 Bing.All.sln 字节备份于 artifacts/baseline/worktree_snapshot/ 后删除根旧入口。
- 根 Bing.All.sln 现已删除，根目录 Bing.All.slnx 是唯一主方案；CodeSmith 独立 .sln 保持原样。
- 最终 git diff --check exit 0；当时工作树无本任务以外的清理操作。未自动 git commit、未自动 git push、未自动创建 PR。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/20261022-slnx-migration/review.md`

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：`.travis.yml`
- 根因：Travis 仍使用 Ubuntu Focal，而 .NET 10 支持矩阵要求 Jammy 或更新版本。
- 修复：将 `dist: focal` 改为 `dist: jammy`；保留现有 lane、SDK/runtime 安装、PATH 检查、测试范围、命令参数和 secrets/gates。
- 验证：
  - `python ai_docs/tasks/20261022-slnx-migration/artifacts/validate_ci_config.py`：PASS，exit 0；YAML 与 Travis Bash 静态解析通过。
  - `rg -n "^(dist|os):|^dist:" .travis.yml`：PASS，exit 0，显示 `dist: jammy`。
  - `rg -n -i focal .travis.yml`：PASS，无匹配，rg exit 1 为预期。
- 远端 Travis job 未运行，仍为 `BLOCKED_EXTERNAL`。

#### FIX-002

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：`samples/README.md`、`docs/getting-started/API速查索引.md`、`docs/migrations/README.md`
- 根因：当前文档仍将 SDK 8.0.424 描述为 `global.json` 锁定版本。
- 修复：仅将三处现行 SDK 版本更新为 10.0.400；其它样例与文档内容保留。
- 验证：
  - `rg -n -F 8.0.424 samples/README.md 'docs/getting-started/API速查索引.md' docs/migrations/README.md`：PASS，无匹配，rg exit 1 为预期。
  - `python eng/ci/sync-docs-index.py --check`：PASS，exit 0，`docs/index.md` 与 `README.md` 一致。
  - `git diff --check`：PASS，exit 0；仅提示既有未相关 `ai_docs/tasks/20261021-merged-code-audit/final-report.md` 的 CRLF 转换，无 whitespace error。

#### FIX-003

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：`CONTRIBUTING.md`
- 根因：贡献指南错误地称 Payments 测试不在主方案内，并要求常规情况下重复运行。
- 修复：说明 `Bing.Biz.Payments.Tests` 已纳入主方案；常规全量测试无需重复单跑，仅在定向开发或排查该项目测试时使用该工程的 `dotnet test` 命令。
- 验证：
  - `rg -n -F components/tests/Bing.Biz.Payments.Tests/Bing.Biz.Payments.Tests.csproj Bing.All.slnx`：PASS，exit 0，项目路径位于 SLNX 第 189 行。
  - `Get-Content -Raw -Encoding utf8 ai_docs/tasks/20261022-slnx-migration/artifacts/logs/slnx-release-trx-summary.json | ConvertFrom-Json | Where-Object { $_.Assembly -eq 'Bing.Biz.Payments.Tests.dll' } | Format-Table Framework,Assembly,Total,Passed,Failed,NotExecuted -AutoSize`：PASS，exit 0；net6.0、net8.0 各为 11 项、10 passed、0 failed、1 NotExecuted。原始 TRX：`artifacts/test-results/solution-release/jianx_DESKTOP-8CUU71G_2026-10-22_11_35_37_net6.0.trx` 与 `..._11_35_36_net8.0.trx`；已直接解析两份 XML，结果一致。
  - `python eng/ci/sync-docs-index.py --check`：PASS，exit 0。
  - `git diff --check`：PASS，exit 0；无 whitespace error。

### Round 1 汇总

- MUST_FIX：1；SHOULD_FIX：2；已完成：3。
- PARTIAL：0；BLOCKED：0；FAILED：0。
- 本轮只运行配置、文档与证据核对；没有重跑主方案 Release 测试。原主方案 Release 测试仍为 exit 1、181 failed、869 NotExecuted，原始 `PARTIAL` 结论保留。
- 远端 DocFX、Travis、AppVeyor job 仍未运行；本记录不构成独立 Review PASS。下一步：由独立 Reviewer 重新审查。
