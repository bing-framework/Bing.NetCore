# BING-SQL-RC-FINAL-HARDENING-20260908-001 实施计划

## 0. 计划元数据与边界

- Task ID：`BING-SQL-RC-FINAL-HARDENING-20260908-001`
- 计划日期：2026-09-08
- 优先级：P0（`Bing.Data.Sql` / `Bing.Dapper` 7.0.0 Release Candidate 最终门禁）
- 当前源码：分支 `dev_v6.0-refactor-sqlquery`，HEAD `de1b7d9da516e621b5d2aedd42715c4c247f2f11`
- 规划时工作树：clean；`git status --porcelain=v1 --untracked-files=all` 无输出
- 正式计划路径：`ai_docs/tasks/BING-SQL-RC-FINAL-HARDENING-20260908-001/plan.md`
- 前置任务：`BING-SQL-RC-HARDENING-20260906-001`、`BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001`
- 工具链：Windows、PowerShell、.NET SDK `8.0.424`、xUnit、Dapper、BenchmarkDotNet `0.14.0`、AppVeyor
- 本轮 `create-plan` 只新增本计划及必要目录，不修改业务源码、测试、配置、数据库或构建文件，不执行 Git add/commit/push/merge/PR。

本任务不是继续扩展 SQL 功能，而是把已经实现的 7.0.0 RC 收敛为可重复、可审计、fail-closed 的发布结论。历史报告只能作为回归线索；最终 `RC READY` 必须来自当前 clean commit、同一受保护 CI build identity 和同一 EvidenceSessionId。

## 1. 需求解释与范围

用户只给出“FINAL HARDENING”任务标识，没有新增功能规格。结合最近两轮任务和当前源码，本计划将目标解释为：

1. 关闭 `BING-SQL-RC-HARDENING-20260906-001` 留下的真实发布阻塞，而不是重做已经通过的 API、Provider Capability 和缓存工作。
2. 让受保护 AppVeyor 正向工作流真实可达，并且失败时明确区分仓库缺陷、环境缺失和外部数据库不可达。
3. 在当前 HEAD 上重新生成 Unit、Analyzer/Public API、SQLite、三种外部 Provider、FormalHost 和 aggregate 的同源证据。
4. 只有全部 P0 门禁满足时才输出 `RC READY`；否则保持 `RC NOT READY`，不得用历史、dirty、DryJob、零执行或手工拼接制品放行。

### 1.1 明确不在本任务内

- 不新增 Query、Lambda、Mutation、Transaction、Streaming、Multiple Result 或 Provider 公共能力。
- 不再次改名或增加同义公共 API；当前 Transaction 显式 overload 与 `ToEntity` 决策保持冻结。
- 不把 Oracle/Doris 提升为核心 RC blocker；两者继续按 non-blocking / NotExecuted / Unsupported 的真实状态报告。
- 不降低 TLS、数据库名、reset 授权、secret scope、Public API Analyzer 或 Evidence validator 的安全门槛。
- 不使用生产数据库，不在仓库中写入连接串，不自动执行 Git 发布操作。

若执行中发现必须修改公共 API、Provider 能力语义或 SQL 输出才能通过门禁，应停止该扩展并另立设计任务；不得把新的 Breaking Change 混入最终验证任务。

## 2. 当前真实状态与完成度

### 2.1 已真正实现并通过职责级验证

1. Query、Raw SQL、Lambda、Mutation、Transaction、Streaming、Multiple Result 与 Provider Profile 的真实调用链已存在，不是接口、Stub 或 Mock 骨架。
2. PostgreSQL 已明确区分文本 SQL Function 与 `Procedure()`/OUT：当前 Profile 对 Procedure/OUT fail-fast，不再以 Function result set 伪装过程输出能力。
3. Transaction factory 已收敛为显式 overload，公共参数使用 `dataSourceKey`；七个生产项目统一将 `RS0016/RS0017/RS0018/RS0026` 作为错误。
4. Provider runner、SQLite runner、Evidence CLI、TRX/sidecar/hash/session/source validator、RC validation script 和 AppVeyor lane matrix 均已存在。
5. RC Unit 清单已覆盖 11 个直接测试项目；最近一轮报告中核心项目双 TFM、七个生产项目 Release build 和 Analyzer 均通过。
6. `BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001` 的独立 Review 为 `PASS`：实体映射、模型元数据、Mutation Plan、Getter 和有界 Lazy 缓存已有键隔离、命中/未命中、容量、LRU、异常恢复及并发直接测试。
7. 缓存生产修复会在 faulted `Lazy` 后做条件删除，并仅在最终映射 cache miss 时串行化构造；命中路径保持无锁。它不是临时兼容层，但属于需重新测量的热路径变化。
8. `docs/sql/**`、数据库集成测试说明、Public API 治理和“最终生产符号 -> 测试方法”追溯文档已经存在。

### 2.2 部分完成或尚未形成发布证据

| 优先级 | 状态 | 证据 | 发布影响 |
| --- | --- | --- | --- |
| P0 | 当前 HEAD 尚无同源 ReleaseEvidence | 9 月 6 日 Provider/Unit/Analyzer/FormalHost 报告绑定旧源码身份或 dirty source；当前 HEAD 为 9 月 8 日缓存提交。 | 历史 PASS 不能证明当前 RC。 |
| P0 | MySQL 双 TFM 未真实执行 | 最近报告为每个 TFM `59 discovered / 0 executed / 59 auth failures`，根因位于专用测试环境 SSL/RSA/认证策略。 | 四核心 Provider 的 8/8 运行集合不完整。 |
| P0 | AppVeyor aggregate lane 的跨 job 输入未闭环 | `appveyor.yml` materialize 六个 matrix lane，但 `aggregate` 默认读取本 job 工作区目录；仓库中没有可证明的跨 job 下载/fan-in 步骤。`release` lane 则在单 job 内已具备完整串行聚合路径。 | 独立 aggregate job 可能必然缺少生产 job 制品，真实 positive workflow 尚未证明。 |
| P0 | FormalHost 必须在当前 HEAD 重跑 | 136/136 旧组合已完成，但 manifest 报告的源码身份为旧提交/dirty；此后实体映射缓存 admission/concurrency 路径发生生产修改。 | 旧性能结果只能做历史参考，不能作为当前发布门禁。 |
| P0 | 受保护 CI 正向链未执行 | `Invoke-ReleaseCandidateValidation.ps1` 强制 `CI=true`、`APPVEYOR_BUILD_ID`、clean source 和 provider-scoped secrets；本地不能伪造发布通过。 | 当前最终状态仍是 `RC NOT READY`。 |
| P1 | 脚本编排自动化验证不完整 | Provider runner 有 `-SelfTest/-ValidateOnly`，RC `-SelfTest` 主要覆盖 FormalHost manifest；未发现对 AppVeyor lane/fan-in 和完整阶段 fail-closed 编排的职责级自动测试。 | 配置漂移可能直到真实 CI 才暴露。 |
| P1 | 文档存在旧口径 | `docs/sqlquery-usage.md` 仍使用较多 `dbKey`/DefaultConnection 表述；`docs/integration-testing.md` 仍描述旧 runsettings/global gate 回退。 | 用户可能绕过当前 provider-scoped、安全、显式配置路径。 |

### 2.3 完成度判断

- SQL/Dapper 功能与公共 API：约 95%，已冻结；本任务不计划扩展。
- Provider Capability 语义与职责级测试：约 90%，核心语义已收敛，欠当前 clean source 的真实外部执行。
- 缓存正确性与并发测试：约 95%，独立 Review 已 PASS；欠当前提交性能重新定基。
- CI/Evidence 代码基础：约 85%，fail-closed 组件齐全；欠真实 AppVeyor 拓扑闭环与 protected positive run。
- 发布性能证据：约 70%，批准集合和验证器齐全；旧 136/136 结果不属于当前 HEAD，也没有可比较 Delta。
- 最终 RC 状态：`NOT READY`。直接阻塞项为 MySQL 安全环境、当前 HEAD 的 8/8 Provider ReleaseEvidence、当前 HEAD FormalHost 以及同 session aggregate 正向结果。

## 3. 质量、API 与维护性评估

### 3.1 性能与资源

- 映射缓存命中路径仍为无锁读取；cache miss admission lock 避免同键重复执行昂贵映射构造，但不同键同时 miss 会串行化。必须用现有 `SqlMetadataBenchmarks` 的命中、冷 miss、容量/LRU 场景复测吞吐和分配。
- Mutation Plan/Getter、Lambda、Debug SQL、SQLite E2E 已有 BenchmarkDotNet 场景；不在没有对比数据时引入池化、Span、unsafe 或新的缓存层。
- Provider/SQLite runner 必须验证 reader/connection/transaction 释放及 cancellation；不以仅 pre-cancel 的 Happy Path 代替执行期资源合同。

### 3.2 API 与兼容性

- 根 Query 六入口、Transaction overload、`ToEntity` 终结命名与 Provider SPI 已冻结；参数、返回值和可见性不在本任务调整。
- `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` 和 RS0016/17/18/26 是最终合同来源；禁止用 `NoWarn`、pragma、SuppressMessage 或兼容 facade 绕过。
- 预计无 Breaking Change，因此无需迁移层。若最终验证发现公开合同必须变化，应新建任务并给出迁移策略。

### 3.3 复杂度、耦合与冗余

- 现有 Evidence CLI/runner 已承担身份、哈希、TRX、能力矩阵和 fail-closed 校验；不得创建第二套报告/验证框架。
- 当前主要冗余是 AppVeyor 的 provider/release/aggregate 多入口与单 job RC orchestration 并存。应选定一个权威发布拓扑，避免两个可产生“最终矩阵”的入口相互漂移。
- `Invoke-ReleaseCandidateValidation.ps1` 集中编排 11 个 Unit 项目、8 个 Provider/TFM 运行、Analyzer/API gate、FormalHost 和 aggregate，修改时优先抽取可测试的薄职责，不做与最终门禁无关的大重构。

## 4. 关键实施决策

1. **单一发布权威路径**：推荐以 AppVeyor `release` lane 的同 job 串行链为唯一 `RC READY` producer，因为它天然共享 clean checkout、build identity、secret scope、EvidenceSessionId 和本地制品目录。独立 provider lanes保留为诊断；`aggregate` lane 若没有显式可靠的跨 job artifact download/fan-in，应移出发布必需矩阵或改为只消费经过哈希、session、source 校验的下载制品，不能假设 matrix job 共享文件系统。
2. **MySQL 安全修复优先在测试环境完成**：为专用测试账号配置与当前驱动兼容的 TLS/RSA/caching_sha2_password 策略。不得在代码中关闭证书校验、允许明文认证、回退生产/DefaultConnection 或放松专用库名和 reset 授权。
3. **所有最终制品重新生成**：历史 Unit、Provider、SQLite、Analyzer/API 和 FormalHost 报告不复制进新任务目录；同一个 protected release job 从当前 clean HEAD 生成全部证据。
4. **性能判定不伪造 Delta**：优先选取同硬件、SDK/runtime、BenchmarkDotNet 配置、filter 和参数集的可信历史 FormalHost 作为 baseline。若没有同 key baseline，则报告 `NOT COMPARABLE`，但仍要求当前 136/136 完整性；是否允许缺 Delta 放行必须由既有发布策略明确，不能由执行者临时降低门槛。
5. **失败分类与最终结论分离**：环境缺失可标记 `BLOCKED`，代码/测试失败标记 `FAILED`；两者都不能生成 `RC READY`。

## 5. 分阶段实施任务

## Phase A：冻结当前基线与发布拓扑

### FINAL-001（P0）建立当前 Task 的不可混用证据边界

- 目标：将当前 clean HEAD、分支、SDK/runtime、TaskId、RunId、EvidenceSessionId 和历史证据边界记录为后续唯一来源。
- 现状/证据：HEAD 为 `de1b7d9d`；9 月 6 日报告与 FormalHost 属于旧源码/dirty source，9 月 7 日缓存报告在提交前生成，均不是当前 Task ReleaseEvidence。
- 修改范围：新增本 Task 的 `execution.md` 和 `artifacts/**/BING-SQL-RC-FINAL-HARDENING-20260908-001/**`；不得覆盖旧任务制品。
- 已确认文件：`global.json`、`version.props`、`common.props`、`Bing.All.sln`、本计划、两项前置任务的 plan/execution/review。
- 候选文件：本 Task 的基线 JSON/Markdown、artifact index；仅在现有 RC script 尚未输出等价元数据时新增。
- 实施步骤：记录 Git identity 和 clean 状态；记录 SDK/runtime/OS；为制品统一 TaskId/RunId/session/source；把旧制品标记 Historical 或 BaselineCandidate；拒绝复制旧 PASS 作为新 PASS。
- 依赖：无。
- 验证：`git status --porcelain=v1 --untracked-files=all`、`git rev-parse HEAD`、`git branch --show-current`、`dotnet --version`、`dotnet --info`。
- 风险：测试运行产生未跟踪制品后使 source dirty；发布 job 应在身份校验后将输出限制到被忽略的 `artifacts/`，并在聚合前再次验证 source identity。
- 验收标准：所有最终制品均可追溯到同一 Task、clean commit 和 session；历史制品无法进入当前 aggregate 的 PASS 输入。

### FINAL-002（P0）收敛 AppVeyor 发布 lane 与 artifact fan-in

- 目标：使受保护发布正向链在仓库配置上真实可达，并且只有一个权威 `RC READY` producer。
- 现状/证据：`appveyor.yml` 已列出 `common/mysql/postgresql/sqlserver/release/aggregate`；`release` 单 job 可串行生成全部输入，独立 `aggregate` 默认却读取当前 job 目录，未见跨 job下载步骤。
- 修改范围：AppVeyor matrix/脚本入口、RC orchestration 的最小契约测试；不修改 SQL 业务行为。
- 已确认文件：`appveyor.yml`、`eng/ci/Invoke-ReleaseCandidateValidation.ps1`、`eng/ci/Invoke-ProviderIntegrationTests.ps1`、`eng/ci/Invoke-SqliteContractTests.ps1`、`eng/Bing.ProviderEvidence.Cli/Program.cs`。
- 候选文件：`framework/tests/Bing.Test.Shared/**` 或新的 CI 配置契约测试文件；如选择跨 job fan-in，则增加仓库内明确的下载/校验薄脚本。
- 实施步骤：选择并文档化单 job release 或真实跨 job fan-in；推荐移除“无下载却聚合”的必需 lane，保留 provider lanes 诊断；确保 common lane清空所有外部 secret；确保 release lane 使用 `ci-$APPVEYOR_BUILD_ID` 和 HEAD source identity；对缺 secret、缺 artifact、错 session、错 hash、错 source、零执行和失败建立负向契约。
- 依赖：FINAL-001。
- 验证：PowerShell AST parse；`& .\eng\ci\Invoke-ProviderIntegrationTests.ps1 -SelfTest`；`& .\eng\ci\Invoke-ReleaseCandidateValidation.ps1 -SelfTest`；新增 CI 配置/编排职责级测试；真实 AppVeyor 页面必须出现预期 lane 且 release job 不依赖其他 matrix job 的本地目录。
- 风险：AppVeyor secret 对 PR/fork 不可用属于预期；这些 job 应 fail-closed 或仅在受保护分支触发，不得回退公共连接配置。
- 验收标准：仓库配置不存在必然缺输入的 aggregate 路径；唯一权威 release job 可从 clean checkout 走到 aggregate；任一输入异常均非零退出且不生成 READY。

## Phase B：关闭环境阻塞并验证基础合同

### FINAL-101（P0）修复 MySQL 专用测试环境认证并完成预检

- 目标：在不降低安全性的前提下，让 MySQL net6.0/net8.0 测试真正连接专用数据库并执行。
- 现状/证据：最近运行两个 TFM 均发现 59 个测试，但在 SSL/RSA/caching_sha2_password 认证阶段全部失败，执行数为 0。
- 修改范围：优先为受保护 AppVeyor MySQL 专用账号、证书/公钥与 provider-scoped secret；只有诊断确有不足时才最小修改 preflight/脱敏报告和对应测试。
- 已确认文件：`docs/testing/database-integration-tests.md`、MySQL Integration fixture/数据库脚本、Provider runner 的 startup diagnostic。
- 候选文件：MySQL fixture 的可诊断错误分类测试、集成测试文档；连接串本身不得写入仓库。
- 实施步骤：确认驱动版本和服务器认证插件；验证专用测试库名及 reset 授权；配置受保护 secret；先运行 `-ValidateOnly`，再执行双 TFM；将认证、TLS、不可达、库名不安全分类输出为无密诊断。
- 依赖：FINAL-002；需要具备授权的非生产 MySQL 测试环境。
- 验证：`RUN_MYSQL_INTEGRATION_TESTS=true`、`ALLOW_DATABASE_RESET_FOR_TESTS=true` 和 `ConnectionStrings__MySqlConnection` 仅由 protected secret 注入；分别调用 Provider runner 的 net6.0/net8.0；TRX 必须 `Discovered>0, Executed>0, Failed=0, CoreSkipped=0`，可选跨库 skip 必须命中白名单并有原因。
- 风险：环境修复不在仓库权限范围内；无法获得安全账号时，本 Task 必须保持 BLOCKED，而不是修改客户端绕过 TLS/RSA。
- 验收标准：MySQL 两个 TFM 生成当前 session/source 的 ReleaseEvidence；日志、TRX sidecar 和制品不包含连接串、主机、用户名、密码或 token。

### FINAL-102（P0）验证当前 HEAD 的 Unit、缓存、Analyzer/API 与 SQLite 基线

- 目标：证明 9 月 8 日缓存提交在两个目标框架和本地真实 SQLite 路径上无回归。
- 现状/证据：缓存任务的全量双 TFM与 Review 已 PASS，但其执行报告不是当前 clean commit 的发布 session；RC script 已列出 11 个 Unit 项目。
- 修改范围：测试/脚本仅在发现真实缺口时修改；任何生产修复必须同步职责级测试和追溯矩阵。
- 已确认文件：11 个 `$unitProjects`、`Bing.Data.Sql.Tests` 缓存测试、七个 PublicAPI baseline、SQLite Integration、`ai_docs/sql-metadata-test-traceability.md`。
- 候选文件：本 Task unit/analyzer/API/SQLite 报告及 sidecar；若失败则为对应职责测试与最小生产文件。
- 实施步骤：restore 一次；Release build；执行 RC Unit 全清单；重点连续运行并发缓存用例至少 3 轮；生成 RS0016/17/18/26 inventory 和 API gate；运行 SQLite 双 TFM真实合同；维护最终生产符号到测试方法映射。
- 依赖：FINAL-001。
- 验证：`dotnet restore .\Bing.All.sln`；`dotnet build .\Bing.All.sln -c Release --no-restore --nologo`；`dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --no-restore --nologo` 及 `net8.0`；由 RC script 执行其余 10 项目、Analyzer/API gate 和 SQLite双 TFM。
- 风险：并行构建共享 `obj` 会造成文件锁；最终证据采用顺序、可重复运行。SQL 输出测试如需更新必须断言完整字符串。
- 验收标准：11 项目所有适用 TFM 0 failed；并发专项三轮稳定；七项目四类 Analyzer 均 0；PublicAPI 基线一致；SQLite 双 TFM真实执行且资源释放/取消/事务合同通过。

## Phase C：生成当前源码的核心发布与性能证据

### FINAL-201（P0）执行四核心 Provider 双 TFM protected release run

- 目标：从一个 clean、受保护、同 session 的 job 生成 SQLite/MySQL/PostgreSQL/SQL Server 共 8 个唯一 ReleaseEvidence。
- 现状/证据：PostgreSQL、SQL Server、SQLite 旧本地运行已证明主链可执行，但均非当前 Task ReleaseEvidence；MySQL仍阻塞。
- 修改范围：Provider test/evidence 代码仅在真实运行暴露缺陷时修改；外部数据库继续由专用变量门控。
- 已确认文件：三个外部 Provider Integration 项目、SQLite Integration、三个 CI runner、Provider Capability Catalog/validator/CLI。
- 候选文件：受影响 Provider 的职责级测试、fixture、无密诊断和追溯行。
- 实施步骤：release job 开始时固定 session/source；逐 Provider 清除非目标 gate/connection；执行 net6/net8；校验发现/执行/失败/核心 skip、TRX、sidecar、二进制 hash、数据库/驱动版本、时间窗口；任何失败停止后续 READY 生成但保留 BLOCKED/FAILED 诊断。
- 依赖：FINAL-101、FINAL-102。
- 验证：在 protected AppVeyor 上执行 `& .\eng\ci\Invoke-ReleaseCandidateValidation.ps1 -Configuration Release -ResultsRoot artifacts/release-candidate`；直接 runner 用于问题隔离，不作为另一条发布入口。
- 风险：fixture 会重置专用测试库；必须继续校验安全库名和 `ALLOW_DATABASE_RESET_FOR_TESTS=true`。SQL Server optional multi-provider skip 与 MySQL跨库 skip 只能按既有白名单接受。
- 验收标准：8/8 运行唯一、同 session/source、`Executed>0`、`Failed=0`、`CoreSkipped=0`；每个 RealIntegrationProven scenario 绑定真实 passed test method，不存在全 skip 假通过。

### FINAL-202（P0）重跑当前 HEAD FormalHost 并作可比性判定

- 目标：覆盖最新实体映射缓存热路径，形成完整且不混入 DryJob 的正式性能证据。
- 现状/证据：旧 136/136 组合使用 FormalHost 3 launch/6 warmup/15 iteration，但属于旧 source；最新改动可能影响不同键 miss 并发和映射构造成本。
- 修改范围：优先只生成 BenchmarkDotNet 制品和 manifest；只有确认关键回归且有剖析证据时才修改生产代码/Benchmark。
- 已确认文件：`framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`、`SqlMutationBenchmarks.cs`、Lambda/Debug/SQLite/Aggregate benchmark 类、RC FormalHost manifest validator。
- 候选文件：若现有 Metadata 场景不能区分命中、单键并发 miss 和不同键并发 miss，可最小补充对应 benchmark 及基线说明。
- 实施步骤：先通过 CI/E2E smoke；在同一 clean release job 执行九个批准类型；从真实 CSV 校验类型集合、Job 和迭代参数；与同环境同参数可信 baseline 比较；记录 Mean、Allocated、分布警告和回归阈值来源。
- 依赖：FINAL-102；正式结论应与 FINAL-201 使用同一 session/source。
- 验证：RC script 的 FormalHost 阶段；manifest 必须覆盖批准的 136 个组合，所有 CSV 行均为 `Job=FormalHost`、`LaunchCount=3`、`WarmupCount=6`、`IterationCount=15`，并校验 SHA-256。
- 风险：完整运行耗时较长且机器噪声显著；不得以一次均值或不同环境数据判定回归。无可比 baseline 时如实标记 `NOT COMPARABLE`。
- 验收标准：当前 HEAD 的 FormalHost manifest 完整；缓存命中/冷 miss/容量/LRU 无关键回归，或给出阻断及证据；DryJob 只作 smoke，不进入正式结论。

## Phase D：聚合、文档一致性与最终判定

### FINAL-301（P0）生成 fail-closed aggregate 与制品索引

- 目标：让唯一 Evidence CLI aggregate 同时验证 Unit、8 个 Provider/TFM、Analyzer/API、FormalHost 和源码身份。
- 现状/证据：CLI 已验证 session/source/hash/执行计数等；当前缺完整 protected positive inputs，且独立 aggregate lane 拓扑存疑。
- 修改范围：现有 Evidence CLI、validator、直接单测及本 Task artifact index；不创建第二套矩阵格式。
- 已确认文件：`eng/Bing.ProviderEvidence.Cli/Program.cs`、`framework/tests/Bing.Test.Shared/ProviderReleaseEvidence*.cs` 与测试、RC script aggregate 调用。
- 候选文件：针对当前拓扑新增的 artifact contract/direct test；最终 matrix、Markdown、hash manifest、artifact index。
- 实施步骤：只消费当前 release job 的显式目录；校验 8/8 唯一 run、session/source、TRX/sidecar/binary hash、unit inventory、RS/API gate 和 FormalHost manifest；增加或复跑缺失/重复/篡改/旧 session/错误 source/零执行负向测试。
- 依赖：FINAL-201、FINAL-202。
- 验证：`dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net6.0 --no-restore --nologo` 与 `net8.0`；RC script 最终 CLI 调用；对 matrix 和 artifact index 做 SHA-256 重验。
- 风险：把环境 BLOCKED 错写为 PASS，或把历史目录意外纳入递归扫描；输入根目录必须显式且绑定本 Task/session。
- 验收标准：所有正向输入满足时 `ReleaseReady=true`；任一必需输入缺失、重复、失败、跳过、篡改或异源时稳定为 false/非零退出。

### FINAL-302（P1）收口文档、追溯与旧口径

- 目标：让用户文档与最终 Provider gate、命名和安全配置一致，并留下可审计的生产符号映射。
- 现状/证据：`docs/sql/**` 和权威集成测试文档已更新；`docs/sqlquery-usage.md`、`docs/integration-testing.md` 仍包含 `dbKey`、旧 runsettings、全局 gate 或 DefaultConnection 回退口径。
- 修改范围：文档、Release Notes、追溯矩阵；只有实际行为变化时才更新公共 API baseline。
- 已确认文件：`docs/sql/README.md`、`docs/sql/provider-capabilities.md`、`docs/sql/migration-guide.md`、`docs/testing/database-integration-tests.md`、`ai_docs/sql-metadata-test-traceability.md`。
- 候选文件：`docs/sqlquery-usage.md`、`docs/integration-testing.md`、`docs/ReleaseNotes.md`、本 Task artifact index/execution report。
- 实施步骤：以当前代码和 runner 为唯一事实源；统一 `dataSourceKey`、`.local` 显式本地加载、provider-scoped gate/secret、非生产库/reset 和 PostgreSQL Function/Procedure 差异；追加本 Task 最终生产符号 -> 测试方法 -> 证据路径映射。
- 依赖：FINAL-201、FINAL-301。
- 验证：使用 `rg` 检查旧变量/回退描述；逐项核对命令与脚本参数；检查所有 Markdown 为 UTF-8 且链接目标存在。
- 风险：示例中的历史 API 可能是特定上下文而非错误；修改前追踪真实调用链，不做全局机械替换。
- 验收标准：文档不承诺不存在的回退或 Provider 能力；每个本轮最终生产符号都有直接测试方法和证据路径；无连接秘密。

### FINAL-303（P0）执行最终 RC Gate 并给出唯一结论

- 目标：基于本 Task 的当前 clean commit 同源证据给出 `RC READY` 或精确的 `RC NOT READY`。
- 现状/证据：当前唯一合法结论是 `RC NOT READY`；旧 final report 不得覆盖。
- 修改范围：本 Task `execution.md`、最终报告、验证报告和 artifact index；不自动发布 NuGet 包。
- 已确认文件：本计划、RC script、aggregate matrix 及所有本 Task 制品。
- 候选文件：`artifacts/reports/BING-SQL-RC-FINAL-HARDENING-20260908-001-final-report.md`、机器可读 summary JSON。
- 实施步骤：复核 Git/source/session；执行完整 protected release；逐门禁记录 PASS/FAILED/BLOCKED；复核无密和 hash；生成最终报告；由独立 Reviewer 对照 plan/execution/source/diff/evidence 审查。
- 依赖：FINAL-001 至 FINAL-302 全部完成。
- 验证：`git diff --check`；完整解决方案 Release build；RC protected command；PowerShell AST/self-test；独立 `review-code BING-SQL-RC-FINAL-HARDENING-20260908-001`。
- 风险：外部数据库、AppVeyor secret 或性能环境仍可能阻塞；不能因接近发布日期而降低门禁。
- 验收标准：仅当所有 P0 gate、8/8 Provider runs、Analyzer/API、Unit、SQLite、FormalHost、aggregate、无密检查和独立 Review 同时通过时写 `RC READY`；否则报告必须列出失败阶段、Provider/TFM、发现/执行/失败/skip 数、环境状态、原因和所需外部动作。

## 6. 验证矩阵

| 门禁 | 最低要求 | 发布证据 |
| --- | --- | --- |
| Source | clean HEAD；CI commit 与 HEAD 一致；同一 `ci-$APPVEYOR_BUILD_ID` | source identity + session |
| Build | `Bing.All.sln` Release 0 error | build log |
| Unit | RC 清单 11 项目所有适用 TFM 0 failed | TRX + unit summary |
| Cache concurrency | net6/net8 连续 3 轮稳定；命中/未命中/异常恢复合同通过 | direct test logs |
| Analyzer/Public API | 七项目 RS0016/17/18/26 均 0；baseline 一致 | inventory JSON/MD + logs + API gate |
| SQLite | net6/net8 真实执行；0 failed/core skip | 2 份 TRX/sidecar/evidence |
| MySQL | net6/net8 真实执行；0 failed/core skip | 2 份 TRX/sidecar/evidence |
| PostgreSQL | net6/net8 真实执行；Function/Procedure 语义一致 | 2 份 TRX/sidecar/evidence |
| SQL Server | net6/net8 真实执行；optional skip 仅白名单 | 2 份 TRX/sidecar/evidence |
| FormalHost | 当前 HEAD，批准集合 136/136，3/6/15，无 DryJob 混入 | CSV/MD/HTML/log + manifest/hash |
| Aggregate | 8/8 唯一运行；全部输入同 session/source/hash；fail-closed | capability matrix + artifact index |
| Security | provider-scoped secret；专用非生产库；显式 reset；日志无密 | startup diagnostic + redaction check |
| Review | 独立审查无 MUST_FIX/SHOULD_FIX | `review.md` PASS |

## 7. 计划级风险与回退策略

1. **外部权限风险**：MySQL/AppVeyor secret 配置需要用户或 CI 管理员权限。仓库内工作完成但权限缺失时，结论为 BLOCKED，不以本地模拟替代。
2. **数据库破坏风险**：所有 fixture 只能操作通过安全库名校验的专用测试库；缺少显式 reset 授权时立即退出。
3. **性能可比性风险**：环境、SDK/runtime 或 benchmark 参数不同则不计算 Delta；保留原始 CSV 与机器信息。
4. **CI 成本风险**：完整 FormalHost 较慢。诊断 lanes 可并行，但最终 READY producer 保持单 session、可追溯；不得因节省时间只跑子集。
5. **修复引入变更风险**：若执行阶段修改任何生产代码，必须回到受影响 Task 重新执行直接单测、SQLite、Provider、Benchmark 和 aggregate；旧制品全部失效。
6. **回退策略**：CI/脚本修改可回退到上一个已知 fail-closed 版本，但回退后仍只能输出 NOT READY；禁止回退 validator、安全门槛或公共 API 来换取通过。

## 8. Definition of Done

本任务仅在以下条件全部成立时完成：

- 当前 clean commit 的完整 protected release workflow 在真实 AppVeyor 上执行；
- MySQL 认证问题在安全专用环境中解决，四核心 Provider 双 TFM 共 8/8 真实运行通过；
- Unit、缓存并发、SQLite、Analyzer/Public API、FormalHost 和 aggregate 都绑定同一 session/source；
- AppVeyor 不再存在假设 matrix job 共享目录的发布必需路径；
- 最新映射缓存热路径有当前 HEAD FormalHost 证据和明确的可比性/回归结论；
- 最终生产符号到测试方法的追溯矩阵与文档已同步；
- 最终报告无秘密、可重验 hash，并经独立 Review 判定 PASS；
- 未自动执行 commit、push、merge、PR 或包发布。

若任一条件未满足，任务报告应保持 `PARTIAL/BLOCKED` 或 `FAILED`，RC 结论保持 `NOT READY`。
