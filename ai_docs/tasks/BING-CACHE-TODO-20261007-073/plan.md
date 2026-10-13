# BING-CACHE-TODO-20261007-073：同步客户端分片读取复用有界路由

## 目标

收敛 FreeRedis 与 CSRedis 的同步客户端分片读取路径。当前同步 `GetAll` 和前缀批次读取在分片间逐组执行，绕过已在异步流式路径中实现的 `MaxConcurrency` 有界窗口。同步 API 继续返回 `List<T>`，但复用原生异步 I/O 的分片路由、索引回填和失败观察逻辑，不使用 `Task.Run` 包装网络调用。

## 现状与证据

- FreeRedis `FreeRedisCacheManager.GetAll<T>` 的 `ClientSharded` 分支按 `GroupIndexes` 逐客户端调用同步 `ReadPhysicalBatch`；`ReadScannedBatch<T>` 也按客户端顺序调用同步 MGET。
- CSRedis `CSRedisCacheManager.GetAll<T>` 在非 Cluster 路径统一调用同步 `ReadRawBatch`，客户端分片没有进入 `ReadShardedValuesAsync<T>`；`ReadScannedBatch<T>` 同样绕过分片有界路由。
- FreeRedis `ReadValuesBatchAsync<T>` 已按客户端分组、`MaxConcurrency` 分窗并按输入索引回填；CSRedis `ReadShardedValuesAsync<T>` 已具备同等语义。两者都使用 `ConfigureAwait(false)`，同步桥接可避免依赖调用方同步上下文。

## 修改范围

### 生产代码

- FreeRedis `GetAll<T>` 和 `ReadScannedBatch<T>` 的 `ClientSharded` 路径改为按现有批大小调用 `ReadValuesBatchAsync<T>(...).GetAwaiter().GetResult()`。
- CSRedis `GetAll<T>` 和 `ReadScannedBatch<T>` 的 `ClientSharded` 路径改为调用 `ReadShardedValuesAsync<T>(...).GetAwaiter().GetResult()`。
- 保持单节点、Cluster、删除、租约、键格式、批大小和公共签名不变；同步调用只阻塞当前调用线程，不创建线程池任务。

### 测试与证据

- 扩展 FreeRedis/CSRedis 现有双节点分片集成测试，验证同步批量读取的顺序、重复键、缺失默认值及前缀读取结果与异步路径一致。
- 继续使用分片环境门控；没有第二个非生产 Redis 节点时只报告跳过和外部阻塞，不把单节点结果当作跨节点并发证据。
- 更新权威缓存 TODO 索引、执行记录和生产符号到测试方法映射。

## 实施步骤

1. 建立 073 任务状态和文档，登记同步路径的新根因。
2. 修改 FreeRedis 同步 List/前缀读取的 ClientSharded 分支，复用有界异步辅助。
3. 修改 CSRedis 对应分支，保留 Cluster 和普通单节点分支。
4. 在两个分片集成测试中加入同步顺序/重复/缺失和前缀结果断言。
5. 运行生产 `netstandard2.0` 构建、分片精准测试、公共探针回归和差异检查。
6. 按 L0-L5 记录本地证据与外部环境边界，更新索引并收口任务。

## 风险与边界

- 同步 API 仍会占用调用线程等待 Redis I/O；这是既有同步 API 的固有成本，本轮只消除分片间不必要的串行调度和重复实现。
- 已发送的异步命令无法被同步调用方取消；同步签名没有 `CancellationToken`，不引入伪取消语义。
- 跨分片批量操作仍可能部分成功并传播异常，不宣称事务性。
- EasyCaching 分片前缀扫描仍需单独定义全局扫描预算、结果顺序和部分失败语义，本轮不修改。

## 验收标准

- FreeRedis/CSRedis 同步 ClientSharded `GetAll` 和前缀批次读取进入现有 `MaxConcurrency` 窗口，代码中不再对这些路径逐组同步 MGET。
- List 结果保持输入顺序、重复键位置和缺失/default 语义；前缀结果与异步读取保持相同值集合。
- 生产项目 `netstandard2.0` 构建无警告/错误；精准测试无失败；真实双节点证据按环境门控分类。
