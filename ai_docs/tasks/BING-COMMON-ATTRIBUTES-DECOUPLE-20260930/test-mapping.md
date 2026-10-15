# Admin MySQL/CAP 生产符号与测试映射

| 生产符号或行为 | 测试项目与方法 | 本轮证据 |
| --- | --- | --- |
| FreeSQL `CapUnitOfWorkExtensions.RollbackBoth` 独立清理、异常聚合 | `Bing.Admin.FreeSQL.Tests.CapUnitOfWorkExtensionsTest.RollbackBoth_ShouldContinueAfterCapFailure`、`RollbackBoth_ShouldReturnNoAggregateWhenCleanupSucceeds` | `net6.0` 通过 |
| FreeSQL `CapUnitOfWorkExtensions.BeginTransaction` 预先解析依赖、新建事务失败回滚、既有事务保留及清理异常聚合 | `CapUnitOfWorkExtensionsTest.BeginTransaction_WhenDispatcherIsMissing_ShouldNotOpenDatabaseTransaction`、`BeginTransaction_WhenDatabaseBeginFails_ShouldPreserveAmbientTransaction`、`BeginTransaction_WhenCapBeginFails_ShouldRollbackNewDatabaseTransaction`、`BeginTransaction_WhenExistingDatabaseTransactionFails_ShouldKeepExistingTransaction`、`BeginTransaction_WhenCleanupFails_ShouldAttemptEveryCleanup` | 升级后 `net6.0` 定向 12/12 通过 |
| FreeSQL `CapUnitOfWorkExtensions.Commit` 提交后 Flush 失败 | `Bing.Admin.FreeSQL.Tests.CapUnitOfWorkExtensionsTest.Commit_WhenFlushFails_ShouldPreserveCommittedDatabaseResult` | `net6.0` 通过；证明提交先于 Flush，真实 Provider 故障结果未注入 |
| 共用 `TransactionCleanup.DisposeAndCapture` 释放异常与次数 | `CapUnitOfWorkExtensionsTest.DisposeAndCapture_WhenDisposeFails_ShouldPreserveBothErrors`、`DisposeAndCapture_WithoutEarlierFailure_ShouldReturnDisposeError`、`DisposeAndCapture_WhenDisposeSucceeds_ShouldKeepOriginalError`、`AdminUnitOfWorkCleanupTest.SaveChangesAsync_WhenBusinessAndCleanupFail_ShouldKeepEveryFailure`、`SaveChangesAsync_WhenAllCleanupStepsFail_ShouldPreserveEveryError`、`SaveChangesAsync_WhenCanceledAndDisposeFails_ShouldKeepCancellation` | 完整工作单元经可控 CAP 包装对象及 FreeSQL 工作单元注入回滚/释放故障；不声称真实 MySQL Provider 会自行抛出相同故障 |
| FreeSQL `CapUnitOfWorkExtensions.Flush` 缺失方法诊断及提交后不伪回滚 | `CapUnitOfWorkExtensionsTest.Flush_ShouldRejectTransactionWithoutFlushMethod`、`AdminUnitOfWorkCleanupTest.SaveChangesAsync_WhenFlushFailsAfterCommit_ShouldNotRollback` | `net6.0` 通过；CAP 8 正常提交由下列 Docker 集成测试覆盖 |
| FreeSQL `CapUnitOfWorkExtensions.BeginTransaction` CAP 8 事务适配 | `Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest.Commit_ShouldPersistBusinessDataAndPublishMessage` | MySQL 8.4/CAP 8.0.1/RabbitMQ 4.3 通过 |
| FreeSQL `AdminUnitOfWork.SaveChangesAsync` 成功提交与 CAP 出箱 | `Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest.Commit_ShouldPersistBusinessDataAndPublishMessage` | 真实环境通过 |
| FreeSQL `AdminUnitOfWork.SaveChangesAsync` 业务失败回滚 | `Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest.DuplicateBusinessKey_ShouldRollbackMessage` | 真实环境通过 |
| FreeSQL `AdminUnitOfWork.SaveChangesAsync` 执行中取消 | `Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest.CancellationAfterOutboxAction_ShouldRollbackBoth` | 真实环境通过 |
| EFCore `AdminUnitOfWork.SaveChangesAsync` 代理提交与 CAP 出箱 | `Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest.Commit_ShouldPersistBusinessDataAndDeliverCapMessage` | 真实环境通过 |
| EFCore `AdminUnitOfWork.SaveChangesAsync` 回滚及出箱原子性 | `Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest.DuplicateBusinessKey_ShouldRollbackCapMessage` | 真实环境通过；清理异常另有本地故障注入测试 |
| EFCore `AdminUnitOfWork.SaveChangesAsync` 执行中取消及连接所有权 | `Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest.CancellationAfterOutboxAction_ShouldRollbackBoth` | 真实环境通过 |
| FreeSQL `AdminUnitOfWork.SaveChanges` 直接同步事务与当前事务恢复 | `Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest.SyncSaveChanges_ShouldPersistBusinessAndOutbox`、`SyncSaveChanges_WhenBusinessFails_ShouldRollbackOutbox` | 当前源码组合 TRX 中 2/2 通过 |
| EFCore `AdminUnitOfWork.SaveChanges` 直接同步事务与当前事务恢复 | `Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest.SyncSaveChanges_ShouldPersistBusinessAndOutbox`、`SyncSaveChanges_WhenBusinessFails_ShouldRollbackOutbox` | 当前源码组合 TRX 中 2/2 通过 |
| EFCore `AdminUnitOfWork.TryRollbackAsync` 取消期间清理及异常保留 | `Bing.Admin.EFCore.Tests.AdminUnitOfWorkRollbackTest.TryRollbackAsync_ShouldUseIndependentTokenAndPreserveErrors`、`TryRollbackAsync_ShouldReturnNoAggregateWhenCleanupSucceeds` | `net6.0` 通过 |
| FreeSQL 3.5.311 核心仓储、SQLite Provider、MySQL Provider 版本一致 | `Bing.FreeSQL.Tests` net6.0/net8.0 全部 33 项；`Bing.Admin.FreeSQL.Tests` 本地 23 项；`dotnet list ... package --include-transitive` | 五个 FreeSQL 包均解析到 3.5.311；两个 Admin 宿主默认还原构建通过 |
| 两个 Admin 宿主的专用环境预检 | `eng/ci/tests/Invoke-AdminMySqlCapIntegrationTests.Tests.ps1` | 非法端口、缺门控、预检状态 5 项通过 |

2026-10-03 使用隔离 Docker 容器（MySQL 8.4、RabbitMQ 4.3）执行六项真实测试。诊断文件为 `artifacts/admin-mysqlcap-results/net6.0-20261003T142701992Z/admin-mysqlcap-diagnostic.json`，同目录的两份 TRX 分别记录 EFCore 3/3、FreeSQL 3/3，通过且无失败或跳过。EFCore 连接所有权的最后调整发生在该组合 TRX 之后，调整后的三项真实测试再次 3/3 通过，见 `execution.md` 的 Docker 验收记录；该次复测没有对应 TRX，因此组合 TRX 不代表最终源码的完整联合验收。

本轮新增同步用例后，`net6.0-20261003T151038606Z` 的 EFCore 与 FreeSQL 组合 TRX 各 5/5 通过。中间轮次 `net6.0-20261003T151638527Z` 为 EFCore 5/5、FreeSQL 4/5；唯一失败为消费者 30 秒未回调，业务和出箱持久化断言已通过，该用例在新容器单项复测 1/1。加入事务状态恢复后，当前生产源码对应的 `net6.0-20261003T152807178Z` 两份组合 TRX 再次各 5/5，诊断为 `Passed/Test`。单次投递等待超时仍保留为历史波动证据。

升级至 FreeSQL `3.5.311` 后，`net6.0-20261003T161736646Z` 两份 TRX 分别为 EFCore、FreeSQL 各 5/5，0 失败、0 跳过，诊断为 `Passed/Test`。前一轮 `net6.0-20261003T161216420Z` FreeSQL 为 4/5：出箱已持久化，但消费者等待 30 秒超时；单项复测和完整复跑通过。两只临时容器已移除。上述结果使用 SDK `8.0.425`；锁定 SDK `8.0.424`、外部部署环境及真实 Provider 自身抛出释放异常仍未验证。同步 `SaveChanges()` 的 CAP 原子性由两套直接集成用例覆盖。
