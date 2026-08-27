# 实施决策

- task-id: `BING-SQL-RC-HARDENING-20260826-002`

## 已确认

1. 不执行 `git add`、`git commit`、`git push`、PR、reset、clean 或历史改写。
2. 当前工作树初始仅有本任务 `plan.md` 未跟踪文件，未发现需合并的用户业务修改。
3. 当前 .NET SDK 为 `10.0.300`，仓库没有 `global.json`；实际发布 SDK 需由 CI/发布配置继续确认，不能凭空固定版本。
4. 外部 Provider 真实数据库测试必须遵守既有环境变量和安全数据库 Gate，缺少环境时逐项记录为 `blocked`。
5. 前序任务 Round 3-10 的不完整 FormalHost before 不作为本任务有效性能基线。
6. Join 普通入口保留 `predicate, string rightAlias = null`；需要左侧 alias 或 schema 时统一使用 `SqlJoinOptions`，不保留多字符串兼容重载。
7. `SqlJoinOptions` 采用仅初始化可写的三个属性 `RightAlias`、`LeftAlias`、`Schema`；null 表示使用现有来源解析和 Provider 默认配置。
8. 底层 `ISqlBuilder.ClearSelect()` 保留，因为它服务 Builder/CRUD 独立职责；仅删除高层 Lambda 查询的 `ClearSelect()`，避免恢复已删除的查询兼容入口。
9. Raw Fluent 空白追加在扩展层短路，不经过 mutation gateway，因此不改变版本、缓存或参数状态。

## 待裁决

- Runtime SPI 每个成员的 public/internal/private 边界，继续以消费者搜索和编译结果为准；本轮未发现需要新增生产 IVT 的跨程序集消费者。
