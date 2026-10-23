# 多数据库工作单元 Sample 最终交付

日期：2026-10-21。任务：20261021-sql-uow-sample。

**实施与独立验收 PASS，T01–T07 全部完成。** GPT-6-Luna/max 实施，GPT-6.1-Sol/high 独立审查；经过 1 轮修复、2 轮审查，FIX-001 与 FIX-002 全部 CLOSED，OPEN_ACTIONABLE=0。本任务已批准的本地验收范围 RELEASE_STATUS=PASS，GOAL_STATUS=COMPLETED。

## 运行与审查入口

在仓库根目录运行默认 Dapper 模式：

```powershell
dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj
```

启用 EF 与 Dapper 共用 orders 事务：

```powershell
dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -- --SqlUow:Storage=EfBridge
```

默认地址 http://localhost:5000。完整操作说明见 [Sample README](../../../samples/Bing.Samples.SqlUnitOfWork/README.md)，可执行请求见 [SqlUnitOfWork.http](../../../samples/Bing.Samples.SqlUnitOfWork/SqlUnitOfWork.http)。每次运行创建独立 orders.db/reporting.db；正常停止并释放宿主后清理本次目录。

## 已验证能力

- HTTP、真实 BackgroundService 队列、真实 Hangfire Server/MemoryStorage 三种入口均执行相同业务服务。Worker 每条消息创建 scope，Hangfire 使用作业 DI scope；队列不传递 scoped 对象。
- 应用层和 orders 仓储分别构造函数注入 Query，具名工厂访问 reporting。跨层共享同源事务，交错构造的查询 SQL、参数及结果独立；完整 SQL 与参数字典均有直接断言。
- 同库与双库提交/回滚、捕获 SQL 失败后 rollback-only、同 root 内 100 次工厂 Query 创建/释放、EF SaveChanges 与 root Complete 的边界均通过独立连接的准确行集核对。
- 两个只读 HTTP 请求的实际活动 scope 重叠达到 2；验证真实 manager、Query、连接及 EF context 的身份与寿命。一方结束后，另一方仍能执行查询。该观测不表示两条 SQL 命令同时执行。
- Worker/Job 连续操作的真实 scoped 资源独立；正常任务结束后的 worker 退出、端口关闭、Hangfire Server 注销及数据库目录删除均有证据。

## 最终验证

| 验证 | 结果 |
| --- | --- |
| Sample net6 Release 构建 | 退出码 0，0 错误 |
| Sol 新鲜 net6 Release 完整 Sample 套件 | 8/8 passed，0 failed，0 skipped |
| Sol 新鲜 net8 Release 完整 Sample 套件 | 8/8 passed，0 failed，0 skipped |
| 无测试 hook 的正常宿主探测 | Dapper/EfBridge 两宿主、6 个实际 Hangfire Job，状态与正常关闭/清理符合预期，退出码 0 |
| 静态检查 | git diff --check 通过；已有 SDK 目标/RID 提示保留 |

Sample 按计划仍编译为 net6；net8 指配套测试宿主。Reviewer 核对修复范围和全部 Sample/Test 文件身份，审查前后源码未变化。原始日志、命令、TRX 和并发观测见 [最新证据清单](artifacts/review-round-2/manifest.md)。报告收口只同步任务文档，复用已验收且未改变的源码证据，没有重新跑全仓测试或 Benchmark。

## 边界与交付记录

双 SQLite 只验证本地多数据源能力，各库独立事务，不保证跨库原子提交；Hangfire MemoryStorage 不持久化任务。EF 示例使用明确的 POCO 映射，未验收完整模块的软删除、审计等能力。清理证据针对正常操作结束后的 Stop/Dispose，不扩展为在途任务强杀或队列排空保证。

没有新增框架公共 API、调整版本/API baseline/性能预算，也没有暂存、提交、推送或创建 PR。保留 F008、前置 Uow 实现及用户已有修改。本 Sample 的 PASS 不替代前置框架任务仍单独记录的异构数据库与正式性能门禁。

- [批准计划](plan.md)
- [执行与修复报告](execution.md)
- [独立审查报告](review.md)
- [符号到测试映射](traceability.md)
- [全部证据索引](artifacts-manifest.md)
