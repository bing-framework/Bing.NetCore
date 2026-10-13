# BING-CACHE-TODO-20261007-066：已提交批量变更的取消后失效传播

## 目标

修复 EasyCaching Redis/Hybrid 协调器在批量 Redis 命令已经提交后，调用方取消令牌恰好取消导致本地层失效或 Hybrid 总线发布被跳过的问题。保持“取消只阻止尚未派发的 Redis 命令；已提交写入/删除不回滚”的契约，并保证已提交变更仍完成必要的本地失效和跨实例通知。

## 现状与证据

- `RedisLeaseCoordinator.WriteBatchGroupAsync` 和 `RemoveBatchGroupAsync` 在 Redis 异步命令完成后继续使用调用方 `CancellationToken` 调用 `InvalidateBatchAsync`。
- `HybridCacheManager` 的批量回调把该令牌传入本地 `RemoveAllAsync` 和总线 `PublishAsync`。令牌在 Redis 命令完成后取消时，回调可能在本地失效前抛出 `OperationCanceledException`。
- 现有测试只覆盖取消后停止后续 Cluster 分组派发，没有覆盖“第一组 Redis 命令已完成、随后令牌取消、仍完成失效”的语义。
- 单键协调路径也存在同一边界：已提交 `WriteAsync`/`RemoveAsync` 后不能让取消令牌跳过 Hybrid 失效。

## 修改范围

### 任务 066-A：协调器提交后失效令牌

- 修改 `framework/src/Bing.EasyCaching/Bing/EasyCaching/RedisLeaseCoordinator.cs`。
- Redis 写入/删除命令完成后调用失效回调时使用 `CancellationToken.None`，保证已提交变更完成本地失效和总线传播；调用方令牌仍用于命令派发前检查、等待中的命令和未派发批次。
- 不改变同步路径、租约校验、批量字节预算、Cluster 分槽或部分成功语义。

### 任务 066-B：回归测试

- 在 `framework/tests/Bing.EasyCaching.Tests/RedisLeaseCoordinatorBatchTest.cs` 增加单键与批量写入/删除的“提交后取消仍回调”测试。
- 测试断言取消会阻止后续批次，但已完成 Redis 命令的失效回调收到非取消令牌并完成。

### 任务 066-C：证据与索引

- 更新 `test-mapping.md`、`TODO.md`、`execution.md` 和 `ai_docs/cache-todo-index.md`。
- 记录 Change Impact Analysis；不修改旧任务历史快照。

## 验证

1. `dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj --framework netstandard2.0 --no-restore`。
2. `RedisLeaseCoordinatorBatchTest` 在 net6.0、net8.0 精准测试。
3. `Bing.EasyCaching.Tests` 在 net6.0、net8.0 完整回归。
4. `git diff --check`。

## 风险与验收

- 风险：调用方取消后，已提交变更的失效回调仍会短暂继续执行；这是保持 Hybrid 最终一致所必需的提交后清理，不扩大 Redis 命令派发范围。
- 失效回调本身失败时继续传播原异常；已提交 Redis 变更不回滚。
- 验收：所有新增测试通过；取消后的第一组/已提交批次完成失效，后续未派发批次不执行；双 TFM 完整测试无新增失败。
