# EasyCaching Redis/Hybrid 批量协调路径优化计划

## 目标

修复 Redis/Hybrid 适配器在启用 Redis 回源协调器后，`SetAll`、`SetAllAsync`、`RemoveAll`、前缀删除和 `Clear` 退化为逐键协调命令的问题。批量路径应继续撤销对应租约并传播 Hybrid 失效，但在单节点、Cluster 同槽和客户端分片单节点管理器内使用有界批处理。

## 根因

`CacheManager.Cluster.cs` 在 `_coordinator != null` 时逐项调用 `WriteValue` 或 `DeleteValue`。每项都会单独执行 Lua/KeyDelete，并在 Hybrid 模式单独删除本地值和发布一条总线消息，导致大批量操作的 Redis 命令、分配和总线消息数量线性放大。

## 实现范围

- 为 `RedisLeaseCoordinator` 增加批量覆盖写入和批量删除路径。
- 单节点批量使用一个脚本/删除命令；Redis Cluster 按 hash slot 分组；客户端分片由每个单节点管理器批量执行。
- `SetAllAsync`、批量删除和扫描删除使用已有 `BatchSize`、`MaxConcurrency` 边界，取消只阻止后续派发并观察已发送任务。
- Hybrid 提供批量本地失效和一条包含多个键的失效消息；普通 Redis 不改变租约和覆盖写入语义。
- 保留失败时的部分成功语义，已提交 Redis 写入不能回滚，失效发布失败继续向调用方报告。
- 补单元测试，验证批量脚本/删除调用数量、键租约配对、Hybrid 批量失效回调、取消和异常传播。

## 非目标

- 不改变公开接口、物理键格式、租约时长或 Cluster 路由规则。
- 不宣称跨键事务性；不在没有同构性能基线时修改性能预算。
- 不替代三主三从 Cluster、双进程回源或独立总线的外部集成门禁。

## 验收

1. 协调器批量写入/删除按拓扑分组，单节点同批不再逐键发送。
2. 批量路径仍覆盖数据键与 `:lease` 键，Hybrid 本地层及总线失效保持正确。
3. 取消、Redis/总线失败和部分成功异常可观察，已派发操作被等待回收。
4. `Bing.EasyCaching.Tests` net6.0/net8.0 通过，生产 netstandard2.0 构建通过，`git diff --check` 通过。
