<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: sql-mapping-cache-versioning
AI_EXECUTION_FINISHED_AT: 2026-09-18T18:45:00+08:00

# 实施执行报告

## 执行结论

计划内缓存正确性、版本化发布、Query/Builder 快照、Mutation 隔离、测试、基准场景和文档均已完成。

## 任务信息

- Task ID：`sql-mapping-cache-versioning`
- 执行器：Codex
- 状态：COMPLETED

## 计划执行情况

- T01：统一 DbKey、MappingProfile、TableRouteKey 规范化，完成。
- T02：Database、Schema、TableName 精确隔离，完成。
- T03：新增版本化解析器、配置深快照和原子发布，完成。
- T04：Builder、Clone、Dapper Query 捕获固定解析器，Mutation 缓存按快照解析器分区，完成。
- T05：保留 DatabaseType Provider 隔离并记录扩展契约，完成。
- T06：完成发布、并发冷解析、命中、冷解析、满容量旁路和 LRU 的正式基准与结论归档。
- T07：公共 API、迁移文档、测试追踪和验证完成。

## 已完成事项

- 新增 `IEntityMappingSnapshotProvider` 与 `VersionedEntityMappingResolver`。
- 默认解析器深复制映射及列配置；直接修改源配置不再隐式生效。
- 发布失败保留旧版本，并发发布生成单调版本。
- Query/Builder 创建后固定映射快照，Clone 沿用原快照。
- 修复物理对象名大小写碰撞，并同步 Mutation Plan 缓存键。

## 部分/未完成事项

无计划内遗留。功能、测试、兼容性、正式性能基准和结果归档均已完成，整体计划完成度为 100%。

## 修改文件

修改覆盖 Bing.Data.Sql 映射解析与缓存、Bing.Dapper.Core Query 生命周期、直接测试、Benchmark、公共 API 基线、迁移指南和测试追踪文档。

## API/数据/配置变化

- 新增 `VersionedEntityMappingResolver.CurrentVersion`、`PublishMappings`、`CaptureSnapshot`。
- 新增 `IEntityMappingSnapshotProvider.CaptureSnapshot`。
- 无数据库 Schema 或历史数据迁移。
- `DefaultEntityMappingResolver` 的映射配置改为构造时冻结；运行时更新应迁移到显式发布。

## 测试结果

- Bing.Data.Sql.Tests net8.0：1317 passed。
- Bing.Data.Sql.Tests net6.0：1316 passed。
- Bing.Dapper.Core.Tests net8.0：162 passed。
- Bing.Dapper.Sqlite.Tests net8.0：112 passed。
- Bing.EntityFrameworkCore.Tests net6.0：61 passed。
- Bing.FreeSQL.Tests net8.0：33 passed。

## Build/Typecheck/Lint/Format

- Bing.Dapper.Core 构建通过，0 warnings / 0 errors。
- Bing.Data.Sql.Benchmarks net8.0 Release 构建通过。`FormalHost` 正式执行 18 个缓存相关场景，全部产生有效结果；详细数据见 benchmark-notes.md。
- `git diff --check` 通过；仅报告工作区行尾转换提示。

## 计划偏差

计划文件在用户批准后由执行阶段补写。EF Core 测试项目实际只目标 net6.0，因此按 net6.0 运行。未更改 `global.json`。

## 基线问题

部分既有项目包含 XML 注释、过时 API、xUnit2031 和 SQLite RID 警告，本任务未扩大范围处理。

## 已知问题

无已知功能问题。自定义解析器只有实现快照接口时才参与版本固定契约。

## 风险与回归关注点

物理对象名现在区分大小写和有效空白；依赖旧合并行为的调用会产生独立缓存项。直接修改映射配置对象不再生效。

## Reviewer 注意事项

重点检查版本发布的原子替换、Dapper Query 构造时捕获、Builder Clone 快照复用以及 Mutation Plan 物理名称键。

## Git 状态

未执行 git add、commit、push 或创建 PR。


