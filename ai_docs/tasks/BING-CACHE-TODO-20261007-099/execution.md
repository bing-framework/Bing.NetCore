<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-CACHE-TODO-20261007-099
AI_EXECUTION_FINISHED_AT: 2026-10-07T10:56:55Z

# 实施执行报告

## 执行结论

按计划完成 handle API、Redis payload 分离、旧格式惰性迁移，并通过隔离单节点 Redis 与临时三主三从 Cluster 实测。实测发现并修复迁移后读取返回重复旧 member 的缺陷，也修正 Cluster 测试跨 slot 清理。EasyCaching.Tests 在 net6.0、net8.0 全量回归及 netstandard2.0 构建通过。仅旧/新二进制蓝绿切换仍需外部发布环境，因此本任务状态为 `PARTIAL`。

## 任务信息

- Task ID：`BING-CACHE-TODO-20261007-099`
- Plan：[`plan.md`](plan.md)
- TODO：[`TODO.md`](TODO.md)
- 迁移说明：[`migration.md`](migration.md)
- 测试映射：[`test-mapping.md`](test-mapping.md)

## 计划执行情况

| 子项 | 状态 | 结果 |
| --- | --- | --- |
| 099-01 公开 handle 操作和单节点迁移行为 | `CLOSED` | 新字符串 API、旧 DTO 重载、payload hash、原子重放/删除和历史格式迁移已实现；临时 Redis 7.4 上 outbox 专项 net6.0/net8.0 各 11/11 通过。 |
| 099-02 Redis Cluster 实机验证 | `CLOSED` | 临时三主三从 Cluster 上同槽脚本、旧格式迁移、重放/删除和命名空间哨兵测试 net6.0/net8.0 各 1/1 通过。 |
| 099-03 旧/新二进制蓝绿切换验证 | `BLOCKED_EXTERNAL` | 需要隔离发布环境验证旧二进制退出访问 dead-letter 集合后，新版本读取并迁移历史数据；本轮没有该发布环境。 |

## 已完成事项

- `IHybridInvalidationDeadLetterAdmin` 增加按公开 handle 操作的重放和确认删除重载，保留原 DTO 重载。
- Redis Sorted Set 使用 SHA-256 handle，payload 单独保存在同槽 hash；读取时按请求页惰性迁移旧 payload member 并保留 score。
- 重放、删除、清理、字节重算和新旧格式重复去重在 Lua 中原子维护；重复记录合并时修正 payload 字节计数。
- delivery 在 payload 查询前授权，校验 handle 和确认 token，并把 handle 放入授权请求。
- 单元测试覆盖跨 delivery 实例 JSON handle/token、旧 DTO 兼容、非法 handle、错误 token、未授权不读 payload。
- 增加单节点 Redis 迁移/字节预算/混合格式去重测试，以及三主 Cluster 门禁测试。
- 真实 Redis 首轮发现 `ReadDeadLettersAsync` 先构造旧页结果、再执行迁移，导致同页旧 payload member 与 handle 同时返回；改为迁移后重读有界页，修复该竞态窗口中的重复返回。
- Cluster 首轮仅在 finally 清理时失败：测试把不同 hash slot 的哨兵键和 outbox keys 一起调用多键删除；已拆分为同槽 outbox 删除和独立哨兵删除。
- 更新 EasyCaching README、迁移说明、缓存能力索引及生产符号到测试方法映射。

## 部分/未完成事项

- 单节点 Redis 和三主三从 Cluster 门禁均已实测通过；临时容器已停止并自动移除，现有持久化 Redis 服务未触碰。
- 旧/新二进制并行访问共享 dead-letter 的发布场景未实测。迁移说明记录了格式切换时旧二进制必须停止访问该集合的要求。
- `OPEN_ACTIONABLE`：0。唯一未完成项需要隔离发布环境，状态为 `BLOCKED_EXTERNAL`，不自动进入下一轮代码修改。

## 修改文件

- `framework/src/Bing.EasyCaching/Bing/EasyCaching/HybridInvalidationDeadLetterAdmin.cs`
- `framework/src/Bing.EasyCaching/Bing/EasyCaching/HybridInvalidationDeadLetterHandle.cs`
- `framework/src/Bing.EasyCaching/Bing/EasyCaching/HybridInvalidationDelivery.cs`
- `framework/src/Bing.EasyCaching/Bing/EasyCaching/IHybridInvalidationDeadLetterAdminOutbox.cs`
- `framework/src/Bing.EasyCaching/Bing/EasyCaching/RedisHybridInvalidationOutbox.cs`
- `framework/tests/Bing.EasyCaching.Tests/HybridInvalidationDeliveryTest.cs`
- `framework/tests/Bing.EasyCaching.Tests/RedisHybridInvalidationOutboxIntegrationTest.cs`
- `framework/tests/Bing.EasyCaching.Tests/EasyClusterIntegrationTest.cs`
- `framework/src/Bing.EasyCaching/README.md`
- `ai_docs/cache-todo-index.md`
- `ai_docs/tasks/BING-CACHE-TODO-20261007-099/{TODO.md,migration.md,test-mapping.md,execution.md}`

## API/数据/配置变化

- 新增 `ReplayAsync(string handle, ...)` 和 `ConfirmDeleteAsync(string handle, string token, ...)`；旧签名保留。
- 授权请求新增 handle 字段；单条授权在读取 payload 前执行，MessageId 此时为空。
- dead-letter Redis 内部 member 改为摘要 handle，payload hash 与所有辅助键沿用 outbox root 的 hash tag；不增加依赖或配置。

## 测试结果

| 命令/门禁 | 结果 |
| --- | --- |
| `dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net8.0` | 133 通过，70 跳过，0 失败 |
| `dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net6.0` | 133 通过，70 跳过，0 失败 |
| `RedisHybridInvalidationOutboxIntegrationTest` + 临时 Redis 7.4，net8.0/net6.0 | 各 11 通过，0 跳过，0 失败 |
| `RedisCluster_DeadLetterHandlesMigrateReplayAndDeleteInOneSlot` + 临时三主三从 Cluster，net8.0/net6.0 | 各 1 通过，0 跳过，0 失败 |
| 单节点首次运行 | 10 通过、1 失败；发现混合旧 member 与 handle 的读取页重复，修复后门禁全通过 |
| Cluster 首次运行 | 主体结果被 finally 清理跨 slot 删除异常遮蔽；拆分清理后 net6.0/net8.0 均通过 |
| `git diff --check` | 通过；Git 仅提示两个既有 CRLF 文件未来会规范为 LF |

NuGet 服务索引不可达，构建/测试出现 `NU1801`、`NU1900` 和近似包版本 `NU1603` 警告；本轮未执行 restore。测试编译另有既存 xUnit analyzer 警告。

## Build/Typecheck/Lint/Format

- `dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj --no-restore --framework netstandard2.0 --verbosity quiet`：0 错误，2 个 NuGet 漏洞数据源警告。
- 未修改计划要求之外的 Provider、Core 或 benchmark 项目；未运行完整解决方案构建。
- `git diff --check`：通过；没有空白错误。

## 计划偏差

- 在计划要求的惰性迁移上增加了混合版本写入产生重复 payload/handle member 时的原子去重和字节计数修复，并加入对应 Redis 门禁测试。
- 临时隔离基础设施可用后补跑真实 Redis/Cluster 门禁；测试发现的重复返回与测试清理问题均已修复并重跑通过。
- 跨版本部署验证仍依赖隔离蓝绿发布环境，未以 Redis 单元或 Cluster 测试替代。

## 基线问题

- 仓库在开始前已有大量缓存任务未提交文件和文档修改；本轮只处理 099 相关文件，没有清理或重置其他改动。

## 已知问题

- 新版 Redis dead-letter member 布局不兼容旧二进制的读取/清理逻辑。切换共享 outbox 格式前需让旧二进制停止访问该集合；滚动并行行为仍待外部验证。
- 当前 `DeadLetterMaxBytes` 只计 payload UTF-8 字节，不代表 Redis allocator 实际内存。

## 风险与回归关注点

- Lua 多键脚本必须继续保持在同一个 Redis Cluster hash slot；本轮三主三从 Cluster 门禁已确认同槽执行通过。
- 旧格式惰性迁移按查询条数有界执行；Redis 单节点和 Cluster 覆盖了迁移、重复合并与字节计数，但未模拟真实旧版应用持续并发写入。
- 当前唯一验证阻塞是跨版本蓝绿发布场景。

## Reviewer 注意事项

- 检查 `RedisHybridInvalidationOutbox` 中 `DeadLetterScript`、`MigrateLegacyDeadLettersScript`、`ReplayDeadLetterScript` 和 `DeleteDeadLetterScript` 的 KEYS 顺序及 hash slot；本轮 Cluster 门禁已通过。
- 检查新旧混合 member 去重时的 `:sizes`/`:bytes` 修正及迁移后重读页；单节点 Redis 门禁 `Outbox_DeduplicatesLegacyPayloadAndHandleWithoutInflatingByteBudget` 已在 net6.0/net8.0 通过。
- 跨版本部署保持待验证，不能由本地单节点或 Cluster 成功代替。

## Git 状态

- 未自动 git commit。
- 未自动 git push。
- 未自动创建 PR。
- 保留仓库既有未提交修改。
