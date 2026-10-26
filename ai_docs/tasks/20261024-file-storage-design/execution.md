<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261024-file-storage-design
AI_EXECUTION_FINISHED_AT: 2026-10-25T13:13:48Z

# 文件治理增强执行报告

## 执行结论

本轮 GOV-01～05 完成：共享管理页面、逐消费者 Outbox 确认和管理重放、历史记录预览归档恢复均进入实际调用链。共 276 项测试执行通过、0 失败，4 项专用环境门控跳过；另有四次真实 CLI 进程和本地浏览器验收通过。OPEN_ACTIONABLE=0；没有对生产库执行迁移。

## 任务信息与计划执行

沿用 20261024-file-storage-design，用户授权继续后续增强；create-plan / execute-plan 已使用。原计划、执行、审查已保存于 evidence/governance/before-*.md，独立 review.md 未改。主代理负责架构、核心、HTTP/UI、集成及最终验证；luna_worker 负责两 ORM、四份 SQL 和共用 MySQL 治理测试。

| 任务 | 状态 | 交付 |
|---|---|---|
| GOV-01 | COMPLETED | 三张治理表、两个 ORM 原子事务及 CAS、SQLite/MySQL 增量升级 |
| GOV-02 | COMPLETED | 冻结消费者集合、持久独立回执、重试上限、管理重放与审计 |
| GOV-03 | COMPLETED | 三类历史预览、保护凭据、全批 CAS 归档、默认列表过滤及恢复 |
| GOV-04 | COMPLETED | `/files` 共享嵌入页面，两 Admin 配置接入，Demo 管理身份与共用上传脚本 |
| GOV-05 | COMPLETED | 受影响双框架/双 ORM/双 Admin 测试、真实浏览器、部署文档与符号映射 |

## 已完成事项与 API/数据/配置变化

Outbox 在首次投递时持久化消费者名单，每个消费者有独立尝试次数、确认、下次执行时间及错误码。成功确认在其他消费者失败后保留。尝试先落库，确认始终检查版本及有效租约；默认最多 8 次。移除消费者不会自动确认，新消费者通过显式管理重放加入历史事件。仍是至少一次语义，事件 ID 不变，消费者需要自己的持久幂等。

FileGovernanceAdministration 提供范围明确的可信内部服务；HTTP 必须经过 files.manage。重放/归档/恢复使用请求 GUID + 操作者 + 范围 + 规范参数实现幂等，治理审计与状态原子提交。接口包括 `/events`、`/events/{id}/replay`、`/history/preview`、`/history/archive`、`/history/{id}/restore` 和 `/governance-audit`。预览凭据绑定身份/范围，15 分钟过期。返回不含后端租约令牌或签名秘密。

历史范围为成功任务、全部投递事件、无待办条目的完成批次。保留期默认 90 天，配置最低 7 天；操作按 FinishedAt，事件/批次按 CreatedAt。归档只写版本匹配标记，保留源记录、文件、审计和幂等锚点；源版本变化后标记失效。执行时重检全部候选，任一冲突整体回滚。

新增 FileEventDelivery、FileGovernanceAudit、FileHistoryArchive；已有基础库按“增强 → 治理”顺序升级。旧 worker 必须停止后统一发布新协议。默认页面关闭，通过 `FileManagement:Console:Enabled=true` 开启；页面只把令牌保存在内存，上传会话元数据仍按身份保存。`MaxEventDeliveryAttempts`、`HistoryRetentionDays` 可配置。IFileManagementStore 新增 Governance 契约，自定义实现需要一并重编译。

## 测试结果

| 最终矩阵 | 通过 | 失败 | 跳过 |
|---|---:|---:|---:|
| 业务 net6.0 / EF Core + FreeSQL SQLite | 104 | 0 | 2 |
| 业务 net8.0 / EF Core + FreeSQL SQLite | 104 | 0 | 2 |
| 两 Admin SQLite/MySQL 与真实宿主 | 68 | 0 | 0 |
| 总计 | **276** | **0** | **4** |

每框架各跳过一个真实 MinIO 和一个 ClamAV 环境门控测试，本轮未改这些实现。首批 22 项不重复计入总数。四次实际 Program.Main schema / audit-schema 命令成功。专用 MySQL 8.4 容器、卷及测试库均已清理。实际宿主用独立 testhost 验证，防止之前测试的 Moq 动态程序集影响框架扫描。

Playwright 实际验证：20 MiB 文件首片完成后暂停、刷新并重新选文件恢复；业务关联、签名下载、解除关联；消费者回执与重放；历史预览、归档、恢复及审计；普通身份隐藏管理区、令牌未落盘。桌面 1280×900 与手机 390×844 截图人工检查通过；最终身份切换修正另作定向复测。Demo 已停止，截图/测试数据在临时专用目录保留复查。

证据：

- [最终机器汇总](evidence/governance/final-validation.json)
- [完整验证说明与复现命令](evidence/governance/README.md)
- [生产符号 → 测试方法](evidence/governance/test-map.md)
- [UTF-8/XML/JSON/空白检查与 SHA-256](evidence/governance/source-checks.json)
- [使用与部署说明](../../../docs/operations/file-management/governance.md)
- [增量升级说明](../../../docs/migrations/file-management-governance/README.md)

## Build/Typecheck/Lint/Format

两 ORM、Web 模块、两 Admin 和 Demo 随定向构建或测试编译通过；两个共享 JS 文件 node --check 通过。43 个本轮源码/配置/文档文件 UTF-8、XML/JSON 和尾随空白检查通过。已跟踪改动的范围内 git diff --check 通过。

多目标测试误用了同名 TRX，net8 覆盖了 net6 的最终文件；net6 的 104/0/2 由命令终端结果记录，首批 net6 的 22 项 TRX 保留。复现命令改为 LogFilePrefix 避免覆盖，未为重复日志重跑已通过矩阵。

## 计划偏差、基线问题与风险

- 实施中修复 EF6 对 Guid 数组 Contains 表达式的解析（改 List）、嵌入资源的显式逻辑名、手机身份范围长文本溢出；均有直接失败证据及复测。
- 全仓 git diff --check 的其他任务 plan.md 及 EfCoreSqliteUnitOfWorkTestSupport.cs 存在既有尾随空白，本轮保留。原有 .NET 6 / 传递依赖支持告警没有被禁用。
- 逻辑归档不是物理清理；冷存储压缩、自动定时归档、删除审计/幂等记录不在本轮。SourceVersion 标记确保状态变更不会隐藏当前任务。
- HTTP/ORM 验证覆盖迁移批次；浏览器没有重跑 MinIO 直传、迁移批次控制、生产 SSO、其他浏览器或多副本。生产部署、角色授权及共享 DataProtection 密钥仍由目标环境执行。
- Provider/100 GiB 路径未变化，本轮复用既有证据，不声称新的容量/性能验收。没有真实业务迁移。

## Reviewer 注意事项

重点检查事件有效租约 CAS、消费者名单冻结、重放同请求响应丢失、归档全部候选再校验、源版本锁定与恢复 ABA 边界。两 ORM 的每个变更均有真实 SQLite 或 MySQL 调用链；没有用 Mock 代替持久化。旧独立 review.md 保留，不把本报告冒充新独立审核。

## Git 状态与收口

保留所有已有工作区修改；未自动 git commit；未自动 git push；未自动创建 PR；未发送外部通知。最终执行 task-finish --no-notify，完成后停止，不继续无目标 Review/Fix。

## 修改文件

- `components/src/Bing.Biz.FileManagement/FileGovernanceModels.cs`
- `components/src/Bing.Biz.FileManagement/IFileManagementStore.Governance.cs`
- `components/src/Bing.Biz.FileManagement/FileGovernanceAdministration.cs`
- `components/src/Bing.Biz.FileManagement/FileOutboxDispatcher.cs`
- `components/src/Bing.Biz.FileManagement/FileManagementOptions.cs`
- `components/src/Bing.Biz.FileManagement/FileManagementOptions.Enhancements.cs`
- `components/src/Bing.Biz.FileManagement.EntityFrameworkCore/EfFileManagementStore.Governance.cs`
- `components/src/Bing.Biz.FileManagement.EntityFrameworkCore/EfFileManagementStore.Enhancements.cs`
- `components/src/Bing.Biz.FileManagement.EntityFrameworkCore/FileManagementDbContext.cs`
- `components/src/Bing.Biz.FileManagement.FreeSql/FreeSqlFileManagementStore.Governance.cs`
- `components/src/Bing.Biz.FileManagement.FreeSql/FreeSqlFileManagementStore.Enhancements.cs`
- `components/src/Bing.Biz.FileManagement.FreeSql/FreeSqlFileManagementModel.cs`
- `components/src/Bing.Biz.FileManagement.AspNetCore/Bing.Biz.FileManagement.AspNetCore.csproj`
- `components/src/Bing.Biz.FileManagement.AspNetCore/FileManagementControllerBase.Governance.cs`
- `components/src/Bing.Biz.FileManagement.AspNetCore/FileManagementConsoleController.cs`
- `components/src/Bing.Biz.FileManagement.AspNetCore/FileManagementWebRegistration.cs`
- `components/src/Bing.Biz.FileManagement.AspNetCore/Console/index.html`
- `components/src/Bing.Biz.FileManagement.AspNetCore/Console/console.css`
- `components/src/Bing.Biz.FileManagement.AspNetCore/Console/console.js`
- `components/src/Bing.Biz.FileManagement.AspNetCore/Console/uploader.js`
- `modules/admin/src/Bing.Admin/Modules/FileStorageModule.cs`
- `samples/Bing.FileManagement.Demo/Program.cs`
- `samples/Bing.FileManagement.Demo/wwwroot/uploader.js`
- `samples/Bing.FileManagement.Demo/README.md`
- `components/tests/Bing.Biz.FileManagement.Tests/FileGovernanceTests.cs`
- `components/tests/Bing.Biz.FileManagement.Tests/FileGovernanceHttpTests.cs`
- `components/tests/Bing.Biz.FileManagement.Tests/EnhancementResourceTests.cs`
- `components/tests/Bing.Biz.FileManagement.Tests/EnhancementSchemaUpgradeTests.cs`
- `modules/admin/tests/Shared/AdminFileGovernanceTests.cs`
- `modules/admin/tests/Shared/AdminFileEnhancementUpgradeTests.cs`
- `modules/admin/tests/Shared/AdminFileHostIntegrationTests.cs`
- `modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj`
- `modules/admin/tests/Bing.Admin.FreeSQL.Tests/Bing.Admin.FreeSQL.Tests.csproj`
- `docs/migrations/file-management-governance/efcore-sqlite.sql`
- `docs/migrations/file-management-governance/efcore-mysql8.sql`
- `docs/migrations/file-management-governance/freesql-sqlite.sql`
- `docs/migrations/file-management-governance/freesql-mysql8.sql`
- `docs/migrations/file-management-governance/README.md`
- `docs/operations/file-management/governance.md`
- `docs/operations/file-management/admin-integration.md`
- `docs/operations/file-management/admin-integration.example.json`
- `docs/modules/file-storage.md`
- `docs/migrations/file-storage-adoption.md`
