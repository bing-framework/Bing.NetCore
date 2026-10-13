# 缓存文档注册入口对齐计划

## 目标

修正当前使用文档、架构说明和配置参考中已经失效的 EasyCaching 注册结论，使文档与 `CachingServiceCollectionExtensions`、admin 注册代码及当前缓存契约一致。

## 范围

- 更新当前使用文档中的缓存注册表、FAQ、包索引、最佳实践、配置参考和架构说明。
- 说明 `AddMemoryCaching`、`AddRedisCaching`、`AddHybridCaching`、`AddShardedRedisCaching` 和 `AddShardedHybridCaching` 的适用边界。
- 保留 `docs/migrations`、`docs/CHANGELOG` 和 ADR 等历史快照，不把历史事实改写成当前状态。
- 不修改缓存运行时代码、公共 API、测试逻辑或依赖。

## 验收标准

1. 当前文档不再声称 EasyCaching 注册扩展被注释或 `CachingOptions` 仍存在。
2. 文档明确区分通用 `AddCache`/`AddCaching` 不存在与 EasyCaching 的具体注册入口。
3. Redis/Hybrid/客户端分片的注册示例与 README、生产注册代码一致。
4. 历史迁移和变更记录仍保留其历史语境，不参与当前能力判断。
5. 所有修改文本保持 UTF-8，Markdown 链接和 `git diff --check` 通过。

## 验证

- 静态搜索当前文档中的过时缓存结论。
- 检查修改文件 UTF-8 可读性和 Markdown 链接目标。
- 运行 `git diff --check`。
