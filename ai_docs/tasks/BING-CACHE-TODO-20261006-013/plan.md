# EasyCaching 前缀流取消计划

## 目标

补齐 EasyCaching `CacheManager.GetByPrefixStreamAsync` 消费首批结果后取消的批量读取证据，确认取消令牌阻止下一批 `GetAllAsync` 派发。

## 范围

- 修改 `framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs`。
- 使用现有 `IEasyCachingProvider` mock，不接触生产代码。
- 更新本任务 TODO、执行记录和测试映射。

## 验收标准

1. 配置 `BatchSize=2`，Provider 返回四个同前缀物理键。
2. 首批 `GetAllAsync` 返回后消费一个流元素并取消令牌。
3. 下一次 `MoveNextAsync` 抛出 `OperationCanceledException`。
4. Provider 的 `GetAllAsync` 调用次数严格为 1，且首批请求包含两个键。

## 非目标

- 不改变 EasyCaching 生产实现、Provider 注册或性能预算。
- 不运行 Cluster、总线或性能矩阵。
