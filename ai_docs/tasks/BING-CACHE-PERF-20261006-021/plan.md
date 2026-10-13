<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-PERF-20261006-021
AI_EXECUTION_STARTED_AT: 2026-10-06T01:20:00.000Z

# BING-CACHE-PERF-20261006-021：批量标量解码去除装箱

## 目标

减少 FreeRedis、CSRedis 批量标量读取中 `Nullable<T>[]`、反射 `Array.GetValue` 和逐项装箱造成的分配。保持现有 List API 的返回顺序、重复键、缺失值、`null/default`、命名空间隔离、Cluster/分片路由和取消语义不变；流式 API 的公开契约和行为不变。

## 证据与范围

当前 FreeRedis、CSRedis 的默认标量解码会先构造 `Nullable<T>[]`，再通过非泛型数组访问读取每个元素；`Array.GetValue` 对值类型逐项装箱。短基准显示 List/Stream 收集差异很小，继续围绕 `CacheEntry<T>` 收集会触发无效重复实验。该轮只处理这个新的结构性热点，不重复已经停止的字符串 MGET、哈希上下文或容量预分配实验。

实施范围：

1. FreeRedis、CSRedis 默认 JSON 标量路径直接反序列化为 `T[]`，用原始响应是否为 `null` 单独判断 Hit；保留自定义序列化器、对象类型和 `JsonConvert.DefaultSettings` 的逐项路径。
2. 确认非 nullable 值类型数组中的 Redis 缺失/显式 `null` 仍映射为 `default(T)`，不把反序列化值误当 Hit 状态。
3. 保留既有原始批量读取、槽位分组、客户端分片路由、并发上限、预算校验、扫描拓扑校验和取消检查。
4. EasyCaching 的 List 直连路径暂不作为本轮实现目标；由于当前分配差异证据不足，记录为后续候选，避免引入低收益重复路径。
5. 只新增行为级回归和短基准；不把绝对短基准冒充历史正式性能预算通过。

## 设计约束

- 物理键和逻辑键仍由现有 `CacheKeyFormat` 生成，不改变键格式。
- 扫描结果继续逐批消费，不全量收集；扫描读取期间的拓扑校验和失败传播保持原实现。
- 已经发送的 Redis 命令不承诺撤销；取消后停止派发新批次。
- 缺失的输入键在 `GetAllAsync` 中仍返回 `default` 占位；前缀读取保持当前 Provider 的命中/缺失行为。
- 不增加新的公共 API，不改变同步路径，不改变租约和回源协调。

## 验证

- FreeRedis、CSRedis 精准批量契约，覆盖 `int`、`long`、`decimal` 的命中、缺失、显式 `null`、重复键和 Stream/List 等价性，net6.0/net8.0。
- 受影响生产项目 netstandard2.0 构建。
- 1k 短基准记录 List 读取、前缀扫描的 P95、分配量、命令数；必要时启用 `--allocation-components=true`，与 task020/历史结果只做结构性观察。
- `git diff --check`。

## 明确未在本轮关闭

- 旧版/当前版/优化版严格三方基线。
- 10k/100k 完整资源矩阵和正式 P95/分配预算。
- EasyCaching 专用 52800 MONITOR、完整 Cluster/分片/独立总线环境。
- T02/T03 之后重新生成的跨进程回源证据。
