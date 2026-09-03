# 执行进度

## 任务
- Task ID：`BING-SQL-RELEASE-HARDENING-20260831`
- 状态：`PARTIAL`
- 分支：`dev_v6.0-refactor-sqlquery`
- 基线 HEAD：`ee4688dedf3ef7c11efbd78989c596aac5f6529b`

## 已完成
- [x] 读取并执行批准的 `plan.md`，未重新规划整体方案。
- [x] 注册 `plan-execution` 任务状态。
- [x] 记录 Git、系统、SDK 和 Runtime 基线；当前 `global.json` 声明 SDK `8.0.424`，本机解析一致。
- [x] 创建并更新 Unit、Integration、Benchmark 和 Final Report。
- [x] 增加 `SqlProviderTransactionCapabilities` 三项 native async flags 及 Profile 深复制。
- [x] 按驱动反射证据更新 MySQL、PostgreSQL、SQL Server、Oracle 的事务能力声明；SQLite 不声明 native async。
- [x] 完成异步事务 Adapter 的 native/fallback、Task/ValueTask、预取消和原始异常解包路径。
- [x] 完成事务租约执行模式保存、并发安全读取以及 After/Error 诊断刷新。
- [x] 完成 Lambda 多源 optional SPI、默认 Clause 桥接和 `SqlLambdaQueryCore` 调用迁移。
- [x] 补充 Profile、SPI、事务 Adapter 和 SQL Server 替身诊断直接测试。
- [x] 更新 `PublicAPI.Unshipped.txt` 和 `ai_docs/sql-metadata-test-traceability.md`。
- [x] Round 5 收紧 `ProviderCapabilityEvidenceState`、`ProviderCapabilityMatrix`、真实集成元数据和 `ProviderContractRunner`；执行委托与固定状态互斥，`TestGenerated` 不满足发布放行。
- [x] Round 5 将 SQLite 真实执行入口写入双 TFM JSON Matrix，并生成独立 runner/SQLite TRX。
- [x] Round 5 同步 Unit、Integration、Benchmark、Final Report 和数据库集成测试文档的当前证据边界。
- [x] Round 5 补齐 `SqlTransactionScopeFactory` 新增参数 XML 文档；Dapper Core 当前 SDK 构建为 `0 warning/0 error`。
- [x] 目标文件编辑器诊断为 `No errors found`；`git diff --check` 通过。
- [x] `REL-P6-02` 补齐 ProfileMissing、ProfileMismatch、ProviderImplementationGap 和 DatabaseUnsupported 的直接分类测试与主要入口校验。
- [x] `REL-P8-01` 完成职责审计；确认现有 partial/职责文件边界足够，未进行机械拆分，public API 与 SQL 输出保持不变。
- [x] 本轮验证 Data.Sql Profile 专项 net6.0/net8.0 各 `11/11`、Dapper Core 能力门禁专项 net6.0/net8.0 各 `69/69`，Data.Sql 与 Dapper Core Release Build 均 `0 warning/0 error`。

## 未完成或部分完成
- [ ] 完整 Provider Integration Contract、所有 Provider 的六态 Capability Matrix 和外部 Provider 当前 TRX。
- [ ] Oracle 安全集成 fixture 和 SQL Server 深度真实合同。
- [ ] MySQL/PostgreSQL/SQL Server/Oracle/Doris 真实 Provider 矩阵，以及外部 Procedure/Cancellation/Multiple Result 合同。
- [ ] Public API Analyzer、完整解决方案 Build/Test 和 Release Gate。
- [ ] 本任务 FormalHost Benchmark、同 key before/after 原始制品和性能报告数据。
- [ ] 独立 Reviewer 再次审查；`review.md` 保持 Reviewer 原始 `NEEDS_FIX` 证据，未被修改。
- [ ] `REL-P2-03` SQL Server CRUD/Batch/Transaction/Procedure/Multiple Result/Cancellation 真实合同仍缺少授权外部环境。
- [ ] `REL-P5-02` PostgreSQL/SQL Server/Oracle Procedure 真实矩阵仍未执行；MySQL 仅保留已有真实 Output/InputOutput 证据。

## 阻塞
- [x] 重新确认当前 `global.json` 声明 SDK `8.0.424`，本机实际解析一致；不修改用户现有 SDK 配置。
- [x] Round 5 runner 双 TFM 各 `6/6`、Round 9 SQLite 合同双 TFM 各 `1/1` 已在当前 SDK `8.0.424` 执行；Data.Sql/Dapper Core 全量单元和生产项目构建亦已完成。
- [ ] 外部 Provider 需要专用数据库、Provider gate、安全 reset 和授权；本轮未读取连接配置、未连接数据库。
- [ ] FormalHost 同 key before/after Benchmark、外部 Provider 真实合同和完整 Release Gate 仍未完成。

## 证据规则
- 静态 Profile、编辑器诊断、DI 检查和测试替身不计为真实 Provider 通过。
- 共享 runner 只有成功执行且携带完整真实集成元数据时才产生 `RealIntegrationProven`；固定状态不得伪造该状态。
- SQLite Matrix 由合同测试生成，包含 Provider/数据库/驱动版本、真实连接类别、测试方法、TRX、artifact、开始/完成 UTC 时间和源码身份；当前 `ArtifactKind=TestGenerated`，`ReleaseReady=false`。
- Round 5/9 runner 和 SQLite 合同已生成独立当前源码身份 TRX，均使用当前 `global.json` 声明的 `8.0.424`。
- 默认 Skip、ValidateOnly、runner self-test 和历史报告不计为本任务通过。
- 未自动执行 `git add`、`git commit`、`git push`、PR、tag、release、reset 或 clean。
