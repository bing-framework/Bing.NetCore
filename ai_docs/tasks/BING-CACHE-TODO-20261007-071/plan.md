# Hybrid 失效消息预算测量实例一致性计划

## 目标

修复任务 070 引入的一个边界：消息拆分器在计算 serializer 载荷预算后重新构造消息，导致自定义 serializer 对 `Id` 或其他消息元数据采用非固定编码时，最终发布消息可能与预算测量对象不同。让每个已测量消息对象直接作为发布对象，并避免重复测量已经确定的批次。

## 现状/证据

- `HybridInvalidationMessageBatcher.CreateMessages` 在候选消息通过预算后丢弃该实例；切分或收尾时再次调用 `CreateMessage`。
- 默认预算为 0 时不测量，行为不受影响。
- 正值预算的验收要求是“实际 serializer 载荷不超过上限”，因此必须对最终发布的同一对象测量。

## 修改范围

- `framework/src/Bing.EasyCaching/Bing/EasyCaching/HybridInvalidationMessageBatcher.cs`
- `framework/tests/Bing.EasyCaching.Tests/HybridInvalidationMessageBatcherTest.cs`
- 本任务的 plan/TODO/test-mapping/execution 和权威缓存 TODO 索引。

## 实施步骤

1. 在正值预算路径保存已通过测量的当前 `EasyCachingMessage`。
2. 遇到下一个键超限时直接发布/产出保存的对象，再为新批次创建并测量新对象。
3. 收尾时直接产出最后一个已测量对象；单键超限继续抛出原异常。
4. 增加 serializer 每次输出大小/Id 可变化的测试，断言发布对象本身再次序列化仍不超限，并验证每个键只参与必要的候选测量。

## 非目标

- 不改变消息预算配置、默认值、消息顺序、重复键、取消和总线失败语义。
- 不引入消息代理背压、重试或 outbox。

## 验证与验收

- `HybridInvalidationMessageBatcherTest` 覆盖动态消息元数据和拆分边界。
- EasyCaching 精准测试 net6.0/net8.0；受影响生产项目 netstandard2.0 构建。
- `git diff --check`、严格 UTF-8、静态调用点检查通过。
- 正值预算路径发布的对象就是被 serializer 测量的对象；默认 0 路径保持单条消息。
