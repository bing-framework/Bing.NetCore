# 缓存命令探针写入、删除与命中读分类计划

## 目标

补齐上一轮逻辑命令分类的可观测范围，使 FreeRedis、CSRedis 和 EasyCaching 的异步写入、删除及单键命中读取可以在 CacheBench 阶段报告中被区分。该轮只增加内部性能观测，不改变缓存读写、批处理、取消、命名空间或租约语义。

## 实施范围

1. 在 `Bing.Caching` 内部命令探针增加 `SingleWrite`、`Delete` 分类，保持现有枚举值和旧兼容入口稳定。
2. 标注 FreeRedis、CSRedis 的直接单键读、脚本写入、删除调用；标注 EasyCaching 普通 Provider 的单键读、写入和删除调用。批量读、扫描沿用上一轮分类。
3. 为 CacheBench 的命中阶段建立与其他阶段一致的探针 scope，输出 `nativeClientCalls`、`commandBreakdown` 和逻辑调用并发数据；真实网络请求继续使用未知值 `-1`。
4. 增加命令分类单元测试，执行受影响生产构建、CacheBench 构建和最小单节点 smoke，记录证据及生产符号映射。

## 验收标准

- 旧 `InvokeAsync(Func<Task<T>>)` 仍归类为 `Unknown`，已有批量读/扫描分类不变。
- 写入阶段可见 `SingleWrite`，删除阶段可见 `Delete`，命中阶段可见 `SingleRead`；分类只统计探针包裹的逻辑客户端调用。
- 命令异常后活动数、总调用数和分类计数保持一致；不引入重复包裹导致的虚假计数。
- FreeRedis、CSRedis、EasyCaching 的 netstandard2.0 构建、Bing.Caching.Tests 双 TFM 精准测试、CacheBench 构建、smoke 和 `git diff --check` 通过。

## 不在本轮范围

- 不新增真实 TCP/Redis 网络往返、连接池排队、重试或重定向指标。
- 不改动租约脚本、批大小、并发限制、键格式、公共缓存接口或性能预算。
- 不重跑严格三方性能基线、100k 多拓扑矩阵或独立进程协调证据。
